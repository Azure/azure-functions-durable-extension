# CSX samples

This project contains samples using the C# scripting support. This is the same C# variant used by the Azure Portal development experience.

## Building the sample

Use the following command to install the extensions used by this sample app. Note that the `--csx` flag is required for .csx projects:

```bash
func extensions install --csx
```

## Running the sample locally

After a successful build, use the following command to start the function app. Note that it requires a `--no-build` flag.

```bash
func host start --no-build
```

## Running an orchestration

Use the following cURL command to start a new orchestration:

```bash
curl -i -X POST http://localhost:7071/orchestrators/E1_HelloSequence -H "Content-Length: 0"
```

The response should contain a JSON payload with several URLs. The following is an example of what the response may look like (formatted for readability):

```json
{
  "id": "36a1d82fb9064275b1df810b5962d4e0",
  "statusQueryGetUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/36a1d82fb9064275b1df810b5962d4e0?taskHub=SampleHubCsx&connection=Storage&code=1sGdXsmh9c1Yglp9ihYRzqRwx7cbbhrfdig2qKAd9v9Ju1gaacUuFg==",
  "sendEventPostUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/36a1d82fb9064275b1df810b5962d4e0/raiseEvent/{eventName}?taskHub=SampleHubCsx&connection=Storage&code=1sGdXsmh9c1Yglp9ihYRzqRwx7cbbhrfdig2qKAd9v9Ju1gaacUuFg==",
  "terminatePostUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/36a1d82fb9064275b1df810b5962d4e0/terminate?reason={text}&taskHub=SampleHubCsx&connection=Storage&code=1sGdXsmh9c1Yglp9ihYRzqRwx7cbbhrfdig2qKAd9v9Ju1gaacUuFg==",
  "purgeHistoryDeleteUri": "http://localhost:7071/runtime/webhooks/durabletask/instances/36a1d82fb9064275b1df810b5962d4e0?taskHub=SampleHubCsx&connection=Storage&code=1sGdXsmh9c1Yglp9ihYRzqRwx7cbbhrfdig2qKAd9v9Ju1gaacUuFg=="
}
```

The response should also contain a `Location` header. The value of this header is a URL, which is the same URL as the `statusQueryGetUri`. Use curl again to query this URL to get the orchestration status. The actual URL will be different for every orchestration instance. The following example shows what this will look like:

```bash
curl -i "http://localhost:7071/runtime/webhooks/durabletask/instances/36a1d82fb9064275b1df810b5962d4e0?taskHub=SampleHubCsx&connection=Storage&code=1sGdXsmh9c1Yglp9ihYRzqRwx7cbbhrfdig2qKAd9v9Ju1gaacUuFg=="
```

The JSON response will look something like the following (formatted for readability):

```JSON
{
  "name": "E1_HelloSequence",
  "instanceId": "36a1d82fb9064275b1df810b5962d4e0",
  "runtimeStatus": "Completed",
  "input": null,
  "customStatus": null,
  "output": [
    "Hello Tokyo!",
    "Hello Seattle!",
    "Hello London!"
  ],
  "createdTime": "2019-12-18T19:02:42Z",
  "lastUpdatedTime": "2019-12-18T19:02:42Z"
}
```
