import azure.functions as func

from azure.durable_functions import DurableOrchestrationContext
from azure.durable_functions import Blueprint
import http

bp = Blueprint()

@bp.orchestration_trigger(context_name="context", orchestration="ExternalEventOrchestrator")
def external_event_orchestrator(context: DurableOrchestrationContext) -> str:
    context.wait_for_external_event("Approval")
    return "Orchestrator Finished!"

@bp.route(route="SendExternalEvent_HttpStart", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def send_external_event_http_start(req: func.HttpRequest, client) -> func.HttpResponse:
    try:
        instance_id = req.get_json()
        if isinstance(instance_id, dict):
            instance_id = instance_id.get("instanceId")
        await client.raise_event(instance_id, "Approval", True)
        return func.HttpResponse(
            f"External event sent to {instance_id}.",
            status_code=http.HTTPStatus.OK
        )
    except Exception as ex:
        # gRPC errors are surfaced as generic exceptions in Python SDK
        return func.HttpResponse(
            f"{type(ex).__name__}: {str(ex)}",
            status_code=http.HTTPStatus.BAD_REQUEST
        )