// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [DataContract]
    internal class ComplexType
    {
        [DataContract]
        public enum CustomEnum
        {
            [EnumMember]
            Value1,

            [EnumMember]
            Value2,
        }

        [DataMember]
        public int A { get; set; }

        [DataMember]
        public List<DateTime> B { get; set; }

        [DataMember]
        public CustomEnum C { get; set; }

        [DataMember]
        public ComplexInnerType D { get; set; }

        [DataContract]
        public class ComplexInnerType
        {
            [DataMember]
            public string E { get; set; }

            [DataMember]
            public TimeSpan F { get; set; }
        }
    }
}
