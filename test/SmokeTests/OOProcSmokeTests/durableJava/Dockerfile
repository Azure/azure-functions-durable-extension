ARG DOTNET_SDK_TAG=10.0
ARG TARGET_FRAMEWORK=net8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build-env
ARG TARGET_FRAMEWORK
COPY . /root
RUN --mount=type=secret,id=nuget_config,required=false \
    cd /root/test/SmokeTests/OOProcSmokeTests/durableJava && \
    if [ -f /run/secrets/nuget_config ]; then \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK --configfile /run/secrets/nuget_config; \
    else \
      dotnet restore -p:TargetFramework=$TARGET_FRAMEWORK; \
    fi && \
    dotnet build -o bin -f $TARGET_FRAMEWORK --no-restore

FROM mcr.microsoft.com/azure-functions/java:4-java8
# Copy the bin folder generated at /root/test/SmokeTests/OOProcSmokeTests/durableJava
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJava /home/site/wwwroot

# Copy the Java azure function package
COPY test/SmokeTests/OOProcSmokeTests/durableJava/build/azure-functions/durableJava/ /home/site/wwwroot/
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
