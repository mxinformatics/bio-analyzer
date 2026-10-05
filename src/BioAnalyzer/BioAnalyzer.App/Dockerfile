# Use the official .NET 10.0 runtime as the base image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET 10.0 SDK for building the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the solution file
COPY ["BioAnalyzer.sln", "."]

# Copy project files for dependency resolution
COPY ["BioAnalyzer.App/BioAnalyzer.App.csproj", "BioAnalyzer.App/"]
COPY ["BioAnalyzer.ServiceDefaults/BioAnalyzer.ServiceDefaults.csproj", "BioAnalyzer.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "BioAnalyzer.App/BioAnalyzer.App.csproj"

# Copy the entire source code
COPY . .

# Build the application
WORKDIR "/src/BioAnalyzer.App"
RUN dotnet build "BioAnalyzer.App.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BioAnalyzer.App.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "BioAnalyzer.App.dll"]
