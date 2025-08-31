# ============================================================
# Stage 1: Build and publish the application
# ============================================================

# Base Image - .NET 9.0 SDK with Debian for building the application
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release

# 设置国内镜像源加速apt-get
RUN sed -i 's/deb.debian.org/mirrors.aliyun.com/g' /etc/apt/sources.list.d/debian.sources \
    && sed -i 's/security.debian.org/mirrors.aliyun.com/g' /etc/apt/sources.list.d/debian.sources

WORKDIR /src

# Copy project files first for better caching
COPY ["src/Mcp.Links.Http/Mcp.Links.Http.csproj", "src/Mcp.Links.Http/"]
COPY ["src/Mcp.Links/Mcp.Links.csproj", "src/Mcp.Links/"]

# Restore NuGet packages
RUN dotnet restore "src/Mcp.Links.Http/Mcp.Links.Http.csproj"

# Copy source code
COPY . .

# Build and publish the application
WORKDIR "/src/src/Mcp.Links.Http"
RUN dotnet build "Mcp.Links.Http.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
RUN dotnet publish "Mcp.Links.Http.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ============================================================
# Stage 2: Final runtime image
# ============================================================

# Base Image - .NET 9.0 ASP.NET Core runtime with Debian for running the application
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS final

# 设置国内镜像源加速apt-get
RUN sed -i 's/deb.debian.org/mirrors.aliyun.com/g' /etc/apt/sources.list.d/debian.sources \
    && sed -i 's/security.debian.org/mirrors.aliyun.com/g' /etc/apt/sources.list.d/debian.sources

# Install basic system packages
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install Python and related packages
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    && rm -rf /var/lib/apt/lists/*

# Install Node.js
RUN (curl -fsSL https://deb.nodesource.com/setup_lts.x | bash - || echo "Failed to setup NodeSource repository") \
    && (apt-get install -y nodejs || echo "Failed to install nodejs") \
    && rm -rf /var/lib/apt/lists/* \
    && (npm config set registry https://registry.npmmirror.com || true) \
    && (npm config set disturl https://npmmirror.com/mirrors/node || true) \
    && (npm config set electron_mirror https://npmmirror.com/mirrors/electron || true) \
    && (npm config set chromedriver_cdnurl https://npmmirror.com/mirrors/chromedriver || true)

# Install .NET 10 SDK and create symlinks
RUN curl -fsSL https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh | bash -s -- --version latest --channel 10.0 --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet-10 \
    && echo '#!/bin/bash\n/usr/bin/dotnet-10 dnx "$@"' > /usr/local/bin/dnx \
    && chmod +x /usr/local/bin/dnx

# Install uv (Python package manager) and create symlinks
RUN export UV_INSTALLER_GHE_BASE_URL="https://ghfast.top/https://github.com" \
    && curl -LsSf https://astral.sh/uv/install.sh | sh || true \
    && (ls -la /root/.local/bin/ || echo "uv installation may have failed") \
    && (cp /root/.local/bin/uv /usr/local/bin/uv || true) \
    && (cp /root/.local/bin/uvx /usr/local/bin/uvx || true) \
    && chmod +x /usr/local/bin/uv /usr/local/bin/uvx || true

# Configure uv/uvx to use Chinese mirrors for faster package downloads
RUN mkdir -p /etc/uv \
    && echo 'index-url = "https://pypi.tuna.tsinghua.edu.cn/simple"' > /etc/uv/uv.toml \
    && echo 'extra-index-url = ["https://mirrors.aliyun.com/pypi/simple/"]' >> /etc/uv/uv.toml \
    && mkdir -p /root/.config/uv \
    && cp /etc/uv/uv.toml /root/.config/uv/uv.toml || true

# Verify installations
RUN dotnet --version && node --version && npm --version && python3 --version && dotnet-10 --version && dotnet-10 --list-sdks && dnx --help && (uv --version || echo "uv not available")

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
# Set mirrors for package managers
ENV UV_INDEX_URL=https://pypi.tuna.tsinghua.edu.cn/simple
ENV UV_EXTRA_INDEX_URL=https://mirrors.aliyun.com/pypi/simple/
ENV NPM_CONFIG_REGISTRY=https://registry.npmmirror.com

# Expose the port the application listens on
EXPOSE 8080

# Configure health check - using root path since no specific health endpoint is configured
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Switch to non-root user for security
# USER $APP_UID

# Set the entry point for the application
ENTRYPOINT ["dotnet", "Mcp.Links.Http.dll"]
