# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
#         This is an alternative to "func extensions install"
ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durableJS && \
    dotnet build -o bin -f $TARGET_FRAMEWORK

# Step 2: Deploy and run npm install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/node:4-node20
ARG NPM_REGISTRY=https://pkgs.dev.azure.com/azfunc/public/_packaging/upstream-public/npm/registry/
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJS /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
RUN cd /home/site/wwwroot && \
    npm install --registry="$NPM_REGISTRY" \
      --@azure:registry="$NPM_REGISTRY" \
      --@opentelemetry:registry="$NPM_REGISTRY"