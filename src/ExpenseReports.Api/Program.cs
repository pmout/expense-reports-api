using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ExpenseReports.Api.Auth;
using ExpenseReports.Api.Endpoints;
using ExpenseReports.Api.ErrorHandling;
using ExpenseReports.Application;
using ExpenseReports.Application.Abstractions;
using ExpenseReports.Infrastructure;
using ExpenseReports.Infrastructure.Persistence;
using ExpenseReports.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs to stdout. Request bodies are never logged, so
// passwords and tokens cannot leak into the logs.
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

// One call per layer keeps the composition root readable and means Program.cs
// never references a concrete repository or handler — only the two entry points.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// CurrentUserAccessor reads the JWT claims. It is registered once and exposed
// through two interfaces resolving to the SAME scoped instance, so the identity
// the handlers see (ICurrentUser) and the tenant the DbContext filters on
// (ITenantProvider) are guaranteed to be one and the same within a request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<CurrentUserAccessor>());

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // By default the JWT handler renames claims to long XML URIs. Turning
        // that off keeps the short names we issued (sub, tenant_id, role), so
        // CurrentUserAccessor can read them by the exact names in the token.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Validate issuer, audience and signature: a token from anywhere
            // else, or tampered with, is rejected. (Lifetime is validated by
            // default, so an expired token is refused too.)
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtClaims.Role // lets [Authorize(Roles=...)] / RequireRole read our role claim
        };
    });

// A named policy keeps the manager check in one place; endpoints opt in with
// RequireAuthorization(ManagerPolicy) instead of repeating the role string.
builder.Services.AddAuthorization(options =>
    options.AddPolicy(ExpenseEndpoints.ManagerPolicy, policy => policy.RequireRole("Manager")));

// Brute-force protection on /auth/login. Partitioning by client IP means one
// attacker's attempts cannot lock out other users. The limit is read from
// configuration so tests can raise it without changing the production default.
var loginRateLimit = builder.Configuration.GetValue("RateLimiting:LoginAttemptsPerMinute", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginRateLimit, Window = TimeSpan.FromMinutes(1) }));
});

// Serialize enums as their names ("Pending", "EUR") rather than integers, so the
// JSON is self-describing and stable if the enum order ever changes.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Discovers every FluentValidation validator in this assembly; includeInternalTypes
// because the validators are internal (they are an implementation detail of the API).
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

// AddProblemDetails + the exception handler make every error a machine-readable
// application/problem+json response instead of an HTML error page or a stack trace.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// /healthz reports the app AND its ability to reach the database, which is what
// docker-compose waits on and what an orchestrator would probe.
builder.Services.AddHealthChecks().AddDbContextCheck<ExpenseReportsDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Expense Reports API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT obtained from POST /auth/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

await PrepareDatabaseAsync(app);

// Middleware order is significant — each item runs in this sequence per request:
//   1. ExceptionHandler first, so it can catch failures from everything after it.
//   2. Request logging next, to record every request that gets through.
//   3. RateLimiter before auth, so flooding login costs as little work as possible.
//   4. Authentication then Authorization — you must establish *who* the caller is
//      before you can decide *what* they may do. Swapping these two breaks security.
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapAuthEndpoints();
app.MapExpenseEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous(); // health must be reachable without a token

app.Run();
return;

// Applies pending migrations (and optionally seeds) on startup, gated by config:
// convenient for Docker/dev, but a real production deploy would run migrations as
// a separate step rather than on every app boot. Runs in its own DI scope because
// the DbContext is scoped and there is no request scope at startup.
static async Task PrepareDatabaseAsync(WebApplication app)
{
    var database = app.Configuration.GetSection("Database");
    if (!database.GetValue<bool>("MigrateOnStartup"))
        return;

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ExpenseReportsDbContext>();
    await db.Database.MigrateAsync();

    if (database.GetValue<bool>("SeedOnStartup"))
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

// Minimal-API programs have no Program class by default; declaring this partial
// gives WebApplicationFactory<Program> a type to reference in the integration tests.
public partial class Program;
