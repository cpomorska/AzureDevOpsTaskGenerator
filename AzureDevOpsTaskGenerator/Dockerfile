# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Use the .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["AzureDevOpsTaskGenerator/AzureDevOpsTaskGenerator.csproj", "AzureDevOpsTaskGenerator/"]

# Restore dependencies
RUN dotnet restore "AzureDevOpsTaskGenerator/AzureDevOpsTaskGenerator.csproj"

# Copy source code
COPY . .
WORKDIR "/src/AzureDevOpsTaskGenerator"

# Build the application
RUN dotnet build "AzureDevOpsTaskGenerator.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "AzureDevOpsTaskGenerator.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Create directory for task files
RUN mkdir -p /app/tasks

# Copy published application
COPY --from=publish /app/publish .

# Set the entry point
ENTRYPOINT ["dotnet", "AzureDevOpsTaskGenerator.dll"]