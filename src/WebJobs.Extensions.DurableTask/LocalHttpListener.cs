// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LocalHttpListener : IDisposable
    {
        private readonly DurableTaskExtension extension;
        private HttpListener httpListener;

        public LocalHttpListener(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
        }

        public bool IsListening => this.httpListener?.IsListening ?? false;

        public void Dispose()
        {
            ((IDisposable)this.httpListener)?.Dispose();
        }

        public void Start(Uri baseUri, Func<HttpRequestMessage, Task<HttpResponseMessage>> onRequest)
        {
            if (onRequest == null)
            {
                throw new ArgumentNullException(nameof(onRequest));
            }

            if (this.IsListening == true)
            {
                throw new InvalidOperationException("The local HTTP listener has already started.");
            }

            if (baseUri.AbsolutePath == "/")
            {
                throw new ArgumentException("Cannot use '/' as the path.", nameof(baseUri));
            }

            if (!baseUri.IsLoopback)
            {
                throw new ArgumentException($"The URI {baseUri} does not represent a loopback address.", nameof(baseUri));
            }

            this.Dispose();

            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(baseUri.GetLeftPart(UriPartial.Path));

            // This can fail if another process is already listening on this endpoint.
            // NOTE: There is a known issue with the Function host restarting where two
            // instances of the listener will start at the same time, causing a conflict.
            // However, it should automatically recover from the exception.
            this.httpListener.Start();

            ThreadPool.QueueUserWorkItem(_ => this.ListenLoop(onRequest));
        }

        public void Stop()
        {
            this.httpListener.Stop();
        }

        private void ListenLoop(Func<HttpRequestMessage, Task<HttpResponseMessage>> onRequestCallback)
        {
            while (this.httpListener.IsListening)
            {
                HttpListenerContext context = this.httpListener.GetContext();
                HttpRequestMessage webApiRequest = ConvertToWebApiRequest(context.Request);
                Task ignored = Task.Run(async () =>
                {
                    try
                    {
                        HttpResponseMessage webApiResponse = await onRequestCallback(webApiRequest);
                        await ApplyWebApiResponse(context, webApiResponse);
                    }
                    catch (HttpListenerException)
                    {
                        // This is expected if the client closes the connection.
                    }
                    catch (Exception e)
                    {
                        this.extension.TraceHelper.ExtensionWarningEvent(
                            this.extension.Options.HubName,
                            functionName: string.Empty,
                            instanceId: string.Empty,
                            message: $"Unhandled exception in HTTP API handler: {e}");
                        context.Response.StatusCode = 500;
                    }
                    finally
                    {
                        try
                        {
                            context.Response.Close();
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                });
            }
        }

        private static HttpRequestMessage ConvertToWebApiRequest(HttpListenerRequest listenerRequest)
        {
            var webApiRequest = new HttpRequestMessage
            {
                Method = new HttpMethod(listenerRequest.HttpMethod),
                RequestUri = listenerRequest.Url,
            };

            foreach (string key in listenerRequest.Headers.AllKeys)
            {
                string value = listenerRequest.Headers.Get(key);
                webApiRequest.Headers.TryAddWithoutValidation(key, value);
            }

            if (listenerRequest.HasEntityBody)
            {
                webApiRequest.Content = new StreamContent(listenerRequest.InputStream);
            }

            return webApiRequest;
        }

        private static async Task ApplyWebApiResponse(HttpListenerContext context, HttpResponseMessage webApiResponse)
        {
            context.Response.StatusCode = (int)webApiResponse.StatusCode;
            AddResponseHeaders(webApiResponse.Headers, context.Response);

            if (webApiResponse.Content != null)
            {
                AddResponseHeaders(webApiResponse.Content.Headers, context.Response);
                await webApiResponse.Content.CopyToAsync(context.Response.OutputStream);
            }
        }

        private static void AddResponseHeaders(HttpHeaders headers, HttpListenerResponse response)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            {
                foreach (string headerValue in header.Value)
                {
                    response.AddHeader(header.Key, headerValue);
                }
            }
        }
    }
}
