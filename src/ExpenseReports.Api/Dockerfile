FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so dependency layers are cached between builds.
COPY global.json Directory.Build.props ./
COPY src/ExpenseReports.Domain/ExpenseReports.Domain.csproj src/ExpenseReports.Domain/
COPY src/ExpenseReports.Application/ExpenseReports.Application.csproj src/ExpenseReports.Application/
COPY src/ExpenseReports.Infrastructure/ExpenseReports.Infrastructure.csproj src/ExpenseReports.Infrastructure/
COPY src/ExpenseReports.Api/ExpenseReports.Api.csproj src/ExpenseReports.Api/
RUN dotnet restore src/ExpenseReports.Api/ExpenseReports.Api.csproj

COPY src/ src/
RUN dotnet publish src/ExpenseReports.Api/ExpenseReports.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Clear the base image's ASPNETCORE_HTTP_PORTS so it does not conflict with
# ASPNETCORE_URLS (having both set logs an "Overriding HTTP_PORTS" warning at boot).
ENV ASPNETCORE_HTTP_PORTS=
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "ExpenseReports.Api.dll"]
