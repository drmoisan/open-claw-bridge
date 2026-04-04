FROM mcr.microsoft.com/devcontainers/base:bookworm

ARG ACTIONLINT_VERSION=latest

RUN apt-get update \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        jq \
        less \
        lsof \
        procps \
        shellcheck \
        shfmt \
        sqlite3 \
        unzip \
        zip \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash \
    | bash -s -- "${ACTIONLINT_VERSION}" /usr/local/bin
