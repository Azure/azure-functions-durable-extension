
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DurableFunctions.SqlServerEndToEnd
{
    public class TestCredential
    {
        public TestCredential(string userId, string password, string connectionString)
        {
            this.UserId = userId;
            this.ConnectionString = connectionString;
            this.Password = password;
        }

        public string UserId { get; }

        public string Password { get; }

        public string ConnectionString { get; }
    }
}
