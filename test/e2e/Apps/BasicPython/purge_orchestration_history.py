#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import logging
from datetime import datetime
from typing import Optional

import azure.functions as func
import azure.durable_functions as df

bp = df.Blueprint()

@bp.route(route="PurgeOrchestrationHistory", methods=["GET", "POST"])
@bp.durable_client_input(client_name="client")
async def purge_history(req: func.HttpRequest, client: df.DurableOrchestrationClient):
    logging.info("Starting purge all instance history")
    try:
        # Parse optional query parameters for purgeStartTime and purgeEndTime
        purge_start_time: Optional[datetime] = None
        purge_end_time: Optional[datetime] = None
        if req.params.get("purgeStartTime"):
            purge_start_time = datetime.fromisoformat(req.params["purgeStartTime"])
        if req.params.get("purgeEndTime"):
            purge_end_time = datetime.fromisoformat(req.params["purgeEndTime"])

        # Purge orchestration history
        result = await client.purge_instance_history_by(
            created_time_from=purge_start_time,
            created_time_to=purge_end_time,
            runtime_status=[
                df.OrchestrationRuntimeStatus.Completed,
                df.OrchestrationRuntimeStatus.Failed,
                df.OrchestrationRuntimeStatus.Terminated,
            ],
        )
        logging.info("Finished purge all instance history")
        return func.HttpResponse(
            f"Purged {result.instances_deleted} records",
            status_code=200,
            mimetype="text/plain"
        )
    except Exception as ex:
        logging.error("Failed to purge all instance history", exc_info=True)
        return func.HttpResponse(
            f"Failed to purge all instance history: {str(ex)}",
            status_code=500,
            mimetype="text/plain"
        )
