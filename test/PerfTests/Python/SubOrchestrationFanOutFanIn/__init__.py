import logging
import azure.functions as func
import azure.durable_functions as df
from shared_utils.parse_and_validate_input import parse_and_validate_input

async def main(req: func.HttpRequest, starter: str) -> func.HttpResponse:
    client = df.DurableOrchestrationClient(starter)
    num_suborchestrators = parse_and_validate_input(req.get_body())
    instance_id = await client.start_new("SubOrchestrationFanOutFanInOrchestrator", None, num_suborchestrators)

    logging.info(f"Started orchestration with ID = '{instance_id}'.")

    return client.create_check_status_response(req, instance_id)