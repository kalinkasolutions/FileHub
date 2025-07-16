FROM golang:latest AS builder
WORKDIR /app
COPY backend/go.mod backend/go.sum ./
RUN go mod tidy
COPY backend/ .
RUN CGO_ENABLED=1 go build  -v -o main .


FROM node:22 AS frontend-builder
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm install
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
RUN ls -la /app/

CMD ["./main"]