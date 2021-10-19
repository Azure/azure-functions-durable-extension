using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace DurableFunctions.NetheriteEndToEnd
{
    public class NetheriteEndToEndTests : DurableTaskEndToEndTests
    {
        public NetheriteEndToEndTests(ITestOutputHelper output)
            : base(output)
        { }

        public override TestHelpers GetTestHelpers(ITestOutputHelper output)
        {
            return new NetheriteTestHelper(output);
        }
    }
}
