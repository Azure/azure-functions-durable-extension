// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// provides message ordering and deduplication of request messages (operations or lock requests)
    /// that are sent to entities, from other entities, or from orchestrations.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal class MessageSorter
    {
        // don't update the reorder window too often since the garbage collection incurs some overhead.
        private static readonly TimeSpan MinIntervalBetweenCollections = TimeSpan.FromSeconds(10);

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, DateTime> LastSentToInstance { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, ReceiveBuffer> ReceivedFromInstance { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime ReceiveHorizon { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime SendHorizon { get; set; }

        [JsonIgnore]
        public int? NumMessages { get; set; }

        [JsonIgnore]
        public int? NumSources { get; set; }

        [JsonIgnore]
        public int? NumDestinations { get; set; }

        /// <summary>
        /// Used for testing purposes.
        /// </summary>
        internal int NumberBufferedRequests =>
            this.ReceivedFromInstance?.Select(kvp => kvp.Value.Buffered?.Count ?? 0).Sum() ?? 0;

        /// <summary>
        /// Called on the sending side, to fill in timestamp and predecessor fields.
        /// </summary>
        public void LabelOutgoingMessage(RequestMessage message, string destination, DateTime now, TimeSpan reorderWindow)
        {
            if (reorderWindow.Ticks == 0)
            {
                return; // we are not doing any message sorting.
            }

            DateTime timestamp = now;

            bool countingEntries = false;
            int destinationCount = 0;

            if (this.SendHorizon + reorderWindow + MinIntervalBetweenCollections < now)
            {
                countingEntries = true;

                this.SendHorizon = now - reorderWindow;

                // clean out send clocks that are past the reorder window

                if (this.LastSentToInstance != null)
                {
                    List<string> expired = new List<string>();

                    foreach (var kvp in this.LastSentToInstance)
                    {
                        if (kvp.Value < this.SendHorizon)
                        {
                            expired.Add(kvp.Key);
                        }
                        else
                        {
                            destinationCount++;
                        }
                    }

                    foreach (var t in expired)
                    {
                        this.LastSentToInstance.Remove(t);
                    }
                }

                this.NumDestinations = destinationCount;
            }

            if (this.LastSentToInstance == null)
            {
                this.LastSentToInstance = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                destinationCount++;
            }
            else if (this.LastSentToInstance.TryGetValue(destination, out var last))
            {
                message.Predecessor = last;

                // ensure timestamps are monotonic even if system clock is not
                if (timestamp <= last)
                {
                    timestamp = new DateTime(last.Ticks + 1);
                }
            }
            else
            {
                destinationCount++;
            }

            message.Timestamp = timestamp;
            this.LastSentToInstance[destination] = timestamp;

            this.NumDestinations = countingEntries ? destinationCount : (int?)null;
        }

        /// <summary>
        /// Called on the receiving side, to reorder and deduplicate within the window.
        /// </summary>
        public IEnumerable<RequestMessage> ReceiveInOrder(RequestMessage message, TimeSpan reorderWindow)
        {
            // messages sent from clients and forwarded lock messages are not participating in the sorting.
            if (reorderWindow.Ticks == 0 || message.ParentInstanceId == null || message.Position > 0)
            {
                // Just pass the message through.
                yield return message;
                yield break;
            }

            bool countingEntries = false;
            int sourceCount = 0;
            int messageCount = 0;

            // advance the horizon based on the latest timestamp
            if (this.ReceiveHorizon + reorderWindow + MinIntervalBetweenCollections < message.Timestamp)
            {
                countingEntries = true;

                this.ReceiveHorizon = message.Timestamp - reorderWindow;

                // deliver any messages that were held in the receive buffers
                // but are now past the reorder window

                List<string> emptyReceiveBuffers = new List<string>();

                if (this.ReceivedFromInstance != null)
                {
                    foreach (var kvp in this.ReceivedFromInstance)
                    {
                        if (kvp.Value.Last < this.ReceiveHorizon)
                        {
                            kvp.Value.Last = DateTime.MinValue;
                        }

                        while (this.TryDeliverNextMessage(kvp.Value, out var next))
                        {
                            yield return next;
                        }

                        if (kvp.Value.Last == DateTime.MinValue
                            && (kvp.Value.Buffered == null || kvp.Value.Buffered.Count == 0))
                        {
                            emptyReceiveBuffers.Add(kvp.Key);
                        }
                        else
                        {
                            sourceCount++;
                            messageCount += kvp.Value.Buffered?.Count() ?? 0;
                        }
                    }

                    foreach (var t in emptyReceiveBuffers)
                    {
                        this.ReceivedFromInstance.Remove(t);
                    }

                    if (this.ReceivedFromInstance.Count == 0)
                    {
                        this.ReceivedFromInstance = null;
                    }
                }
            }

            // Messages older than the reorder window are not participating.
            if (message.Timestamp < this.ReceiveHorizon)
            {
                // Just pass the message through.
                yield return message;

                this.NumSources = countingEntries ? sourceCount : (int?)null;
                this.NumMessages = countingEntries ? messageCount : (int?)null;
                yield break;
            }

            ReceiveBuffer receiveBuffer;

            if (this.ReceivedFromInstance == null)
            {
                this.ReceivedFromInstance = new Dictionary<string, ReceiveBuffer>(StringComparer.OrdinalIgnoreCase);
            }

            if (!this.ReceivedFromInstance.TryGetValue(message.ParentInstanceId, out receiveBuffer))
            {
                this.ReceivedFromInstance[message.ParentInstanceId] = receiveBuffer = new ReceiveBuffer()
                {
                    ExecutionId = message.ParentExecutionId,
                };

                sourceCount++;
            }
            else if (receiveBuffer.ExecutionId != message.ParentExecutionId)
            {
                // this message is from a new execution; release all buffered messages and start over
                if (receiveBuffer.Buffered != null)
                {
                    foreach (var kvp in receiveBuffer.Buffered)
                    {
                        yield return kvp.Value;
                        messageCount--;
                    }

                    receiveBuffer.Buffered.Clear();
                }

                receiveBuffer.Last = DateTime.MinValue;
                receiveBuffer.ExecutionId = message.ParentExecutionId;
            }

            if (message.Timestamp <= receiveBuffer.Last)
            {
                // This message was already delivered, it's a duplicate
                yield break;
            }

            if (message.Predecessor > receiveBuffer.Last
                && message.Predecessor >= this.ReceiveHorizon)
            {
                // this message is waiting for a non-delivered predecessor in the window, buffer it
                if (receiveBuffer.Buffered == null)
                {
                    receiveBuffer.Buffered = new SortedDictionary<DateTime, RequestMessage>();
                }

                receiveBuffer.Buffered[message.Timestamp] = message;
                messageCount++;
            }
            else
            {
                yield return message;

                receiveBuffer.Last = message.Timestamp >= this.ReceiveHorizon ? message.Timestamp : DateTime.MinValue;

                while (this.TryDeliverNextMessage(receiveBuffer, out var next))
                {
                    yield return next;
                    messageCount--;
                }
            }

            this.NumSources = countingEntries ? sourceCount : (int?)null;
            this.NumMessages = countingEntries ? messageCount : (int?)null;
        }

        private bool TryDeliverNextMessage(ReceiveBuffer buffer, out RequestMessage message)
        {
            if (buffer.Buffered != null)
            {
                using (var e = buffer.Buffered.GetEnumerator())
                {
                    if (e.MoveNext())
                    {
                        var pred = e.Current.Value.Predecessor;

                        if (pred <= buffer.Last || pred < this.ReceiveHorizon)
                        {
                            message = e.Current.Value;

                            buffer.Last = message.Timestamp >= this.ReceiveHorizon ? message.Timestamp : DateTime.MinValue;

                            buffer.Buffered.Remove(message.Timestamp);

                            return true;
                        }
                    }
                }
            }

            message = null;
            return false;
        }

        [JsonObject(MemberSerialization.OptOut)]
        internal class ReceiveBuffer
        {
            public DateTime Last { get; set; }// last message delivered, or DateTime.Min if none

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string ExecutionId { get; set; } // execution id of last message, if any

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public SortedDictionary<DateTime, RequestMessage> Buffered { get; set; }
        }
    }
}
