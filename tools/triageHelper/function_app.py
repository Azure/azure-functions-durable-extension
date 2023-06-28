import azure.functions as func
import logging
import requests
import urllib.parse

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

REPOS = [
    "Azure/azure-functions-durable-extension",
    "Azure/durabletask",
    "Azure/azure-functions-durable-python",
    "microsoft/durabletask-java",
    "microsoft/durabletask-dotnet",
    "microsoft/durabletask-mssql",
    "microsoft/durabletask-netherite",
    "microsoft/DurableFunctionsMonitor"
]


@app.route(route="triage")
def HttpTrigger(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    results = []
    for repo in REPOS:
        results.append(get_triage_issues(repo))

    report = "\n".join(results)
    return func.HttpResponse(report)

def get_triage_issues(repository):
    # Define the label
    label = "Needs: Triage"

    payload = {
    'labels': label, 
}

    payload_str = urllib.parse.urlencode(payload, safe=':+')
    print(payload_str)

    # Define the GitHub API endpoint
    api_endpoint = f"https://api.github.com/repos/{repository}/issues"
    query_str = "?labels=Needs%3A%20Triage%20%3Amag%3A" # the mag% segment represents the emoji on the label

    # Send a GET request to the API
    response = requests.get(api_endpoint + query_str)

    # Check if the request was successful
    if response.status_code == 200:
        # Get the list of issues from the response
        issues = response.json()

        # Create an empty string to store the issue information
        issues_info = "# Repository: " + repository + "\n\n"

        # Iterate over each issue and append its title and URL to the string
        for issue in issues:
            issue_title = issue["title"]
            issue_url = issue["html_url"]
            issues_info += f"Issue Title: {issue_title}\n"
            issues_info += f"Issue URL: {issue_url}\n"
            issues_info += "------------------------------------\n"

        return issues_info
    else:
        return f"Error: {response.status_code} - {response.text}"