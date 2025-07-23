// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app } from '@azure/functions';

app.setup({
    enableHttpStream: true,
});
