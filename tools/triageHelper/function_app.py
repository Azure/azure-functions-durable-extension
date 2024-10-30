import azure.functions as func
import logging
import requests
import urllib.parse

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

# this repo is an exception to our labeling system, so we store it in a different variable to detect it later
powershell_worker_repo = "Azure/azure-functions-powershell-worker"
REPOS = [
    "Azure/durabletask",
    "Azure/azure-functions-durable-extension",
    "Azure/azure-functions-durable-js",
    "Azure/azure-functions-durable-python",
    "Azure/azure-functions-durable-powershell",
    powershell_worker_repo,
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

    # Define the GitHub API endpoint
    api_endpoint = f"https://api.github.com/repos/{repository}/issues"
    query_str1 = "?labels=Needs%3A%20Triage%20%3Amag%3A"
    query_str2 = "?labels=Needs%3A%20Triage%20%28Functions%29"
    query_str = query_str2 if repository == powershell_worker_repo else query_str1

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