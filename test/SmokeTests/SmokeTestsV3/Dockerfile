# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env

# Build the app
COPY . /root
RUN cd /root/test/SmokeTests/SmokeTestsV3 && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish -c Release --output /home/site/wwwroot

# Deploy the app
FROM mcr.microsoft.com/azure-functions/dotnet:3.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
