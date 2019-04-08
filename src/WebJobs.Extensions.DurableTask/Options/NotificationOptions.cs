using System;
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
        /// Gets or sets the base URL for the HTTP APIs managed by this extension.
        /// </summary>
        /// <remarks>
        /// This property is intended for use only by runtime hosts.
        /// </remarks>
        /// <value>
        /// A URL pointing to the hosted function app that responds to status polling requests.
        /// </value>
        public Uri ApiUrl { get; set; }

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
            // Don't trace the query string, since that contains secrets
            string url = this.ApiUrl.GetLeftPart(UriPartial.Path);
            builder.Append(nameof(this.ApiUrl)).Append(": ").Append(url).Append(", ");

            if (this.EventGrid != null)
            {
                this.EventGrid.AddToDebugString(builder);
            }
        }
    }
}
