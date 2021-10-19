using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DurableFunctions.SqlServerEndToEnd
{
    public class SqlServerEndToEndTests : DurableTaskEndToEndTests
    {

        public SqlServerEndToEndTests(ITestOutputHelper output) : base(output)
        {
        }

        public override TestHelpers GetTestHelpers(ITestOutputHelper output)
        {
            return new SqlServerTestHelper(output);
        }
    }
}
