// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Chirper.Service
{
    /// <summary>
    /// A data structure representing a chirp.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public struct Chirp
    {
        public string UserId { get; set; }

        public DateTime Timestamp { get; set; }

        public string Content { get; set; }
    }
}
