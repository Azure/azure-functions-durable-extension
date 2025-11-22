# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

ARG DOTNET_VERSION=8
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}.0 AS build-env

# Build the app
COPY . /root
RUN rm -f /root/global.json && \
    cd /root/test/SmokeTests/BackendSmokeTests/Netherite && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish -c Release --output /home/site/wwwroot

# Deploy the app
ARG DOTNET_VERSION=8
FROM mcr.microsoft.com/azure-functions/dotnet:4-dotnet${DOTNET_VERSION}
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
