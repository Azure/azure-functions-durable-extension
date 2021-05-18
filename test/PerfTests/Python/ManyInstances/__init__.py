import logging
import azure.functions as func
import azure.durable_functions as df
from shared_utils.parse_and_validate_input import parse_and_validate_input
import asyncio

async def main(req: func.HttpRequest, starter: str) -> func.HttpResponse:
    client = df.DurableOrchestrationClient(starter)
    num_instances = parse_and_validate_input(req.get_body())

    tasks = map(lambda _: client.start_new("SequentialOrchestrator"), range(num_instances))
    await gather(tasks=tasks, max_concurrency=200)
    return ""

async def gather(tasks, max_concurrency: int):
    semaphore = asyncio.Semaphore(max_concurrency)
    async def sem_task(task):
        async with semaphore:
            return await task
    return await asyncio.gather(*(sem_task(task) for task in tasks))    