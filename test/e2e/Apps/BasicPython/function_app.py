import azure.functions as func
import logging

from hello_cities import bp
from activity_error_handling import bp as error_handling_bp
from entity_error_handling import bp as entity_error_handling_bp
from activity_input_type import bp as activity_input_type_bp
from external_event_orchestration import bp as external_event_bp
from large_output_orchestrator import bp as large_output_bp
from orchestration_query import bp as orchestration_query_bp
from terminate_orchestration import bp as terminate_orchestration_bp
from suspend_resume_orchestration import bp as suspend_resume_orchestration_bp

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

@app.route(route="http_trigger")
def http_trigger(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    name = req.params.get('name')
    if not name:
        try:
            req_body = req.get_json()
        except ValueError:
            pass
        else:
            name = req_body.get('name')

    if name:
        return func.HttpResponse(f"Hello, {name}. This HTTP triggered function executed successfully.")
    else:
        return func.HttpResponse(
             "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.",
             status_code=200
        )

app.register_blueprint(bp)
app.register_blueprint(error_handling_bp)
app.register_blueprint(entity_error_handling_bp)
app.register_blueprint(activity_input_type_bp)
app.register_blueprint(external_event_bp)
app.register_blueprint(large_output_bp)
app.register_blueprint(orchestration_query_bp)
app.register_blueprint(terminate_orchestration_bp)
app.register_blueprint(suspend_resume_orchestration_bp)