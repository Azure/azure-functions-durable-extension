FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durableJava && \
    dotnet build -o bin

FROM mcr.microsoft.com/azure-functions/java:4-java8
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJava /home/site/wwwroot

RUN rm /azure-functions-host/workers/java/azure-functions-java-worker.jar
COPY test/SmokeTests/OOProcSmokeTests/durableJava/azure-functions-java-worker-2.2.3.jar /azure-functions-host/workers/java/azure-functions-java-worker.jar
COPY test/SmokeTests/OOProcSmokeTests/durableJava/ /home/site/wwwroot/
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
