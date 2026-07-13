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
COPY src/InvoiceFlow.Workers/InvoiceFlow.Workers.csproj src/InvoiceFlow.Workers/
RUN dotnet restore src/InvoiceFlow.Workers/InvoiceFlow.Workers.csproj

# Copy source and build
COPY src/ src/
WORKDIR /src/src/InvoiceFlow.Workers
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_ROLL_FORWARD=LatestMajor

ENTRYPOINT ["dotnet", "InvoiceFlow.Workers.dll"]
