const seedrandom = require("seedrandom");
const uuidv1 = require("uuid/v1");

module.exports = function (context, phoneNumber) {
    // Get a random number generator with a random seed (not time-based)
    const rand = seedrandom(uuidv1());
    const challengeCode = Math.floor(rand() * 10000);

    context.log(`Sending verification code ${challengeCode} to ${phoneNumber}.`);

    context.bindings.message = {
        body: `Your verification code is ${challengeCode.toPrecision(4)}`,
        to: phoneNumber
    };

    context.done(null, challengeCode);
};