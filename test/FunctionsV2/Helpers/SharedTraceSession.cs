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
        // Thread-safe collection of subscriber callbacks and their per-subscriber event filters.
        private static readonly ConcurrentDictionary<int, SubscriberInfo> Subscribers
            = new ConcurrentDictionary<int, SubscriberInfo>();

        private static readonly object SyncLock = new object();

        // Tracks providers already enabled on the shared session so subsequent
        // subscribers can add new ones without duplicates.
        private static readonly Dictionary<string, TraceEventLevel> EnabledProviders
            = new Dictionary<string, TraceEventLevel>(StringComparer.OrdinalIgnoreCase);

        private static TraceEventSession session;
        private static Thread backgroundThread;
        private static int refCount;
        private static int nextSubscriberId;

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
            lock (SyncLock)
            {
                int id = ++nextSubscriberId;
                Subscribers[id] = new SubscriberInfo { Callback = callback, EventIdFilters = eventIdFilters };
                refCount++;

                if (session == null)
                {
                    string sessionName = "DTFxTrace" + Guid.NewGuid().ToString("N");
                    session = new TraceEventSession(sessionName);
                    EnableNewProviders(providers);

                    backgroundThread = new Thread(_ =>
                    {
                        Thread.CurrentThread.Name = $"SharedETWListener: {sessionName}";

                        session.Source.Dynamic.All += data =>
                        {
                            if (IsNoiseEvent(data))
                            {
                                return;
                            }

                            // Fan out to all subscribers, applying per-subscriber filters.
                            foreach (var kvp in Subscribers)
                            {
                                try
                                {
                                    if (ShouldExcludeEvent(data, kvp.Value.EventIdFilters))
                                    {
                                        continue;
                                    }

                                    kvp.Value.Callback(data);
                                }
                                catch (Exception ex)
                                {
                                    // Individual subscriber failures must not kill the shared session,
                                    // but we still log them for diagnosability.
                                    Console.Error.WriteLine(
                                        $"SharedTraceSession subscriber {kvp.Key} threw exception: {ex}");
                                }
                            }
                        };

                        // Blocking call - runs until StopProcessing() is called.
                        session.Source.Process();
                    });

                    backgroundThread.IsBackground = true;
                    backgroundThread.Start();
                }
                else
                {
                    // Session already exists — enable any providers not yet enabled.
                    EnableNewProviders(providers);
                }

                return id;
            }
        }

        /// <summary>
        /// Unsubscribes a previously registered callback. Disposes the shared session
        /// when the last subscriber is removed.
        /// </summary>
        public static void Unsubscribe(int subscriberId)
        {
            lock (SyncLock)
            {
                if (!Subscribers.TryRemove(subscriberId, out _))
                {
                    // Already unsubscribed or never subscribed — nothing to do.
                    return;
                }

                refCount--;
                if (refCount <= 0 && session != null)
                {
                    session.Source.StopProcessing();
                    backgroundThread?.Join(TimeSpan.FromMilliseconds(500));
                    session.Dispose();
                    session = null;
                    backgroundThread = null;
                    refCount = 0;
                    EnabledProviders.Clear();
                }
            }
        }

        /// <summary>
        /// Enables providers on the shared session that haven't been enabled yet,
        /// upgrading the trace level if a higher level is requested.
        /// Must be called under <see cref="SyncLock"/>.
        /// </summary>
        private static void EnableNewProviders(IDictionary<string, TraceEventLevel> providers)
        {
            providers
                .Where(p =>
                    !EnabledProviders.TryGetValue(p.Key, out TraceEventLevel currentLevel)
                    || p.Value > currentLevel)
                .Select(p =>
                {
                    session.EnableProvider(p.Key, p.Value);
                    EnabledProviders[p.Key] = p.Value;
                    return p;
                })
                .ToList(); // Force immediate execution of the LINQ query to enable providers within the lock.
        }

        private static bool IsNoiseEvent(TraceEvent traceEvent)
        {
            return traceEvent.EventName == "EventSourceMessage" || traceEvent.EventName == "ManifestData";
        }

        private static bool ShouldExcludeEvent(TraceEvent traceEvent, IDictionary<string, IEnumerable<int>> eventIdFilters)
        {
            if (eventIdFilters != null &&
                eventIdFilters.TryGetValue(traceEvent.ProviderName, out IEnumerable<int> filteredEvents))
            {
                return filteredEvents.Contains((int)traceEvent.ID);
            }

            return false;
        }

        private sealed class SubscriberInfo
        {
            public Action<TraceEvent> Callback { get; set; }

            public IDictionary<string, IEnumerable<int>> EventIdFilters { get; set; }
        }
    }
}
