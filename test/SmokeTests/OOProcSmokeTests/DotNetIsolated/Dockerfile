# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

# Step 1: Build the WebJobs extension and publish it as a local NuGet package
COPY . /root
RUN cd /root/src/WebJobs.Extensions.DurableTask && \
    mkdir /out && \
    dotnet build -c Release WebJobs.Extensions.DurableTask.csproj --output /out && \
    mkdir /packages && \
    dotnet nuget push /out/Microsoft.Azure.WebJobs.Extensions.DurableTask.*.nupkg --source /packages && \
    dotnet nuget add source /packages

# Step 2: Build the sample app, which references the locally built extension from Step 1
RUN cd /root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated && \
    mkdir -p /home/site/wwwroot && \
    dotnet build -c Release && \
    dotnet build -c Release && \
    dotnet publish -c Release --no-build --output /home/site/wwwroot && \
    ls -R /home/site/wwwroot && \
    cat /home/site/wwwroot/extensions.json # debugging

# Step 3: Generate the final app image to run
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated6.0

# Workaround for https://github.com/Azure/azure-functions-docker/issues/600
RUN apt-get update -y \
  && apt-get install -y apt-transport-https wget \
  && wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
  && dpkg -i packages-microsoft-prod.deb \
  && apt-get update -y \
  && apt-get install -y aspnetcore-runtime-6.0

# This is the standard setup for Azure Functions running in Docker containers
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
