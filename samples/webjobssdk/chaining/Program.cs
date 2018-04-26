using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DFWebJobsSample
{
    public static class Program
    {
        static void Main(string[] args)
        {
            var config = new JobHostConfiguration();
            config.LoggerFactory = new LoggerFactory();

            config.UseTimers();
            config.UseDurableTask(new DurableTaskExtension
            {
                HubName = "MyTaskHub",
            });

            var host = new JobHost(config);
            host.RunAndBlock();
        }
    }
}
