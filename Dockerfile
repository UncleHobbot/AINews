# Stage 1: Build React frontend
FROM node:20-alpine AS frontend-build
WORKDIR /frontend
COPY src/AINews.Frontend/package*.json ./
RUN npm ci
COPY src/AINews.Frontend/ ./
RUN npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY src/AINews.Api/AINews.Api.csproj ./AINews.Api/
RUN dotnet restore ./AINews.Api/AINews.Api.csproj
COPY src/AINews.Api/ ./AINews.Api/
RUN dotnet publish ./AINews.Api/AINews.Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user (UID 1001 — compatible with Synology volume permissions)
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup --no-create-home appuser

# Copy published API
COPY --from=api-build /app/publish ./

# Copy React build output into wwwroot (served as static files by ASP.NET)
COPY --from=frontend-build /frontend/dist ./wwwroot/

# Create writable directories
RUN mkdir -p /app/data /app/keys /app/logs && \
    chown -R appuser:appgroup /app

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AINews.Api.dll"]
