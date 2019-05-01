using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chirper.Service
{
    /// <summary>
    /// A data structure representing a chirp.
    /// </summary>
    public struct Chirp
    {
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty(PropertyName = "content")]
        public string Content { get; set; }
    }
}
