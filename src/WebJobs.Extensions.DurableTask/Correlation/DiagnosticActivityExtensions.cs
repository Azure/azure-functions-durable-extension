// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// Extensions for <see cref="Activity"/>.
    /// </summary>
    internal static class DiagnosticActivityExtensions
    {
        // These fields are named in such an "unconventional" way to mimic the internal field names of the Activity class.
        #pragma warning disable SA1311
        #pragma warning disable SA1308
        private static readonly Action<Activity, string> s_setSpanId;
        private static readonly Action<Activity, string> s_setTraceId;
        private static readonly Action<Activity, string?> s_setTraceState;
        #pragma warning restore SA1308
        #pragma warning restore SA1311

        static DiagnosticActivityExtensions()
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            s_setSpanId = typeof(Activity).GetField("_spanId", flags) !.CreateSetter<Activity, string>();
            s_setTraceId = typeof(Activity).GetField("_traceId", flags) !.CreateSetter<Activity, string>();
            s_setTraceState = typeof(Activity).GetField("_traceState", flags) !.CreateSetter<Activity, string?>();
        }

        public static void SetTraceId(this Activity activity, string traceId)
            => s_setTraceId(activity, traceId);

        public static void SetSpanId(this Activity activity, string spanId)
            => s_setSpanId(activity, spanId);

        public static void SetTraceState(this Activity activity, string? traceState)
            => s_setTraceState(activity, traceState);

        /// <summary>
        /// Create a re-usable setter for a <see cref="FieldInfo"/>.
        /// When cached and reused, This is quicker than using <see cref="FieldInfo.SetValue(object, object)"/>.
        /// </summary>
        private static Action<TTarget, TValue> CreateSetter<TTarget, TValue>(this FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException(nameof(fieldInfo));
            }

            ParameterExpression targetExp = Expression.Parameter(typeof(TTarget), "target");
            Expression source = targetExp;

            if (typeof(TTarget) != fieldInfo.DeclaringType)
            {
                source = Expression.Convert(targetExp, fieldInfo.DeclaringType!);
            }

            // Creating the setter to set the value to the field
            ParameterExpression valueExp = Expression.Parameter(typeof(TValue), "value");
            MemberExpression fieldExp = Expression.Field(source, fieldInfo);
            BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);
            return Expression.Lambda<Action<TTarget, TValue>>(assignExp, targetExp, valueExp).Compile();
        }
    }
}
