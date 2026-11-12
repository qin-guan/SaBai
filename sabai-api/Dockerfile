# Build context: this directory (sabai-api/).
FROM rust:1.88-bookworm AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/usr/local/cargo/registry \
    --mount=type=cache,target=/src/target \
    cargo build --release --locked && cp target/release/sabai-api /usr/local/bin/sabai-api

FROM debian:bookworm-slim AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 1654 app
USER app
WORKDIR /app
COPY --from=build /usr/local/bin/sabai-api /usr/local/bin/sabai-api
# Face models are downloaded on first use to ~/.local/share/sabai/models; mount a volume
# there, or pre-provision the files and set FaceRecognition__AutoDownloadModels=false.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["sabai-api"]
