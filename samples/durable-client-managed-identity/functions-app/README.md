# Azure Function App with Durable Function and Identity-Based Connection

This project demonstrates an Azure Function App that invokes a Durable Function through a Durable Client using dependency injection and identity-based connection. In the sample, the function is set up to utilize a storage connection named `Storage` by default. Meanwhile, the integrated Durable Client is set to use a storage connection that is specifically named `ClientStorage`.


## To make the sample run, you need to:

1. Create an identity for your Function App in the Azure portal.

2. Grant the following Role-Based Access Control (RBAC) permissions to the identity:
    - Storage Queue Data Contributor
    - Storage Blob Data Contributor
    - Storage Table Data Contributor

3. Link your storage account to your Function App by adding either of these two details to your `local.settings.json` file (for local development) or as environment variables in your Function App settings in Azure.
    - \<StorageConnectionName\>__accountName
    - \<StorageConnectionName\>__blobServiceUri, \<StorageConnectionName\>__queueServiceUri and \<StorageConnectionName\>__tableServiceUri

4. Add the required identity information to your Functions App configuration.
    - system-assigned identity: nothing needs to be provided.
    - user-assigned identity: 
      - \<StorageConnectionName\>__credential: managedidentity
      - \<StorageConnectionName\>__clientId
    - client secret application:
      - \<StorageConnectionName\>__clientId
      - \<StorageConnectionName\>__ClientSecret
      - \<StorageConnectionName\>__tenantId


## Notes

- The Azure Functions runtime requires a storage account to start, with the default connection name `Storage`.
- The Durable Client injected also requires a storage account, with the same default connection name `Storage`. However, you can use a custom connection name for a separate storage account as runtime for the durable client. For example, in this sample we use custom name `ClientStorage`.
- To provide the necessary connection information, use the format `<StorageConnectionName>__<SettingName>`, as shown in local.settings.json. For example, if you want to specify the accountName, then add the setting `<StorageConnectionName>__accountName`.
