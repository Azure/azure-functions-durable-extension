// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal sealed class TaskHubGrpcExceptionInterceptor : Interceptor
    {
        private readonly DurableTaskExtension extension;

        public TaskHubGrpcExceptionInterceptor(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (Exception exception) when (ShouldLog(exception, context))
            {
                this.LogException(context, exception);
                throw;
            }
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await continuation(request, responseStream, context);
            }
            catch (Exception exception) when (ShouldLog(exception, context))
            {
                this.LogException(context, exception);
                throw;
            }
        }

        private static bool ShouldLog(Exception exception, ServerCallContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (exception is TaskHubRpcException)
            {
                return true;
            }

            return exception is not RpcException;
        }

        private void LogException(ServerCallContext context, Exception exception)
        {
            Exception exceptionToLog = exception is TaskHubRpcException rpcException
                ? rpcException.Cause
                : exception;

            this.extension.TraceHelper.ExtensionWarningEvent(
                this.extension.Options.HubName,
                instanceId: string.Empty,
                functionName: string.Empty,
                message: $"Unhandled exception in local gRPC call '{context.Method}': {exceptionToLog}");
        }
    }
}
