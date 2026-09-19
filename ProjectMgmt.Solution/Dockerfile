# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["ProjectMgmt.Contracts/ProjectMgmt.Contracts.csproj", "ProjectMgmt.Contracts/"]
COPY ["ProjectMgmt.Core/ProjectMgmt.Core.csproj", "ProjectMgmt.Core/"]
COPY ["ProjectMgmt.Modules.DeliveryIntelligence/Domain/DeliveryIntelligence.Domain.csproj", "ProjectMgmt.Modules.DeliveryIntelligence/Domain/"]
COPY ["ProjectMgmt.Modules.DeliveryIntelligence/Application/DeliveryIntelligence.Application.csproj", "ProjectMgmt.Modules.DeliveryIntelligence/Application/"]
COPY ["ProjectMgmt.Modules.DeliveryIntelligence/Infrastructure/DeliveryIntelligence.Infrastructure.csproj", "ProjectMgmt.Modules.DeliveryIntelligence/Infrastructure/"]
COPY ["ProjectMgmt.Modules.IdentityExperience/Domain/IdentityExperience.Domain.csproj", "ProjectMgmt.Modules.IdentityExperience/Domain/"]
COPY ["ProjectMgmt.Modules.IdentityExperience/Application/IdentityExperience.Application.csproj", "ProjectMgmt.Modules.IdentityExperience/Application/"]
COPY ["ProjectMgmt.Modules.IdentityExperience/Infrastructure/IdentityExperience.Infrastructure.csproj", "ProjectMgmt.Modules.IdentityExperience/Infrastructure/"]
COPY ["ProjectMgmt.Modules.Planning/Domain/Planning.Domain.csproj", "ProjectMgmt.Modules.Planning/Domain/"]
COPY ["ProjectMgmt.Modules.Planning/Application/Planning.Application.csproj", "ProjectMgmt.Modules.Planning/Application/"]
COPY ["ProjectMgmt.Modules.Planning/Infrastructure/Planning.Infrastructure.csproj", "ProjectMgmt.Modules.Planning/Infrastructure/"]
COPY ["ProjectMgmt.Solution/ProjectMgmt.Solution.csproj", "ProjectMgmt.Solution/"]

RUN dotnet restore "ProjectMgmt.Solution/ProjectMgmt.Solution.csproj"

FROM restore AS publish
COPY . .
RUN dotnet publish "ProjectMgmt.Solution/ProjectMgmt.Solution.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=publish /app/publish .

USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ProjectMgmt.Solution.dll"]
