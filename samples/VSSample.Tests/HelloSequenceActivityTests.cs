using Xunit;

namespace VSSample.Tests
{
    public class HelloSequenceActivityTests
    {
        [Fact]
        public void SayHello_returns_greeting()
        {
            var result = HelloSequence.SayHello("John");
            Assert.Equal("Hello John!", result);
        }
    }
}
