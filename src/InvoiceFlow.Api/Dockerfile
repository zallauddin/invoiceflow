# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for restore caching
COPY InvoiceFlow.slnx .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/InvoiceFlow.Core/InvoiceFlow.Core.csproj src/InvoiceFlow.Core/
COPY src/InvoiceFlow.Application/InvoiceFlow.Application.csproj src/InvoiceFlow.Application/
COPY src/InvoiceFlow.Infrastructure/InvoiceFlow.Infrastructure.csproj src/InvoiceFlow.Infrastructure/
COPY src/InvoiceFlow.Api/InvoiceFlow.Api.csproj src/InvoiceFlow.Api/
RUN dotnet restore src/InvoiceFlow.Api/InvoiceFlow.Api.csproj

# Copy source and build
COPY src/ src/
WORKDIR /src/src/InvoiceFlow.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_ROLL_FORWARD=LatestMajor

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "InvoiceFlow.Api.dll"]
