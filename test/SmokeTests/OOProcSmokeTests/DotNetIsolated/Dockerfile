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
#         IMPORTANT: restore seems to need to be done separately to ensure the correct
#         nuget package bits get published. Not sure why, but without this, we've observed
#         that the wrong .NET target is used, resulting in app startup failures. It may
#         be an issue with the Azure Functions Worker SDK build process.
RUN cd /root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated && \
    mkdir -p /home/site/wwwroot && \
    dotnet restore --verbosity normal && \
    dotnet build -c Release && \
    dotnet publish -c Release --no-build --output /home/site/wwwroot && \
    ls -aR /home/site/wwwroot && \
    cat /home/site/wwwroot/extensions.json # debugging

# Step 3: Generate the final app image to run
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated6.0

# This is the standard setup for Azure Functions running in Docker containers
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
