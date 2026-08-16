FROM golang:1.24-bookworm AS builder
WORKDIR /app
COPY backend/go.mod backend/go.sum ./
RUN go mod download
COPY backend/ .
RUN CGO_ENABLED=1 go build -v -o main .


FROM node:22-bookworm-slim AS frontend-builder
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build -- --configuration production

FROM debian:bookworm-slim
RUN set -x && apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /app/
COPY --from=builder /app/main .
COPY --from=builder /app/migrations/ ./migrations
COPY --from=frontend-builder /app/frontend/dist/browser ./frontend/
RUN chmod +x /app/main

# Run as a normal user instead of root. The host directory bind-mounted at
# /app/data must be writable by uid 1000: chown -R 1000:1000 ./data
RUN useradd --uid 1000 --create-home filehub
RUN mkdir -p /app/data && chown -R filehub:filehub /app
USER filehub

CMD ["./main"]
