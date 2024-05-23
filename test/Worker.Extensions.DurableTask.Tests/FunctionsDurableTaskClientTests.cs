using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FunctionsDurableTaskClient />.
    /// </summary>
    public class FunctionsDurableTaskClientTests
    {
        private FunctionsDurableTaskClient GetTestFunctionsDurableTaskClient()
        {
            // construct mock client

            // The DurableTaskClient demands a string parameter in it's constructor, so we pass it in
            string clientName = string.Empty;
            Mock<DurableTaskClient> durableClientMock = new(clientName);

            Task completedTask = Task.CompletedTask;
            durableClientMock.Setup(x => x.TerminateInstanceAsync(It.IsAny<string>(), It.IsAny<TerminateInstanceOptions>(), It.IsAny<CancellationToken>())).Returns(completedTask);

            DurableTaskClient durableClient = durableClientMock.Object;
            FunctionsDurableTaskClient client = new FunctionsDurableTaskClient(durableClient, queryString: null);
            return client;
        }

        /// <summary>
        /// Test that the `TerminateInstnaceAsync` can be invoked without exceptions.
        /// Exceptions are a risk since we inherit from an abstract class where default implementations are not provided.
        /// </summary>
        [Fact]
        public async void TerminateDoesNotThrow()
        {
            FunctionsDurableTaskClient client = GetTestFunctionsDurableTaskClient();

            string instanceId = string.Empty;
            object output = string.Empty;
            TerminateInstanceOptions options = new TerminateInstanceOptions();
            CancellationToken token = CancellationToken.None;

            // call terminate API with every possible parameter combination
            // if we don't encounter any unimplemented exceptions from the abstract class,
            // then the test passes

            await client.TerminateInstanceAsync(instanceId, token);

            await client.TerminateInstanceAsync(instanceId, output);
            await client.TerminateInstanceAsync(instanceId, output, token);

            await client.TerminateInstanceAsync(instanceId);
            await client.TerminateInstanceAsync(instanceId, options);
            await client.TerminateInstanceAsync(instanceId, options, token);
        }
    }
}