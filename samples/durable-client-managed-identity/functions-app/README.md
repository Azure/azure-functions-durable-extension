# Azure Function App with Durable Function and Identity-Based Connection

This project demonstrates an Azure Function App that invokes a Durable Function through a Durable Client using dependency injection and identity-based connection. Both the function host and the injected Durable Client are configured to use a storage connection with the default name `Storage`.

## To make the sample run, you need to:

1. Create an identity for your Function App in the Azure portal.

2. Grant the following Role-Based Access Control (RBAC) permissions to the identity:
    - Storage Queue Data Contributor
    - Storage Blob Data Contributor
    - Storage Table Data Contributor

3. Link your storage account to your Function App by adding either of these two details to your `local.settings.json` file (for local development) or as environment variables in your Function App settings in Azure.
    - AzureWebJobsStorage__accountName
    - AzureWebJobsStorage__blobServiceUri, AzureWebJobsStorage__queueServiceUri and AzureWebJobsStorage__tableServiceUri

4. Add the required identity information to your Functions App configuration.
    - system-assigned identity: nothing needs to be provided.
    - user-assigned identity: 
      - AzureWebJobsStorage__credential: managedidentity
      - AzureWebJobsStorage__clientId
    - client secret application:
      - AzureWebJobsStorage__clientId
      - AzureWebJobsStorage__ClientSecret
      - AzureWebJobsStorage__tenantId


## Notes

- The Azure Functions runtime requires a storage account to start, with the default connection name `Storage`.
- For Durable Functions initiated by the Durable Client to run successfully, both the Durable Client and the Functions runtime must use the same storage account. If you need to use a custom storage connection name for the Durable Client, ensure you also update the Azure Functions runtime to use this custom name.

