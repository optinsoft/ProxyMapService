FROM mcr.microsoft.com/devcontainers/dotnet:2-8.0-noble

RUN apt-get update && apt-get install -y \
    cmake \
    build-essential \
    ninja-build \
    pkg-config \
    git \
    libnss3-dev \
    libnspr4-dev \
    ca-certificates \
    curl \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Node.js 22 (LTS) for building the dashboard (Vue/Vite; requires ^20.19.0 || >=22.12.0)
RUN set -eux; \
    mkdir -p /etc/apt/keyrings; \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg; \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_22.x nodistro main" > /etc/apt/sources.list.d/nodesource.list; \
    apt-get update && apt-get install -y nodejs; \
    rm -rf /var/lib/apt/lists/*; \
    node --version && npm --version
