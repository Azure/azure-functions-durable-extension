using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;

namespace WebJobs.Extensions.DurableTask.Tests
{
    internal static class HttpTestUtility
    {
        internal static async Task<string> GetResponseBody(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            StreamReader reader = new StreamReader(response.Body);
            var bodyContent = await reader.ReadToEndAsync();
            return bodyContent;
        }

        internal static DefaultHttpRequest GetSampleHttpRequest()
        {
            DefaultHttpRequest defaultHttpRequest = new DefaultHttpRequest(new DefaultHttpContext());
            defaultHttpRequest.Scheme = TestConstants.RequestUriScheme;
            defaultHttpRequest.Host = new HostString(TestConstants.RequestUriHost);
            defaultHttpRequest.Path = new PathString(TestConstants.RequestUriPath);
            return defaultHttpRequest;
        }
    }
}
