import * as df from "durable-functions";
import { app, HttpHandler, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';

// Orchestration
const ExternalEventOrchestrator: df.OrchestrationHandler = function* (context: df.OrchestrationContext): Generator<any, string, any> {
    yield context.df.waitForExternalEvent("Approval");
    return "Orchestrator Finished!";
};
df.app.orchestration("ExternalEventOrchestrator", ExternalEventOrchestrator);

// HTTP Trigger to send external event
const SendExternalEvent_HttpStart: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    try {
        let body = await request.json().catch(() => ({}));
        let instanceId = body.toString() || request.query.get("instanceId") || request.params["instanceId"];
        if (typeof instanceId === "object" && instanceId !== null) {
            instanceId = instanceId;
        }
        await client.raiseEvent(instanceId, "Approval", true);
        return {
            status: 200,
            body: `External event sent to ${instanceId}.`
        };
    } catch (ex: any) {
        return {
            status: 400,
            body: `${ex.constructor.name}: ${ex.message}`
        };
    }
};
app.http("SendExternalEvent_HttpStart", {
    route: "SendExternalEvent_HttpStart",
    extraInputs: [df.input.durableClient()],
    methods: ["GET", "POST"],
    handler: SendExternalEvent_HttpStart
});