# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
#         This is an alternative to "func extensions install"
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durablePy && \
    dotnet build -o bin

# Step 2: Deploy and run pip install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/python:4-python3.7
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durablePy /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

RUN cd /home/site/wwwroot && \
    pip install -r requirements.txt