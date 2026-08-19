# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK

# Build the DF MSSQL app
COPY . /root
RUN --mount=type=secret,id=nuget_config,required=false \
    cd /root/test/SmokeTests/BackendSmokeTests/MSSQL && \
    mkdir -p /home/site/wwwroot && \
    if [ -f /run/secrets/nuget_config ]; then \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK --configfile /run/secrets/nuget_config; \
    else \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK; \
    fi && \
    dotnet publish -c Release -f $TARGET_FRAMEWORK --no-restore --output /home/site/wwwroot

# Deploy the app
FROM mcr.microsoft.com/azure-functions/dotnet:4-dotnet8
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
