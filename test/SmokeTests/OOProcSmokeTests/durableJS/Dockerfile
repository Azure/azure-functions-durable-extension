# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
COPY . /root
WORKDIR /root

# Build the extension project using the correct relative path
RUN dotnet build test/SmokeTests/OOProcSmokeTests/durableJS/../../../../src/WebJobs.Extensions.DurableTask/WebJobs.Extensions.DurableTask.csproj -c Release -o /root/extension

# Build the extensions.csproj to generate extension metadata
WORKDIR /root/test/SmokeTests/OOProcSmokeTests/durableJS
RUN dotnet build extensions.csproj -c Release -o bin

# Step 2: Deploy and run npm install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/node:4
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJS /home/site/wwwroot
COPY --from=build-env /root/extension /home/site/wwwroot/bin

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    AzureFunctionsJobHost__Logging__LogLevel__Default=Debug \
    AzureFunctionsJobHost__Logging__LogLevel__Host__Default=Debug

# Install npm packages
WORKDIR /home/site/wwwroot
RUN npm install