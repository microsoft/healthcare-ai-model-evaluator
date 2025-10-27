# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/MedBench.API/MedBench.API.csproj", "src/MedBench.API/"]
COPY ["src/MedBench.Core/MedBench.Core.csproj", "src/MedBench.Core/"]
COPY ["src/MedBench.Infrastructure/MedBench.Infrastructure.csproj", "src/MedBench.Infrastructure/"]
COPY ["Directory.Packages.props", "."]

RUN dotnet restore "src/MedBench.API/MedBench.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/MedBench.API"
RUN dotnet build "MedBench.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MedBench.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published app
COPY --from=publish /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "MedBench.API.dll"] 