FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
COPY . /root
RUN cd /root/test/SmokeTests/OOProcSmokeTests/durableJava && \
    dotnet build -o bin

FROM mcr.microsoft.com/azure-functions/java:4-java8
# Copy the bin folder generated at /root/test/SmokeTests/OOProcSmokeTests/durableJava
COPY --from=build-env /root/test/SmokeTests/OOProcSmokeTests/durableJava /home/site/wwwroot

# Copy the Java azure function package
COPY test/SmokeTests/OOProcSmokeTests/durableJava/build/azure-functions/durableJava/ /home/site/wwwroot/
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
