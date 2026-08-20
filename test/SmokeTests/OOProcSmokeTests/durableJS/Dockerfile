# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
#         This is an alternative to "func extensions install"
ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK
COPY . /root
RUN --mount=type=secret,id=nuget_config,required=false \
    cd /root/test/SmokeTests/OOProcSmokeTests/durableJS && \
    if [ -f /run/secrets/nuget_config ]; then \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK --configfile /run/secrets/nuget_config; \
    else \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK; \
    fi && \
    dotnet build -o bin -f $TARGET_FRAMEWORK --no-restore

# Step 2: Deploy and run npm install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/node:4-node20
ARG NPM_REGISTRY=https://pkgs.dev.azure.com/azfunc/public/_packaging/upstream-public/npm/registry/
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJS /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
RUN --mount=type=secret,id=npmrc,target=/root/.npmrc,required=false \
    cd /home/site/wwwroot && \
    npm install --registry="$NPM_REGISTRY" \
      --@azure:registry="$NPM_REGISTRY" \
      --@opentelemetry:registry="$NPM_REGISTRY"