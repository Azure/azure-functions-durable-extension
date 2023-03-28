// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal struct EntityTraceFlags
    {
        // state was rehydrated
        public const char Rehydrated = 'Y';

        // the execution was suspended (to be continued in a fresh batch), or resumed
        public const char Suspended = 'S';
        public const char Resumed = 'R';
        public const char MitigationResumed = 'M';

        // reasons for suspending execution
        public const char TimedOut = 'T';
        public const char HostShutdown = 'H';
        public const char SignificantTimeElapsed = 'E';
        public const char BatchSizeLimit = 'L';

        // execution is waiting for new messages after a continue-as-new
        public const char WaitForEvents = 'W';

        // the execution bypassed the functions middleware because no user code is called
        public const char DirectExecution = 'D';

        // an internal error was captured
        public const char InternalError = '!';

        // trace flags
        private StringBuilder traceFlags;

        public string TraceFlags => this.traceFlags.ToString();

        public void AddFlag(char flag)
        {
            // we concatenate the trace flag characters, they serve as a 'trail of bread crumbs' to reconstruct code path
            (this.traceFlags ??= new StringBuilder()).Append(flag);
        }
    }
}
