# A GitHub triage helper for Durable Functions

## What is it?

A serverless app that automatically lists all issues to be triaged across Durable Functions repos.

## How to use it?

This app can be executed on the cloud or locally. Irrespective of the location, you can obtain the list of issues to triage by GET requesting the `<appHost>/api/triage` endpoint.

## Next Steps

The current implementation is subject to hourly rate limits, which means it error out if too many users attempt to run it within a given hour window.
To make the implementation more reliable, we should register the app with a GitHub API token so that our hourly rate limit can be increased.
