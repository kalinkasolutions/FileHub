# ---- Stage 1: build the Angular SPA ----
# angular.json's outputPath is ../wwwroot, so `npm run build` (from /app/ClientApp)
# emits the compiled SPA to /app/wwwroot.
FROM node:22-alpine AS client_build_env
WORKDIR /app/ClientApp

# Install deps first so this layer is cached unless the lockfile changes.
COPY FileHub.Api/ClientApp/package.json FileHub.Api/ClientApp/package-lock.json ./
RUN npm ci

COPY FileHub.Api/ClientApp/ ./
RUN npm run build

# ---- Stage 2: restore & publish the .NET app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet_build_env
WORKDIR /src

# Copy just the project files first so `restore` is cached unless a .csproj changes.
# These are the six projects FileHub.Api pulls in transitively; the test project is not
# part of the published app, so it is deliberately not restored here.
COPY FileHub.Api/FileHub.Api.csproj FileHub.Api/
COPY FileHub.BusinessLogic/FileHub.BusinessLogic.csproj FileHub.BusinessLogic/
COPY FileHub.Dal/FileHub.Dal.csproj FileHub.Dal/
COPY FileHub.Dtos/FileHub.Dtos.csproj FileHub.Dtos/
COPY FileHub.Entities/FileHub.Entities.csproj FileHub.Entities/
COPY FileHub.Shared/FileHub.Shared.csproj FileHub.Shared/
COPY Directory.Build.props ./
RUN dotnet restore FileHub.Api/FileHub.Api.csproj --disable-parallel

COPY . .
RUN dotnet publish FileHub.Api/FileHub.Api.csproj -c Release -o /app/out --no-restore

# ---- Stage 3: runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=dotnet_build_env /app/out ./
# The SPA is excluded from publish in the csproj, so copy the built wwwroot in directly.
# UseStaticFiles()/MapFallbackToFile serve it from ContentRoot/wwwroot at runtime.
COPY --from=client_build_env /app/wwwroot ./wwwroot

# What release this image is. The publish workflow passes the release tag, commit and build time;
# a plain `docker build` leaves the defaults, and the SPA then shows a development build.
# Deliberately written *after* the SPA copy, so version.json lives in wwwroot but not in the Angular
# build output — that keeps it out of the service worker's hashed asset manifest, which would
# otherwise cache it (or fail its hash check) once this step rewrites it.
ARG VERSION=""
ARG COMMIT_SHA=""
ARG BUILD_DATE=""
RUN printf '{"version":"%s","commitSha":"%s","builtAt":"%s"}\n' \
    "$VERSION" "$COMMIT_SHA" "$BUILD_DATE" > ./wwwroot/version.json

# SQLite lives here (connection string: Data Source=/var/srv/filehub.db) and so does the Data
# Protection key ring (/var/srv/keys). Mount a volume here: losing the key ring signs everyone out.
RUN mkdir -p /var/srv

ENV ASPNETCORE_URLS=http://+:4122
EXPOSE 4122

ENTRYPOINT ["dotnet", "FileHub.Api.dll"]
