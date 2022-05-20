// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Grpc;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Concreate implementation of the <see cref="DurableClientContext"/> abstract class that
/// allows callers to start and manage orchestration instances.
/// </summary>
internal sealed class DefaultDurableClientContext : DurableClientContext
{
    private readonly DurableClientInputData inputData;

    // Private constructor that's called by the converter inner-class
    private DefaultDurableClientContext(DurableTaskClient client, DurableClientInputData inputData)
    {
        this.Client = client ?? throw new ArgumentNullException(nameof(client));
        this.inputData = inputData ?? throw new ArgumentNullException(nameof(inputData));

        if (string.IsNullOrEmpty(inputData.taskHubName))
        {
            throw new ArgumentNullException(nameof(inputData.taskHubName));
        }

        if (string.IsNullOrEmpty(inputData.requiredQueryStringParameters))
        {
            throw new ArgumentNullException(nameof(inputData.requiredQueryStringParameters));
        }
    }

    /// <inheritdoc/>
    public override DurableTaskClient Client { get; }

    /// <inheritdoc/>
    public override string TaskHubName => this.inputData.taskHubName;

    /// <inheritdoc/>
    public override HttpResponseData CreateCheckStatusResponse(HttpRequestData request, string instanceId, bool returnInternalServerErrorOnFailure = false)
    {
        // TODO: To better support scenarios involving proxies or application gateways, this
        //       code should take the X-Forwarded-Host, X-Forwarded-Proto, and Forwarded HTTP
        //       request headers into consideration and generate the base URL accordingly.
        //       More info: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded.
        //       One potential workaround is to set ASPNETCORE_FORWARDEDHEADERS_ENABLED to true.
        string baseUrl = request.Url.GetLeftPart(UriPartial.Authority);
        string formattedInstanceId = Uri.EscapeDataString(instanceId);
        string instanceUrl = $"{baseUrl}/runtime/webhooks/durabletask/instances/{formattedInstanceId}";
        string commonQueryParameters = this.inputData.requiredQueryStringParameters;

        HttpResponseData response = request.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"{instanceUrl}?{commonQueryParameters}");
        response.Headers.Add("Content-Type", "application/json");

        response.WriteBytes(JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = instanceId,
            purgeHistoryDeleteUri = $"{instanceUrl}?{commonQueryParameters}",
            sendEventPostUri = $"{instanceUrl}/raiseEvent/{{eventName}}?{commonQueryParameters}",
            statusQueryGetUri = $"{instanceUrl}?{commonQueryParameters}",
            terminatePostUri = $"{instanceUrl}/terminate?reason={{text}}&{commonQueryParameters}",
        }));

        return response;
    }

    /// <summary>
    /// Input converter implementation for the Durable Client binding (i.e. functions with a <see cref="DurableClientAttribute"/>-decorated parameter)
    /// that translates an input JSON blob into an <see cref="DurableClientContext"/> object.
    /// </summary>
    internal class Converter : IInputConverter
    {
        private readonly IServiceProvider? serviceProvider;

        // Constructor parameters are optional DI-injected services.
        public Converter(IServiceProvider? serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public ValueTask<ConversionResult> ConvertAsync(ConverterContext converterContext)
        {
            // The exact format of the expected JSON string data is controlled by the Durable Task WebJobs client binding logic.
            // It's never expected to be wrong, but we code defensively just in case.
            if (converterContext.Source is not string clientConfigText)
            {
                return new ValueTask<ConversionResult>(ConversionResult.Failed(new InvalidOperationException($"Expected the Durable Task WebJobs SDK extension to send a string payload for {nameof(DurableClientAttribute)}.")));
            }

            DurableClientInputData? inputData = JsonSerializer.Deserialize<DurableClientInputData>(clientConfigText);
            if (string.IsNullOrEmpty(inputData?.rpcBaseUrl))
            {
                InvalidOperationException exception = new("Failed to parse the input binding payload data");
                return new ValueTask<ConversionResult>(ConversionResult.Failed(exception));
            }

            try
            {
                DurableTaskGrpcClient.Builder builder = DurableTaskGrpcClient.CreateBuilder();
                if (!Uri.TryCreate(inputData.rpcBaseUrl, UriKind.Absolute, out Uri? baseUriResult))
                {
                    InvalidOperationException exception = new($"Failed to parse the {nameof(inputData.rpcBaseUrl)} field from the input binding data.");
                    return new ValueTask<ConversionResult>(ConversionResult.Failed(exception));
                }

                builder.UseAddress(baseUriResult.Host, baseUriResult.Port);
                if (this.serviceProvider != null)
                {
                    // The builder will use the host's service provider to look up DI-registered
                    // services, like IDataConverter, ILoggerFactory, IConfiguration, etc. This allows
                    // it to use the default host services and also allows it to use services injected
                    // by the application owner (e.g. IDataConverter for custom data conversion).
                    builder.UseServices(this.serviceProvider);
                }

                DefaultDurableClientContext clientContext = new(builder.Build(), inputData);
                return new ValueTask<ConversionResult>(ConversionResult.Success(clientContext));
            }
            catch (Exception innerException)
            {
                InvalidOperationException exception = new($"Failed to convert the input binding context data into a {nameof(DefaultDurableClientContext)} object. The data may have been delivered in an invalid format.", innerException);
                return new ValueTask<ConversionResult>(ConversionResult.Failed(exception));
            }
        }
    }

    // Serializer is case-sensitive and incoming JSON properties are camel-cased.
    private record DurableClientInputData(string rpcBaseUrl, string taskHubName, string requiredQueryStringParameters);
}
