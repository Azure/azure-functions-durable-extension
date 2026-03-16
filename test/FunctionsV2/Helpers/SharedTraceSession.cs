// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Manages a shared ETW <see cref="TraceEventSession"/> across all test class instances
    /// to avoid exhausting limited Windows kernel ETW session resources when running tests
    /// in parallel. Uses reference counting so the session is created on first subscriber
    /// and disposed when the last subscriber is removed.
    /// </summary>
    internal static class SharedTraceSession
    {
        // Thread-safe collection of subscriber callbacks.
        private static readonly ConcurrentDictionary<int, Action<TraceEvent>> Subscribers
            = new ConcurrentDictionary<int, Action<TraceEvent>>();

        private static readonly object SyncLock = new object();

        private static TraceEventSession session;
        private static Thread backgroundThread;
        private static int refCount;
        private static int nextSubscriberId;
        private static IDictionary<string, IEnumerable<int>> currentEventIdFilters;

        /// <summary>
        /// Subscribes a callback to receive ETW trace events. Creates the shared session
        /// on first subscription.
        /// </summary>
        /// <returns>A subscriber ID used to unsubscribe later.</returns>
        public static int Subscribe(
            Action<TraceEvent> callback,
            IDictionary<string, TraceEventLevel> providers,
            IDictionary<string, IEnumerable<int>> eventIdFilters = null)
        {
            int id = Interlocked.Increment(ref nextSubscriberId);
            Subscribers[id] = callback;

            lock (SyncLock)
            {
                refCount++;
                if (session == null)
                {
                    currentEventIdFilters = eventIdFilters;
                    string sessionName = "DTFxTrace" + Guid.NewGuid().ToString("N");
                    session = new TraceEventSession(sessionName);
                    foreach (KeyValuePair<string, TraceEventLevel> provider in providers)
                    {
                        session.EnableProvider(provider.Key, provider.Value);
                    }

                    backgroundThread = new Thread(_ =>
                    {
                        Thread.CurrentThread.Name = $"SharedETWListener: {sessionName}";

                        session.Source.Dynamic.All += data =>
                        {
                            if (ShouldExcludeEvent(data, currentEventIdFilters))
                            {
                                return;
                            }

                            // Fan out to all subscribers.
                            foreach (var kvp in Subscribers)
                            {
                                try
                                {
                                    kvp.Value(data);
                                }
                                catch
                                {
                                    // Individual subscriber failures must not kill the shared session.
                                }
                            }
                        };

                        // Blocking call - runs until StopProcessing() is called.
                        session.Source.Process();
                    });

                    backgroundThread.IsBackground = true;
                    backgroundThread.Start();
                }
            }

            return id;
        }

        /// <summary>
        /// Unsubscribes a previously registered callback. Disposes the shared session
        /// when the last subscriber is removed.
        /// </summary>
        public static void Unsubscribe(int subscriberId)
        {
            Subscribers.TryRemove(subscriberId, out _);

            lock (SyncLock)
            {
                refCount--;
                if (refCount <= 0 && session != null)
                {
                    session.Source.StopProcessing();
                    backgroundThread?.Join(TimeSpan.FromMilliseconds(500));
                    session.Dispose();
                    session = null;
                    backgroundThread = null;
                    refCount = 0;
                }
            }
        }

        private static bool ShouldExcludeEvent(TraceEvent traceEvent, IDictionary<string, IEnumerable<int>> eventIdFilters)
        {
            if (traceEvent.EventName == "EventSourceMessage" || traceEvent.EventName == "ManifestData")
            {
                return true;
            }

            if (eventIdFilters != null &&
                eventIdFilters.TryGetValue(traceEvent.ProviderName, out IEnumerable<int> filteredEvents))
            {
                return filteredEvents.Contains((int)traceEvent.ID);
            }

            return false;
        }
    }
}
