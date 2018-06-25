const df = require("durable-functions");
const moment = require('moment');

module.exports = df(function*(context) {
    const phoneNumber = context.df.getInput();
    if (!phoneNumber) {
        throw "A phone number input is required.";
    }

    const challengeCode = yield context.df.callActivityAsync("E4_SendSmsChallenge", phoneNumber);

    // The user has 90 seconds to respond with the code they received in the SMS message.
    const expiration = moment.utc(context.df.currentUtcDateTime).add(90, 's');
    const timeoutTask = context.df.createTimer(expiration.toDate());

    let authorized = false;
    for (let i = 0; i <= 3; i++) {
        const challengeResponseTask = context.df.waitForExternalEvent("SmsChallengeResponse");

        const winner = yield context.df.Task.any([challengeResponseTask, timeoutTask]);

        if (winner === challengeResponseTask) {
            // We got back a response! Compare it to the challenge code.
            if (challengeResponseTask.result === challengeCode) {
                authorized = true;
                break;
            }
        } else {
            // Timeout expired
            break;
        }
    }

    if (!timeoutTask.isCompleted) {
        // All pending timers must be complete or canceled before the function exits.
        timeoutTask.cancel();
    }

    return authorized;
});