# Chirper Sample

This sample demonstrates how to create a simple, stateful, serverless REST service using 
Azure Functions and Durable Functions. 

The users of this service can: 

1. post messages (called "chirps") to their account, or delete them
2. follow or unfollow other users
3. view a timeline containing all chirps of the people they are following, sorted by timestamp

This sample is meant to highlight support for stateful entities (a new feature in 2.0), and how they 
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

Seven of the functions are Http triggers that implement the REST interface (see next section for a list). Each of them specifies a path, and uses
the IDurableOrchestrationClient to access the durable entities and durable orchestration. 

  - The POST methods signal the respective entities, and returns 202.
  - The GET methods for chirps or follows read the entity state .
  - The GET method for the timeline calls the GetTimelineOrchestration and returns either the result, 
    or a 202 including an URL for status.


## Running The Sample Locally with VS

Open Chirper.sln in Visual Studio, compile, and run. 

On Windows, this automatically starts the local development storage. 
On macOS, you can edit the `local.settings.json` and replace `UseDevelopmentStorage=true` with a connection string to an Azure storage account.

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

For example, let's say Alice adds three chirps using POST:

    curl -d "Alice's first message" http://localhost:7071/user/alice/chirps -H Content-Type:application/text
    curl -d "Alice's second message" http://localhost:7071/user/alice/chirps -H Content-Type:application/text
    curl -d "Alice's third message" http://localhost:7071/user/alice/chirps -H Content-Type:application/text

We can then query Alice's chirps using GET:

    curl http://localhost:7071/user/alice/chirps

which returns a JSON representation of all the chirps by Alice, including timestamps:

    [{"userId":"alice","timestamp":"2019-05-01T15:45:42.2223472Z","content":"Alice's first message"},{"userId":"alice","timestamp":"2019-05-01T15:45:44.7693918Z","content":"Alice's second message"},{"userId":"alice","timestamp":"2019-05-01T15:45:45.7658774Z","content":"Alice's third message"}]

Let's add some more messages by other users:

    curl -d "Bob's first message" http://localhost:7071/user/bob/chirps -H Content-Type:application/text
    curl -d "Charlie's first message" http://localhost:7071/user/charlie/chirps -H Content-Type:application/text
    curl -d "Bob's second message" http://localhost:7071/user/bob/chirps -H Content-Type:application/text

Then, let's say Doris wants to follow Alice, Bob, and Charlie

    curl -d "" http://localhost:7071/user/doris/follows/alice
    curl -d "" http://localhost:7071/user/doris/follows/bob
    curl -d "" http://localhost:7071/user/doris/follows/charlie

Finally, if Doris queries the timeline now, she will see all the messages of Alice, Bob, and Charlie, sorted by timestamp.

    curl http://localhost:7071/user/doris/timeline



