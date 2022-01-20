// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace DotNetIsolated;

public class Program
{
    public static void Main()
    {
        IHost host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .Build();
        host.Run();
    }
}
