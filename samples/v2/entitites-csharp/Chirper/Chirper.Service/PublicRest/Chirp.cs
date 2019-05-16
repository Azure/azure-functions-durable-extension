// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

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
