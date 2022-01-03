const df = require("durable-functions");
const { DateTime } = require("luxon");

module.exports = df.orchestrator(function* (context) {
    const input = context.df.getInput();
    context.log("Received monitor request. location: " + (input ? input.location : undefined)
        + ". phone: " + (input ? input.phone : undefined) + ".");

    verifyRequest(input);

    const endTime = DateTime.fromJSDate(context.df.currentUtcDateTime, { zone: 'utc' }).plus({ hours: 6 });
    context.log("Instantiating monitor for " + input.location.city + ", " + input.location.state
        + ". Expires: " + (endTime) + ".");

    while (DateTime.fromJSDate(context.df.currentUtcDateTime, { zone: 'utc' }) < endTime) {
        // Check the weather
        context.log("Checking current weather conditions for " + input.location.city + ", "
            + input.location.state + " at " + context.df.currentUtcDateTime + ".");
        const isClear = yield context.df.callActivity("E3_GetIsClear", input.location);

        if (isClear) {
            // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
            context.log("Detected clear weather for " + input.location.city + ", "
                + input.location.state + ". Notifying " + input.phone + ".");

            yield context.df.callActivity("E3_SendGoodWeatherAlert", input.phone);
            break;
        } else {
            // Wait for the next checkpoint
            var nextCheckpoint = DateTime.fromJSDate(context.df.currentUtcDateTime, { zone: 'utc' }).plus({ seconds: 30 });
            context.log("Next check for " + input.location.city + ", " + input.location.state
                + " at " + nextCheckpoint.toString());

            yield context.df.createTimer(nextCheckpoint.toDate());   // accomodate cancellation tokens
        }
    }

    context.log("Monitor expiring.");
});

function verifyRequest(request) {
    if (!request) {
        throw new Error("An input object is required.");
    }
    if (!request.location) {
        throw new Error("A location input is required.");
    }
    if (!request.phone) {
        throw new Error("A phone number input is required.");
    }
}
