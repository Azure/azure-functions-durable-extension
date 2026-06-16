// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Core;
using DurableTask.Core.History;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Unit tests for the <c>TaskScheduledEvent.Tags</c> propagation contract introduced when
    /// <c>DurableClient.GetStatusAsync(showHistory: true)</c> folds <c>TaskScheduled</c> events
    /// into the aggregated <c>TaskCompleted</c> / <c>TaskFailed</c> history items returned to
    /// callers. Without this propagation, <c>dt.retry.*</c> retry-metadata tags written by the
    /// extension would be invisible to client-side consumers (dashboards, custom inspection
    /// tooling) walking the returned history.
    /// </summary>
    public class DurableClientHistoryTagsTests
    {
        private const int ScheduledEventId = 7;

        // -----------------------------------------------------------------
        // TrackNameAndScheduledTime
        // -----------------------------------------------------------------

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TrackNameAndScheduledTime_NoTagsField_StoresNullTags()
        {
            JObject scheduled = BuildScheduledEvent(tags: null, omitTagsField: true);
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);

            Assert.True(mapper.TryGetValue($"TaskScheduled_{ScheduledEventId}", out DurableClient.EventIndexDateMapping mapping));
            Assert.Null(mapping.Tags);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TrackNameAndScheduledTime_NullTagsField_StoresNullTags()
        {
            JObject scheduled = BuildScheduledEvent(tags: null);
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);

            Assert.True(mapper.TryGetValue($"TaskScheduled_{ScheduledEventId}", out DurableClient.EventIndexDateMapping mapping));
            Assert.Null(mapping.Tags);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TrackNameAndScheduledTime_WithRetryTags_DeepClonesTagsIntoMapping()
        {
            var originalTags = new JObject
            {
                ["dt.retry.attempt"] = "2",
                ["dt.retry.maxAttempts"] = "5",
            };
            JObject scheduled = BuildScheduledEvent(tags: originalTags);
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);

            Assert.True(mapper.TryGetValue($"TaskScheduled_{ScheduledEventId}", out DurableClient.EventIndexDateMapping mapping));
            Assert.NotNull(mapping.Tags);
            Assert.Equal("2", (string)mapping.Tags["dt.retry.attempt"]);
            Assert.Equal("5", (string)mapping.Tags["dt.retry.maxAttempts"]);

            // Mutating the source must not affect the cloned copy on the mapping.
            originalTags["dt.retry.attempt"] = "999";
            Assert.Equal("2", (string)mapping.Tags["dt.retry.attempt"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TrackNameAndScheduledTime_TagsAsNonObjectToken_StoresNullTags()
        {
            // Defensive: if Tags ever appears as a non-object JToken (e.g. JArray from a
            // misbehaving backend), the mapping should not crash and should not capture it.
            JObject scheduled = BuildScheduledEvent(tags: null);
            scheduled["Tags"] = new JArray("unexpected");
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);

            Assert.True(mapper.TryGetValue($"TaskScheduled_{ScheduledEventId}", out DurableClient.EventIndexDateMapping mapping));
            Assert.Null(mapping.Tags);
        }

        // -----------------------------------------------------------------
        // AddScheduledEventDataAndAggregate
        // -----------------------------------------------------------------

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AddScheduledEventDataAndAggregate_WithRetryTags_PropagatesTagsToAggregatedEvent()
        {
            Dictionary<string, DurableClient.EventIndexDateMapping> mapper = BuildMapperWithScheduledEvent(new JObject
            {
                ["dt.retry.attempt"] = "3",
                ["dt.retry.maxAttempts"] = "5",
            });
            JObject completed = BuildAggregatedEvent();
            var indexList = new List<int>();

            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", completed, indexList, showInput: false);

            Assert.NotNull(completed["Tags"]);
            JObject propagated = Assert.IsType<JObject>(completed["Tags"]);
            Assert.Equal("3", (string)propagated["dt.retry.attempt"]);
            Assert.Equal("5", (string)propagated["dt.retry.maxAttempts"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AddScheduledEventDataAndAggregate_NoTags_DoesNotAddTagsField()
        {
            Dictionary<string, DurableClient.EventIndexDateMapping> mapper = BuildMapperWithScheduledEvent(tags: null);
            JObject completed = BuildAggregatedEvent();
            var indexList = new List<int>();

            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", completed, indexList, showInput: false);

            Assert.Null(completed["Tags"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AddScheduledEventDataAndAggregate_EmptyTagsObject_DoesNotAddTagsField()
        {
            // Common no-retry-policy case: Tags exists on the scheduled event but is an empty
            // JObject. The aggregator should keep the response shape unchanged for this case
            // so existing consumers do not see new empty {} fields appear on every history item.
            Dictionary<string, DurableClient.EventIndexDateMapping> mapper = BuildMapperWithScheduledEvent(new JObject());
            JObject completed = BuildAggregatedEvent();
            var indexList = new List<int>();

            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", completed, indexList, showInput: false);

            Assert.Null(completed["Tags"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void AddScheduledEventDataAndAggregate_NoMatchingScheduledEvent_LeavesAggregatedEventUntouched()
        {
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();
            JObject completed = BuildAggregatedEvent();
            var indexList = new List<int>();

            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", completed, indexList, showInput: false);

            Assert.Null(completed["Tags"]);
            Assert.Null(completed["ScheduledTime"]);
            Assert.Empty(indexList);
        }

        // -----------------------------------------------------------------
        // End-to-end pair: TrackNameAndScheduledTime → AddScheduledEventDataAndAggregate
        // -----------------------------------------------------------------

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Pair_TaskScheduledThenTaskFailed_PropagatesRetryTagsOntoFailedEvent()
        {
            JObject scheduled = BuildScheduledEvent(tags: new JObject
            {
                ["dt.retry.attempt"] = "1",
                ["dt.retry.maxAttempts"] = "3",
            });
            JObject failed = BuildAggregatedEvent();
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();
            var indexList = new List<int>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);
            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", failed, indexList, showInput: false);

            JObject propagated = Assert.IsType<JObject>(failed["Tags"]);
            Assert.Equal("1", (string)propagated["dt.retry.attempt"]);
            Assert.Equal("3", (string)propagated["dt.retry.maxAttempts"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Pair_TaskScheduledThenTaskCompleted_NoTags_KeepsAggregatedEventShapeUnchanged()
        {
            JObject scheduled = BuildScheduledEvent(tags: null);
            JObject completed = BuildAggregatedEvent();
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();
            var indexList = new List<int>();

            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);
            DurableClient.AddScheduledEventDataAndAggregate(ref mapper, prefix: "TaskScheduled", completed, indexList, showInput: false);

            Assert.Null(completed["Tags"]);
        }

        // -----------------------------------------------------------------
        // Test helpers
        // -----------------------------------------------------------------

        private static JObject BuildScheduledEvent(JObject tags, bool omitTagsField = false)
        {
            var scheduled = new JObject
            {
                ["EventId"] = ScheduledEventId,
                ["Name"] = "MyActivity",
                ["Timestamp"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ["Input"] = "\"hello\"",
            };

            if (!omitTagsField)
            {
                scheduled["Tags"] = tags;
            }

            return scheduled;
        }

        private static JObject BuildAggregatedEvent()
        {
            // Minimal aggregated TaskCompleted / TaskFailed history shape that the aggregator inspects.
            return new JObject
            {
                ["TaskScheduledId"] = ScheduledEventId,
            };
        }

        private static Dictionary<string, DurableClient.EventIndexDateMapping> BuildMapperWithScheduledEvent(JObject tags)
        {
            JObject scheduled = BuildScheduledEvent(tags);
            var mapper = new Dictionary<string, DurableClient.EventIndexDateMapping>();
            DurableClient.TrackNameAndScheduledTime(scheduled, EventType.TaskScheduled, index: 0, mapper);
            return mapper;
        }
    }
}
