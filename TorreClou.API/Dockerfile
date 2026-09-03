# Stage 1: Build
# Pinned to the build machine's own architecture. dotnet publish runs with
# /p:UseAppHost=false, so the output is portable IL with no native launcher —
# identical for every target. Only the runtime stage below needs to vary by
# architecture, which keeps arm64 images from compiling under QEMU emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY TorreClou.sln .
COPY TorreClou.Core/TorreClou.Core.csproj TorreClou.Core/
COPY TorreClou.Application/TorreClou.Application.csproj TorreClou.Application/
COPY TorreClou.Infrastructure/TorreClou.Infrastructure.csproj TorreClou.Infrastructure/
COPY TorreClou.API/TorreClou.API.csproj TorreClou.API/
COPY TorreClou.Worker/TorreClou.Worker.csproj TorreClou.Worker/
COPY TorreClou.GoogleDrive.Worker/TorreClou.GoogleDrive.Worker.csproj TorreClou.GoogleDrive.Worker/
COPY TorreClou.S3.Worker/TorreClou.S3.Worker.csproj TorreClou.S3.Worker/

# Restore dependencies
RUN dotnet restore TorreClou.sln

# Copy all source code
COPY TorreClou.Core/ TorreClou.Core/
COPY TorreClou.Application/ TorreClou.Application/
COPY TorreClou.Infrastructure/ TorreClou.Infrastructure/
COPY TorreClou.API/ TorreClou.API/

# Build and publish
WORKDIR /src/TorreClou.API
RUN dotnet build -c Release --no-restore -o /app/build
RUN dotnet publish -c Release --no-restore -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# curl is needed by the container healthcheck; the aspnet image ships neither curl nor wget
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create downloads mount point
RUN mkdir -p /app/downloads

# Copy published output
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080
# Set entry point
ENTRYPOINT ["dotnet", "TorreClou.API.dll"]
