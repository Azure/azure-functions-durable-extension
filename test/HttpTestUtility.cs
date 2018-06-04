using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Newtonsoft.Json;

namespace WebJobs.Extensions.DurableTask.Tests
{
    internal static class HttpTestUtility
    {
        internal static async Task<HttpResponse> GetSimpleResponse(HttpRequest request)
        {
            return await GetSimpleResponsebyStatusCodeAndContent(request);
        }

        internal static async Task<HttpResponse> GetSimpleErrorResponse(HttpRequest request)
        {
            return await GetSimpleResponsebyStatusCodeAndContent(request, 500, new { message = "Exception has been thrown" });
        }

        private static async Task<HttpResponse> GetSimpleResponsebyStatusCodeAndContent(HttpRequest request, int statusCode = 200, object content = null)
        {
            DefaultHttpResponse httpResponse = new DefaultHttpResponse(request.HttpContext)
            {
                StatusCode = statusCode,
            };
            await httpResponse.WriteAsync(JsonConvert.SerializeObject(content ?? new {message = "OK!"}));
            return httpResponse;
        }

        internal static Task<string> GetRequestBody(HttpRequest request)
        {
            Task<string> stringDataTask;
            using (StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                stringDataTask = reader.ReadToEndAsync();
            }
            return stringDataTask;
        }

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
