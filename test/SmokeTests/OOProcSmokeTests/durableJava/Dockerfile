FROM mcr.microsoft.com/azure-functions/java:4-java11

RUN rm /azure-functions-host/workers/java/azure-functions-java-worker.jar
COPY test/SmokeTests/OOProcSmokeTests/durableJava/azure-functions-java-worker-2.2.3.jar /azure-functions-host/workers/java/azure-functions-java-worker.jar
COPY test/SmokeTests/OOProcSmokeTests/durableJava/ /home/site/wwwroot/
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
