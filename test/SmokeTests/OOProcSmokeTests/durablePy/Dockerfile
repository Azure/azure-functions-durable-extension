# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Step 1: Add the durable extension by building it locally.
#         This is an alternative to "func extensions install"
ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durablePy && \
    dotnet build -o bin -f $TARGET_FRAMEWORK

# Step 2: Deploy and run pip install to get the Durable Functions SDK
FROM mcr.microsoft.com/azure-functions/python:4-python3.11
ARG PIP_INDEX_URL=https://pypi.org/simple/
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durablePy /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

RUN cd /home/site/wwwroot && \
    PIP_INDEX_URL="$PIP_INDEX_URL" pip install --requirement requirements.txt