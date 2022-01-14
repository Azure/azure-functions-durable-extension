To test timeout handling in activity and entity functions,
call

curl http://localhost:7071/api/start/EntityTimeout1
curl http://localhost:7071/api/start/ActivityTimeout

The desired behavior is that when the entity operation times out, a failure response is sent to the orchestrator which then catches the exception and terminates successfully. 
This requires the host to restart, which does not work when debugging in the IDE, but it works when running functions host from the command line, or when running deployed.

Successful completion can be inspected by looking at the results of the two orchestrations in table storage.