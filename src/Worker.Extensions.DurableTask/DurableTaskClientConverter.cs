// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed partial class DurableTaskClientConverter : IInputConverter
{
    private readonly FunctionsDurableClientProvider clientProvider;

    // Constructor parameters are optional DI-injected services.
    public DurableTaskClientConverter(FunctionsDurableClientProvider clientProvider)
    {
        this.clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
    }

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.TargetType != typeof(DurableTaskClient))
        {
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        // The exact format of the expected JSON string data is controlled by the Durable Task WebJobs client binding logic.
        // It's never expected to be wrong, but we code defensively just in case.
        if (context.Source is not string clientConfigText)
        {
            return new ValueTask<ConversionResult>(ConversionResult.Failed(new InvalidOperationException(
                $"Expected the Durable Task WebJobs SDK extension to send a string payload for {nameof(DurableClientAttribute)}.")));
        }

        try
        {
            DurableClientInputData? inputData = JsonSerializer.Deserialize<DurableClientInputData>(clientConfigText);
            if (!Uri.TryCreate(inputData?.rpcBaseUrl, UriKind.Absolute, out Uri? endpoint))
            {
                return new ValueTask<ConversionResult>(ConversionResult.Failed(
                    new InvalidOperationException("Failed to parse the input binding payload data")));
            }

            // Deserialize the gRPC HTTP client timeout from inputData. If the value is null or missing, default to 100 seconds.
            TimeSpan grpcHttpClientTimeout = inputData?.grpcHttpClientTimeout != null
                                                ? JsonSerializer.Deserialize<TimeSpan>(inputData.grpcHttpClientTimeout) : TimeSpan.FromSeconds(100);

            DurableTaskClient client = this.clientProvider.GetClient(endpoint, inputData?.taskHubName, inputData?.connectionName, inputData?.maxGrpcMessageSizeInBytes, grpcHttpClientTimeout);
            client = new FunctionsDurableTaskClient(client, inputData!.requiredQueryStringParameters, inputData!.httpBaseUrl);
            return new ValueTask<ConversionResult>(ConversionResult.Success(client));
        }
        catch (Exception innerException)
        {
            InvalidOperationException exception = new(
                $"Failed to convert the input binding context data into a {nameof(DurableTaskClient)} object. The data may have been delivered in an invalid format.",
                innerException);
            return new ValueTask<ConversionResult>(ConversionResult.Failed(exception));
        }
    }

    // Serializer is case-sensitive and incoming JSON properties are camel-cased.
    private record DurableClientInputData(
        string rpcBaseUrl,
        string taskHubName,
        string connectionName,
        string requiredQueryStringParameters,
        string httpBaseUrl,
        int maxGrpcMessageSizeInBytes,
        string grpcHttpClientTimeout);
}
