using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class HttpRequestMessageExtensionsTests
    {
        [Fact]
        public void GetQueryNameValuePairs_Returns_All_Query_Parameters()
        {
            var request = new HttpRequestMessage
            {
                RequestUri =
                    new Uri("http://localhost:7071/orchestrators/E1_HelloSequence/wait?timeout=10&retryInterval=1")
            };
            var pairs = request.GetQueryNameValuePairs();
            Assert.Equal(pairs.Count, 2);
            Assert.Equal(pairs["timeout"], "10");
            Assert.Equal(pairs["retryInterval"], "1");
        }

        [Fact]
        public void GetQueryParameters_Returns_Values_For_Both_Parameters()
        {
            var request = new HttpRequestMessage
            {
                RequestUri =
                    new Uri("http://localhost:7071/orchestrators/E1_HelloSequence/wait?timeout=10&retryInterval=1")
            };
            request.GetQueryParameters("timeout", out var timeout, "retryInterval", out var retryInterval);
            Assert.Equal(timeout, "10");
            Assert.Equal(retryInterval, "1");
        }

        [Fact]
        public void GetQueryParameters_Returns_Values_For_One_Parameter()
        {
            var request = new HttpRequestMessage
            {
                RequestUri =
                    new Uri("http://localhost:7071/orchestrators/E1_HelloSequence/wait?timeout=10&retryInterval=1")
            };
            request.GetQueryParameters("timeout", out var timeout, "retry", out var retry);
            Assert.Equal(timeout, "10");
            Assert.Equal(retry, null);
        }
    }
}
