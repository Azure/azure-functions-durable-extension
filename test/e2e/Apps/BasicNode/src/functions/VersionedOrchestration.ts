// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, InvocationContext } from "@azure/functions";
import * as df from "durable-functions";
import { OrchestrationContext, OrchestrationHandler } from "durable-functions";

const VersionedOrchestration: OrchestrationHandler = function* (
    context: OrchestrationContext
) {
    return `Version: '${context.df.version}'`;
};
df.app.orchestration("VersionedOrchestration", VersionedOrchestration);

const HttpStart: HttpHandler = async (
    request: HttpRequest,
    context: InvocationContext
): Promise<HttpResponse> => {
    const client = df.getClient(context);
    const version = request.query.get("version") ?? undefined;
    const instanceId = await client.startNew("VersionedOrchestration", { version });

    context.log(`Started orchestration with ID = '${instanceId}' and Version = '${version}'.`);

    return client.createCheckStatusResponse(request, instanceId);
};

app.http("OrchestrationVersion_HttpStart", {
    route: "OrchestrationVersion_HttpStart",
    extraInputs: [df.input.durableClient()],
    handler: HttpStart,
});
