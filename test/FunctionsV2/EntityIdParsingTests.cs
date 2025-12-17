using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class EntityIdParsingTests
    {
        [Fact]
        public void EntityIdParsing_CorrectlyParsesValidEntityId()
        {
            var entityId = EntityId.FromString("@entityName@entityKey");
            Assert.Equal("entityName", entityId.EntityName);
            Assert.Equal("entityKey", entityId.EntityKey);
        }

        [Fact]
        public void EntityIdParsing_ThrowsOnInvalidEntityId()
        {
            Assert.Throws<ArgumentException>(() => EntityId.FromString(null));
            Assert.Throws<ArgumentException>(() => EntityId.FromString(""));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalidEntityId"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalid@symbolplacement"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalid@symbol@placement"));
        }
    }
}
