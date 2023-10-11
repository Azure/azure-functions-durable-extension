# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

# Build the app
COPY . /root
RUN cd /root/test/SmokeTests/BackendSmokeTests/Netherite && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish -c Release --output /home/site/wwwroot

# Deploy the app
# TODO: For some reason, the 'floating tag' `dotnet:4.0` does not contain
# the latest Host. Our Netherite tests will fail without an up to date host.
# Therefore, we're pinning to a more up to date tag than 4.0, but we should
# eventually move to a 'floating tag' again.
FROM mcr.microsoft.com/azure-functions/dotnet:4.22.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
