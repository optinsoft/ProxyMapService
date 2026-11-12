FROM mcr.microsoft.com/devcontainers/dotnet:2-8.0-noble

RUN apt-get update && apt-get install -y \
    cmake \
    build-essential \
    ninja-build \
    pkg-config \
    git \
    libnss3-dev \
    libnspr4-dev \
    && rm -rf /var/lib/apt/lists/*
