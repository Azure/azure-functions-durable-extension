# ASP.NET Core API To Do List Sample with Identity-Based Connection

This example is adapted from the [To Do List sample](https://github.com/Azure-Samples/dotnet-core-api) in the Azure-Samples repository. It demonstrates an ASP.NET Core application with an injected Durable Client and identity-based connections. In this sample, the Durable Client is configured to use a storage connection with a custom name, `MyStorage`, and is set up to utilize a client secret for authentication.


## To make the sample run, you need to:

1. Create an identity for your Function App in the Azure portal.

2. Grant the following Role-Based Access Control (RBAC) permissions to the identity:
    - Storage Queue Data Contributor
    - Storage Blob Data Contributor
    - Storage Table Data Contributor

3. Link your storage account to your Function App by adding either of these two details to your configuration, which is appsettings.json file in this sample .
    - accountName
    - blobServiceUri, queueServiceUri and tableServiceUri

4. Add the required identity information to your Functions App configuration, which is appsettings.json file in this sample.
    - system-assigned identity: nothing needs to be provided.
    - user-assigned identity: 
      - credential: managedidentity
      - clientId
    - client secret application:
      - clientId
      - ClientSecret
      - tenantId


## Notes
- The storage connection information must be provided in the format specified in the appsettings.json file.
- If your storage information is saved in a custom-named JSON file, be sure to add it to your configuration as shown below.
```csharp            
this.Configuration = new ConfigurationBuilder()
                        .AddJsonFile("myjson.json")
                        .Build();
```