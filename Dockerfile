# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first (better layer caching)
COPY PowerPositionService.csproj ./

# Restore all NuGet package dependencies defined in the project file(s)
# This command downloads and installs all required packages from NuGet feeds
# before the actual build process, ensuring all dependencies are available
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install tzdata for proper time zone support
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

# Create directories for state and output
RUN mkdir -p /app/data/pending /app/data/done /app/data/out /app/logs \
    && chown -R appuser:appgroup /app

# Copy published application
COPY --from=build /app/publish .

# Environment defaults
# The double underscore '__' is used to represent nested configuration sections in appsettings.json
ENV DOTNET_ENVIRONMENT=Production
ENV PowerPositionService__OutputDirectory=/app/data
ENV PowerPositionService__LogDirectory=/app/logs
ENV PowerPositionService__EnableFileLog=false

# Switch to non-root user
USER appuser

# Health check to ensure the service is running
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD pgrep -f "PowerPositionService" || exit 1

# Entry point to run the application
ENTRYPOINT ["dotnet", "PowerPositionService.dll"]