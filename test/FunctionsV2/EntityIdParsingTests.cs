// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class EntityIdParsingTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EntityIdParsing_CorrectlyParsesValidEntityId()
        {
            var entityId = EntityId.FromString("@entityName@entityKey");
            Assert.Equal("entityName".ToLowerInvariant(), entityId.EntityName);
            Assert.Equal("entityKey", entityId.EntityKey);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EntityIdParsing_ThrowsOnInvalidEntityId()
        {
            Assert.Throws<ArgumentException>(() => EntityId.FromString(null));
            Assert.Throws<ArgumentException>(() => EntityId.FromString(""));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalidEntityId"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("@"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("@@"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("@invalid@"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("@@invalid"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalid@symbolplacement"));
            Assert.Throws<ArgumentException>(() => EntityId.FromString("invalid@symbol@placement"));
        }
    }
}
