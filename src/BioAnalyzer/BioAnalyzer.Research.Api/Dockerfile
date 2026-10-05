# Use the official .NET 10.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env

# Set the working directory
WORKDIR /app

# Copy solution file and restore dependencies
COPY ../../../BioAnalyzer.sln ./
COPY ../BioAnalyzer.Research.Api/BioAnalyzer.Research.Api.csproj ./BioAnalyzer.Research.Api/
COPY ../BioAnalyzer.AzureStorage.Contracts/BioAnalyzer.AzureStorage.Contracts.csproj ./BioAnalyzer.AzureStorage.Contracts/
COPY ../BioAnalyzer.AzureStorage/BioAnalyzer.AzureStorage.csproj ./BioAnalyzer.AzureStorage/
COPY ../BioAnalyzer.ServiceDefaults/BioAnalyzer.ServiceDefaults.csproj ./BioAnalyzer.ServiceDefaults/
COPY ../BioAnalyzer.Infrastructure/BioAnalyzer.Infrastructure.csproj ./BioAnalyzer.Infrastructure/

# Restore dependencies
RUN dotnet restore BioAnalyzer.Research.Api/BioAnalyzer.Research.Api.csproj

# Copy the source code
COPY ../BioAnalyzer.Research.Api/ ./BioAnalyzer.Research.Api/
COPY ../BioAnalyzer.AzureStorage.Contracts/ ./BioAnalyzer.AzureStorage.Contracts/
COPY ../BioAnalyzer.AzureStorage/ ./BioAnalyzer.AzureStorage/
COPY ../BioAnalyzer.ServiceDefaults/ ./BioAnalyzer.ServiceDefaults/
COPY ../BioAnalyzer.Infrastructure/ ./BioAnalyzer.Infrastructure/

# Build the application
RUN dotnet publish BioAnalyzer.Research.Api/BioAnalyzer.Research.Api.csproj -c Release -o out --no-restore

# Use the official .NET 10.0 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Set the working directory
WORKDIR /app


# Copy the published application from the build stage
COPY --from=build-env /app/out .

# Expose the port that the application listens on
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Define health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Set the entry point for the application
ENTRYPOINT ["dotnet", "BioAnalyzer.Research.Api.dll"]
