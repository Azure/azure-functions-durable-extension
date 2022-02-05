# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
#         This is an alternative to "func extensions install"
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durableJS && \
    dotnet build -o bin

# Step 2: Deploy and run npm install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/node:4
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJS /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
RUN cd /home/site/wwwroot && \
    npm install