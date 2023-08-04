using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DFPerfScenarios
{
    public static class MapReduceTripData
    {
        private static readonly RetryOptions ActivityRetryPolicy = new RetryOptions(TimeSpan.FromSeconds(5.0), 10)
        {
            BackoffCoefficient = 2.0,
            MaxRetryInterval = TimeSpan.FromMinutes(1.0),
        };

        [FunctionName("MapReduceOrchestration")]
        public static async Task<Aggregates> MapReduceOrchestration([OrchestrationTrigger] IDurableOrchestrationContext ctx, ILogger log)
        {
            string[] dataFiles = await ctx.CallActivityAsync<string[]>("GetDataFiles", 5);
            if (dataFiles == null || dataFiles.Length == 0)
            {
                return new Aggregates();
            }

            List<Task<Aggregates>> list = new List<Task<Aggregates>>(dataFiles.Length);
            string[] array = dataFiles;
            foreach (string text in array)
            {
                list.Add(ctx.CallActivityWithRetryAsync<Aggregates>("ProcessTripDataFile", ActivityRetryPolicy, text));
            }
            int aggregateCount = 0;
            double aggregateSum = 0.0;
            double aggregateAverage2 = 0.0;
            Aggregates[] array2 = await Task.WhenAll(list);
            foreach (Aggregates aggregates in array2)
            {
                aggregateCount += aggregates.Count;
                aggregateSum += aggregates.Sum;
                aggregateAverage2 += aggregates.Average;
            }
            aggregateAverage2 /= (double)dataFiles.Length;
            log.LogWarning($"Results are in! Total ride count: {aggregateCount:N0}, total fees: ${aggregateSum:N2}, average cost: ${aggregateAverage2:N2}.", (string)null);
            return new Aggregates
            {
                Count = aggregateCount,
                Sum = aggregateSum,
                Average = aggregateAverage2
            };
        }

        [FunctionName("GetDataFiles")]
        public static IEnumerable<string> GetDataFiles([ActivityTrigger] IDurableActivityContext ctx)
        {
            for (int i = 1; i <= 12; i++)
            {
                yield return $"http://dfperfdedicatedstorage.blob.core.windows.net/mapreduce-data/nyc-trip-data/yellow_tripdata_2017-{i:00}.csv";
            }
        }

        [FunctionName("ProcessTripDataFile")]
        public static async Task<Aggregates> ProcessTripDataFile([ActivityTrigger] string url, ILogger log)
        {
            Aggregates results = new Aggregates();
            log.LogWarning("Processing " + url + "...");
            using (WebResponse response = await WebRequest.CreateHttp(url).GetResponseAsync())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8, true, 65536))
                    {
                        reader.ReadLine();
                        reader.ReadLine();
                        while (!reader.EndOfStream)
                        {
                            string text = await reader.ReadLineAsync();
                            if (!string.IsNullOrEmpty(text))
                            {
                                double num = double.Parse(text.Substring(text.LastIndexOf(',') + 1));
                                results.Sum += num;
                                results.Count++;
                            }
                        }
                        results.Average = results.Sum / (double)Math.Max(results.Count, 1);
                        string text2 = url.Substring(url.LastIndexOf('/') + 1);
                        log.LogWarning($"Finished processing {text2}. Ride count: {results.Count:N0}. Total fees: ${results.Sum:N2}, Average fee: ${results.Average:N2}.");
                        return results;
                    }
                }
            }
        }

        [FunctionName("StartMapReduce")]
        public static async Task<HttpResponseMessage> StartMapReduce(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartMapReduce")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = (await req.Content.ReadAsAsync<dynamic>())?.InstanceId ?? Guid.NewGuid().ToString("N");
            await starter.StartNewAsync<object>("MapReduceOrchestration", instanceId, null);
            log.LogWarning("Started FanOutFanIn orchestration with ID = '" + instanceId + "'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class Aggregates
        {
            public int Count
            {
                get;
                set;
            }

            public double Sum
            {
                get;
                set;
            }

            public double Average
            {
                get;
                set;
            }
        }
    }
}
