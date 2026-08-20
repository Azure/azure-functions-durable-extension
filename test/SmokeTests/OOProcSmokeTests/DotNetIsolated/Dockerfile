# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# NOTE: Always use .NET 10 SDK because global.json requires SDK 10.0.102.
# The SDK can still build for net8.0 target framework.
ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
ARG DOTNET_ISOLATED_TAG=4-dotnet-isolated8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK

# Step 1: Build the WebJobs extension for the specific TFM and publish it as a
#         local NuGet package. We build a single TFM (-f $TARGET_FRAMEWORK) to
#         avoid output collisions that occur when multi-target builds use --output.
#         GeneratePackageOnBuild=true in the csproj produces the nupkg automatically.
COPY . /root
RUN cd /root/src/WebJobs.Extensions.DurableTask && \
    mkdir -p /packages && \
    dotnet restore "WebJobs.Extensions.DurableTask.csproj" && \
    dotnet build -c Release -f "$TARGET_FRAMEWORK" "WebJobs.Extensions.DurableTask.csproj" --no-restore && \
    dotnet nuget push "bin/Release/Microsoft.Azure.WebJobs.Extensions.DurableTask.*.nupkg" --source "/packages" && \
    cp "/root/nuget.config" "/root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated/NuGet.config" && \
    dotnet nuget add source "/packages" --name "local-packages" --configfile "/root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated/NuGet.config" && \
    sed -i '/<\/packageSourceMapping>/i\    <packageSource key="local-packages">\n      <package pattern="Microsoft.Azure.WebJobs.Extensions.DurableTask" />\n    </packageSource>' "/root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated/NuGet.config"

# Step 2: Build the sample app, which references the locally built extension from Step 1
#         IMPORTANT: restore seems to need to be done separately to ensure the correct
#         nuget package bits get published. Not sure why, but without this, we've observed
#         that the wrong .NET target is used, resulting in app startup failures. It may
#         be an issue with the Azure Functions Worker SDK build process.
RUN cd /root/test/SmokeTests/OOProcSmokeTests/DotNetIsolated && \
    mkdir -p /home/site/wwwroot && \
    dotnet restore "DotNetIsolated.csproj" --configfile "NuGet.config" --verbosity normal -p:TargetFramework="$TARGET_FRAMEWORK" && \
    dotnet build "DotNetIsolated.csproj" -c Release -f "$TARGET_FRAMEWORK" --no-restore && \
    dotnet publish "DotNetIsolated.csproj" -c Release -f "$TARGET_FRAMEWORK" --no-build --no-restore --output "/home/site/wwwroot" && \
    cp "/root/src/WebJobs.Extensions.DurableTask/bin/Release/$TARGET_FRAMEWORK/Microsoft.Azure.WebJobs.Extensions.DurableTask.dll" "/home/site/wwwroot/.azurefunctions/" && \
    ls -aR /home/site/wwwroot && \
    cat /home/site/wwwroot/extensions.json # debugging

# Step 3: Generate the final app image to run
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:${DOTNET_ISOLATED_TAG}

# This is the standard setup for Azure Functions running in Docker containers
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build-env ["/home/site/wwwroot", "/home/site/wwwroot"]
