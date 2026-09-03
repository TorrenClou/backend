# Stage 1: Build
# Pinned to the build machine's own architecture. dotnet publish runs with
# /p:UseAppHost=false, so the output is portable IL with no native launcher —
# identical for every target. Only the runtime stage below needs to vary by
# architecture, which keeps arm64 images from compiling under QEMU emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY TorrenClou.sln .
COPY TorrenClou.Core/TorrenClou.Core.csproj TorrenClou.Core/
COPY TorrenClou.Application/TorrenClou.Application.csproj TorrenClou.Application/
COPY TorrenClou.Infrastructure/TorrenClou.Infrastructure.csproj TorrenClou.Infrastructure/
COPY TorrenClou.API/TorrenClou.API.csproj TorrenClou.API/
COPY TorrenClou.Worker/TorrenClou.Worker.csproj TorrenClou.Worker/
COPY TorrenClou.GoogleDrive.Worker/TorrenClou.GoogleDrive.Worker.csproj TorrenClou.GoogleDrive.Worker/
COPY TorrenClou.S3.Worker/TorrenClou.S3.Worker.csproj TorrenClou.S3.Worker/

# Restore dependencies
RUN dotnet restore TorrenClou.sln

# Copy all source code
COPY TorrenClou.Core/ TorrenClou.Core/
COPY TorrenClou.Application/ TorrenClou.Application/
COPY TorrenClou.Infrastructure/ TorrenClou.Infrastructure/
COPY TorrenClou.API/ TorrenClou.API/

# Build and publish
WORKDIR /src/TorrenClou.API
RUN dotnet build -c Release --no-restore -o /app/build
# Stamped by CI so /api/version can report exactly what this image was built
# from. Optional: a local build simply reports them as unknown.
ARG BUILD_SHA=""
ARG BUILD_TIME=""

RUN dotnet publish -c Release --no-restore -o /app/publish /p:UseAppHost=false /p:BuildSha=$BUILD_SHA /p:BuildTime=$BUILD_TIME

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
ENTRYPOINT ["dotnet", "TorrenClou.API.dll"]
