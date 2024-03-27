// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedEntities;

internal abstract class Test
{
    public virtual string Name => this.GetType().Name;

    public abstract Task RunAsync(TestContext context);

    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);
}
