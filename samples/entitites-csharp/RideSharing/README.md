# RideSharing Sample

This sample demonstrates how to create a simple, stateful, serverless REST service using Azure Functions and Durable Functions.

It models a ride-sharing application where drivers and riders try to match up.

Drivers and riders that are looking for a match can post their current location, as a zip code. The system then searches this zip code and nearby zip codes for a matching participant. If a match is found, a new ride is created and the driver and rider are assigned to it. When the ride is finished, the driver can mark it as completed.

This sample is meant to demonstrate two particular concepts, or patterns:

1. **Indexing**. To make the search scalable, we avoid scanning all users when looking for matches. Instead, we create a "region" entity for each zip code that keeps a list of all users currently in that region. Thus we can easily limit the search to particular regions. Asynchronous signals are used to keep the lists for each region eventually consistent.

2. **Critical Section**. As drivers and riders come and go, and change location, there can be races. For example, two riders may
may try to match with the same driver. To avoid inconsistent states, we use a critical section that locks
 both the driver and the rider, and confirms their availability, before assigning them to a ride.

## Design

The function application contains a total of eight Azure Functions.

Two of the functions are *durable entities*, implementing the stateful components.

- The **UserEntity** stores the status of a user, including their currently advertised location (if any) and assigned ride (if any). There is one such entity per user, and the entity key is the user id. The UserEntity has the following three operations:
  - *SetLocation* sets or clears the location where he user is currently looking for a match. It also sends *add-user* or *remove-user* signals to the region entities as needed to keep them up to date.
  - *SetRide* assigns the user to a particular ride, and clears the location.
  - *ClearRide* removes the currently assigned ride.
  - *Get* returns the current location and assigned ride.

- The **RegionEntity** tracks the set of users that currently advertising their presence in a particular region. There is one RegionEntity per region, and the entity key is the zip code of the region. The RegionEntity has the following four operations:
  - *AddUser* adds a user to the list of users in this region.
  - *RemoveUser* removes a user from the list of users in this region.
  - *GetAvailableDrivers* returns all drivers in the list.
  - *GetAvailableRiders* returns all riders in the list.

One of the functions is a *durable orchestration*, and is called when a user advertises that they are looking for a match
in a particular region.

  - The **AdvertiseAvailabilityAndStartSearch** orchestration first updates the location of that user (as stored in the UserEntity). Then, it
    kicks off a search for a match, i.e. a driver or rider whose location is the same region, or a nearby region. This search 
    proceeds by region in order of proximity, and for each region, calls the region entity to get a list of all drivers or riders. 
    Whenever a potential match is found, the orchestration tries to finalize the match in a critical section that locks both participants.
    Finalizing the match means to re-check availability and location of both participants, and
    assign them a ride if they are still available and close enough. 
    Re-checking this information inside the critical section is necessary because it could have changed in
    the meantime. Also, we need to lock both participants in the critical section
    because there can be many searches in progress at the same time and they can race trying to match either participant.
    If finalizing the match fails, the search continues. 
    If no match is found in any of the nearby regions, no further action is taken - the participant just passively
    waiting to be matched. Searches are re-triggered every time a participant advertises their location.

Five of the functions are Http triggers that implement the REST interface. Each of them specifies a path, and uses
the IDurableOrchestrationClient to access the durable entities and durable orchestration. 

| GET http://localhost:7071/user/{userId}/status |
|:---|
| returns the state of the UserEntity for user `userId`,  which is a JSON object containing the currently advertised location and assigned ride.  |

| GET http://localhost:7071/region/{location} |
|:---|
| returns the state of the RegionEntity for the zipcode `location`, which is a JSON array containing the userIds of all users currently in this region.|

| POST http://localhost:7071/user/{userId}/available?location=zipCode |
|:---|
| indicates that the user `userId` is looking for a match in the location `zipCode`. Runs the *AdvertiseAvailabilityAndStartSearch* orchestration.|

| DELETE http://localhost:7071/user/{userId}/available |
|:---|
| clears the location in the UserEntity for user `userId`, indicating that the user is not currently looking for match.|

| POST http://localhost:7071/user/{driverId}/completed?rideId=guid |
|:---|
| marks the ride as completed, unassigning both participants. Only the driver can call this, and the rideId must match the currently assigned ride.|

## Running The Sample Locally with VS

Open RideSharing.sln in Visual Studio, compile, and run.
On Windows, this automatically starts the local development storage. On macOS, you can edit the local.settings.json and replace UseDevelopmentStorage=true with a connection string to an Azure storage account.
Once the function runtime starts successfully, the console shows the progress. After some time it prints a list of the HTTP bindings.

You can now use curl (or any other tool that lets you compose HTTP requests) to test the service via these endpoints.

### Sample interaction

For example, let's say rider R-77 is becoming available at location 98166, looking for a driver:

    > curl http://localhost:7071/user/R-77/available?location=98166 -d ""

We can check the status of R-77, which shows it is still looking and not assigned to a ride yet (because we have no drivers in the system).

    > curl http://localhost:7071/user/R-77/status
    {"userId":"R-77","location":98166,"currentRide":null}

Now let's say driver D-1233 becomes available at location 98154 (which is adjacent), posting

    > curl http://localhost:7071/user/D-1233/available?location=98154 -d ""

Now, if we check the status of R-77 and D-1233, we see (possibly with some delay) that they have been assigned to the ride with id `1a816b50-12e3-5359-acf7-c15b4d82cc71`:

    > curl http://localhost:7071/user/R-77/status
    {"userId":"R-77","location":null,"currentRide":{"rideId":"1a816b50-12e3-5359-acf7-c15b4d82cc71","driverId":"D-1233","riderId":"R-77", "driverLocation": 98154,"riderLocation":98166}}
    > curl http://localhost:7071/user/D-1233/status
    {"userId":"D-1233","location":null,"currentRide":{"rideId":"1a816b50-12e3-5359-acf7-c15b4d82cc71","driverId":"D-1233","riderId":"R-77", "driverLocation":98154,"riderLocation":98166}}

Finally, D-1233 can mark the ride as completed (where XXX is the rideId guid returned in the previous step)

    > curl http://localhost:7071/user/D-1233/completed?rideId=XXX -d ""

Which then means the status of both participants are back to neutral:

    > curl http://localhost:7071/user/R-77/status
    {"userId":"R-77","location":null,"currentRide":null}
    > curl http://localhost:7071/user/D-1233/status
    {"userId":"D-1233","location":null,"currentRide":null}
