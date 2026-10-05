# Use the official .NET 10.0 runtime as the base image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Use the .NET 10.0 SDK for building the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the solution file
COPY ["BioAnalyzer.sln", "."]

# Copy project files for dependency resolution
COPY ["BioAnalyzer.EventHandlers/BioAnalyzer.EventHandlers.csproj", "BioAnalyzer.EventHandlers/"]
COPY ["BioAnalyzer.ServiceDefaults/BioAnalyzer.ServiceDefaults.csproj", "BioAnalyzer.ServiceDefaults/"]
COPY ["BioAnalyzer.AzureStorage/BioAnalyzer.AzureStorage.csproj", "BioAnalyzer.AzureStorage/"]
COPY ["BioAnalyzer.AzureStorage.Contracts/BioAnalyzer.AzureStorage.Contracts.csproj", "BioAnalyzer.AzureStorage.Contracts/"]
COPY ["BioAnalyzer.Infrastructure/BioAnalyzer.Infrastructure.csproj", "BioAnalyzer.Infrastructure/"]
# Restore dependencies
RUN dotnet restore "BioAnalyzer.EventHandlers/BioAnalyzer.EventHandlers.csproj"

# Copy the entire source code
COPY . .

# Build the application
WORKDIR "/src/BioAnalyzer.EventHandlers"
RUN dotnet build "BioAnalyzer.EventHandlers.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BioAnalyzer.EventHandlers.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Azure Functions specific environment variables
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

ENTRYPOINT ["dotnet", "BioAnalyzer.EventHandlers.dll"]
