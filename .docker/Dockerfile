FROM docker.io/library/rust:1-slim-bookworm AS rust
FROM docker.io/library/php:8.3-cli-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        build-essential \
        git \
        unzip \
        libonig-dev \
        libzip-dev \
    && docker-php-ext-install -j"$(nproc)" mbstring zip \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY --from=docker.io/library/composer:2 /usr/bin/composer /usr/bin/composer
COPY --from=rust /usr/local/cargo /usr/local/cargo
COPY --from=rust /usr/local/rustup /usr/local/rustup

ENV PATH="/usr/local/cargo/bin:${PATH}" \
    RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/cargo \
    COMPOSER_ALLOW_SUPERUSER=1

WORKDIR /app
