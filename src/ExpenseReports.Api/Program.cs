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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<CurrentUserAccessor>());

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep raw claim names: sub, tenant_id, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtClaims.Role
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy(ExpenseEndpoints.ManagerPolicy, policy => policy.RequireRole("Manager")));

// Brute-force protection on /auth/login, partitioned by client IP.
var loginRateLimit = builder.Configuration.GetValue("RateLimiting:LoginAttemptsPerMinute", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginRateLimit, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapAuthEndpoints();
app.MapExpenseEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();
return;

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

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program;
