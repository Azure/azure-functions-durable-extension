const df = require("durable-functions");
const moment = require('moment');

module.exports = df(function*(context) {
    const input = context.df.getInput();
    context.log("Received monitor request. location: " + (input ? input.location : undefined)
        + ". phone: " + (input ? input.phone : undefined) + ".");

    verifyRequest(input);

    const endTime = moment.utc(context.df.currentUtcDateTime).add(6, 'h');
    context.log("Instantiating monitor for " + input.location.city + ", " + input.location.state
        + ". Expires: " + (endTime) + ".");

    while (moment.utc(context.df.currentUtcDateTime).isBefore(endTime)) {
        // Check the weather
        context.log("Checking current weather conditions for " + input.location.city + ", "
            + input.location.state + " at " + context.df.currentUtcDateTime + ".");
        const isClear = yield context.df.callActivityAsync("E3_GetIsClear", input.location);

        if (isClear) {
            // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
            context.log("Detected clear weather for " + input.location.city + ", "
                + input.location.state + ". Notifying " + input.phone + ".");

            yield context.df.callActivityAsync("E3_SendGoodWeatherAlert", input.phone);
            break;
        } else {
            // Wait for the next checkpoint
            var nextCheckpoint = moment.utc(context.df.currentUtcDateTime).add(30, 's');
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