// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Chirper.Service
{
    /// <summary>
    /// A data structure representing a chirp.
    /// </summary>
    public struct Chirp
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }
}
