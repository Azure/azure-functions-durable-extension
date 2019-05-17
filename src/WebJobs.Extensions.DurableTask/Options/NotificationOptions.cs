﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration of the notification options
    /// for the Durable Task Extension.
    /// </summary>
    public class NotificationOptions
    {
        /// <summary>
        /// The section of configuration related to Event Grid notifications.
        /// </summary>
        public EventGridNotificationOptions EventGrid { get; set; }

        internal void Validate()
        {
            if (this.EventGrid != null)
            {
                this.EventGrid.Validate();
            }
        }

        internal void AddToDebugString(StringBuilder builder)
        {
            if (this.EventGrid != null)
            {
                this.EventGrid.AddToDebugString(builder);
            }
        }
    }
}
