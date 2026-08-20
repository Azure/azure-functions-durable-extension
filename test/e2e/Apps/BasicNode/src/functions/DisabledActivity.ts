// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { ActivityHandler } from 'durable-functions';

// Disabled at runtime via the AzureWebJobs.DisabledActivity.Disabled app setting in
// local.settings.json. Used by DisabledOrchestrationTests to validate that scheduling a
// disabled-but-still-deployed activity fails the orchestration gracefully instead of poison-looping.
const DisabledActivity: ActivityHandler = (input: string): string => input;

df.app.activity('DisabledActivity', { handler: DisabledActivity });
