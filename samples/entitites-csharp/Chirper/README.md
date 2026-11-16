# Chirper Sample

This sample demonstrates how to create a simple, stateful, serverless REST service using
Azure Functions v4, .NET 10 isolated worker, and Durable Functions with the Azure Storage backend.

The users of this service can: 

1. post messages (called "chirps") to their account, or delete them
2. follow or unfollow other users
3. view a timeline containing all chirps of the people they are following, sorted by timestamp

This sample is meant to highlight support for stateful entities, and how they
can be used in conjunction with Durable Orchestrations. It is based on
[sample code from a paper](https://www.microsoft.com/en-us/research/publication/reactive-caching-for-composed-services/), 
which was itself inspired by the Chirper virtual actor sample included with the 
[Orleans framework](https://github.com/dotnet/orleans).


## Design

The function application contains a total of 10 Azure Functions.

Two of the functions are *durable entities*, implementing the stateful components.

  - The **UserChirps** entity stores a list of chirps, representing the chirps by a particular user. 
    There is one UserChirps entity per user: The entity key is the userId. The operations are *Add* (adds a chirp), 
    *Remove* (removes a chirp) and *Get* (returns the list of chirps).

  - The **UserFollows** entity stores the set of users that a particular user is following.  
    There is one UserFollows entity per user: The entity key is the userId. The operations are *Add* (follows a user), 
    *Remove* (unfollows a user) and *Get* (returns the list of followed users).

One of the functions is a *durable orchestration*, implementing the timeline query.

  - The **GetTimeline** orchestration collects the chirps for the timeline of a particular user. 
    It first calls the `UserFollows` entity to get a list of the followed users. Then it calls the UserChirps
    entities of all the followed users, *in parallel*. Once it receives all the lists
    it combines them and sorts them.

Seven of the functions are HTTP triggers that implement the REST interface (see next section for a list). Each of them specifies a path, and uses
`DurableTaskClient` to access the durable entities and durable orchestration.

  - The POST and DELETE methods signal the respective entities and return 202. The signal is queued;
    subsequent reads are eventually consistent, so poll for the expected state instead of assuming the signal has already completed.
  - The GET methods for chirps or follows read the entity state and return 404 if the entity does not exist.
  - The GET method for the timeline starts `GetTimeline` and waits up to 10 seconds. It returns the chirp array
    on completion, or 202 with a `Location` header and `statusQueryGetUri` to poll. A completed status response
    contains the array in `output`. A failed orchestration is not returned as a successful timeline.

The HTTP functions use asynchronous request-body reads and JSON response writes. No synchronous-I/O
setting or `FUNCTIONS_V2_COMPATIBILITY_MODE` is needed.

## Running the sample locally

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0),
[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite).
When building inside this repository, use an SDK compatible with the repository's `global.json`.
The isolated worker and HTTP packages inherit the repository's central versions; the released Durable
extension version is scoped to this sample in `Directory.Packages.props`.

Start Azurite in a separate terminal before starting the Functions host:

```shell
azurite
```

From this directory, build the sample and start the host:

```shell
dotnet build Chirper.sln
cd Chirper.Service
func start
```

Alternatively, open `Chirper.sln` in a version of Visual Studio that supports .NET 10, set
`Chirper.Service` as the startup project, and run with Azurite already started.
`local.settings.json` selects `dotnet-isolated` and uses local development storage on all platforms.
For deployment, configure `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` and an Azure Storage connection
in the function app's settings; local settings are not published.

Once the function runtime starts successfully, the console shows the progress. 
After some time it prints a list of the HTTP bindings:

        UserChirpsDelete: [DELETE] http://localhost:7071/user/{userId}/chirps/{timestamp}
        UserChirpsGet: [GET] http://localhost:7071/user/{userId}/chirps
        UserChirpsPost: [POST] http://localhost:7071/user/{userId}/chirps
        UserFollowsDelete: [DELETE] http://localhost:7071/user/{userId}/follows/{userId2}
        UserFollowsGet: [GET] http://localhost:7071/user/{userId}/follows
        UserFollowsPost: [POST] http://localhost:7071/user/{userId}/follows/{userId2}
        UserTimelineGet: [GET] http://localhost:7071/user/{userId}/timeline


You can now use [curl](https://github.com/curl/curl) (or any other tool that lets you compose HTTP requests) 
to test the chirper service via these endpoints. 

### Sample interaction

These endpoints retain their original routes (there is no `/api` prefix). Core Tools disables function-key
authorization locally. In Azure, supply a function key. `Authenticate` is only a placeholder:
this sample does **not** authenticate individual users or authorize access to a user's data.

For example, let's say Alice adds three chirps using POST:

    curl -d "Alice's first message" http://localhost:7071/user/alice/chirps -H "Content-Type: text/plain"
    curl -d "Alice's second message" http://localhost:7071/user/alice/chirps -H "Content-Type: text/plain"
    curl -d "Alice's third message" http://localhost:7071/user/alice/chirps -H "Content-Type: text/plain"

We can then query Alice's chirps using GET:

    curl http://localhost:7071/user/alice/chirps

which returns a JSON representation of all the chirps by Alice, including timestamps:

    [{"userId":"alice","timestamp":"2019-05-01T15:45:42.2223472Z","content":"Alice's first message"},{"userId":"alice","timestamp":"2019-05-01T15:45:44.7693918Z","content":"Alice's second message"},{"userId":"alice","timestamp":"2019-05-01T15:45:45.7658774Z","content":"Alice's third message"}]

Let's add some more messages by other users:

    curl -d "Bob's first message" http://localhost:7071/user/bob/chirps -H "Content-Type: text/plain"
    curl -d "Charlie's first message" http://localhost:7071/user/charlie/chirps -H "Content-Type: text/plain"
    curl -d "Bob's second message" http://localhost:7071/user/bob/chirps -H "Content-Type: text/plain"

Then, let's say Doris wants to follow Alice, Bob, and Charlie

    curl -d "" http://localhost:7071/user/doris/follows/alice
    curl -d "" http://localhost:7071/user/doris/follows/bob
    curl -d "" http://localhost:7071/user/doris/follows/charlie

Finally, if Doris queries the timeline now, she will see all the messages of Alice, Bob, and Charlie, sorted by timestamp.

    curl http://localhost:7071/user/doris/timeline

If this returns 202, GET the returned `statusQueryGetUri` until `runtimeStatus` is `Completed`, then
read `output`. Wait for each preceding entity update to become visible before querying a timeline
that depends on it.

To delete one chirp, use the exact UTC `timestamp` returned by POST or GET. The route explicitly
normalizes timestamps to UTC before matching a stored chirp; an invalid timestamp returns 400.

    curl -X DELETE http://localhost:7071/user/alice/chirps/2019-05-01T15:45:42.2223472Z

To unfollow a user, no request body is required:

    curl -X DELETE http://localhost:7071/user/doris/follows/bob

Poll the corresponding GET endpoint to observe either operation completing. Timelines contain only
followed users' chirps, sorted oldest first, and a user with no follows has an empty timeline.

## Existing in-process data

The isolated sample deliberately uses the new `ChirperIsolated` task hub instead of `SampleHubVS`.
It starts with empty state; changing the hub name does not migrate old entities or orchestration history.
Entity state remains an object containing `Chirps` or `FollowedUsers`, rather than a bare list.
HTTP chirps keep their `userId`, `timestamp`, and `content` JSON fields.

The worker SDK, entity dispatch protocol, and serializer have changed. Do not point this sample at
an active in-process hub or assume its persisted state/history is compatible. Exporting and importing
existing application data requires a separate migration after stopping the old application.
