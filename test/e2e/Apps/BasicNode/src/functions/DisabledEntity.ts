// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { EntityContext, EntityHandler } from 'durable-functions';

// Disabled at runtime via the AzureWebJobs.DisabledEntity.Disabled app setting in
// local.settings.json. Used by DisabledOrchestrationTests to validate that calling an operation on a
// disabled-but-still-deployed entity fails the orchestration gracefully instead of poison-looping.
const DisabledEntity: EntityHandler<unknown> = (context: EntityContext<unknown>) => {
    context.df.return(null);
};

df.app.entity('DisabledEntity', DisabledEntity);
