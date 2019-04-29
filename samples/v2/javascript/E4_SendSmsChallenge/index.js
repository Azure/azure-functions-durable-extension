const seedrandom = require("seedrandom");
const uuidv1 = require("uuid/v1");

// Get a random number generator with a random seed (not time-based)
const rand = seedrandom(uuidv1());

module.exports = async function (context, phoneNumber) {
    const challengeCode = Math.floor(rand() * 10000);

    context.log(`Sending verification code ${challengeCode} to ${phoneNumber}.`);

    context.bindings.message = {
        body: `Your verification code is ${challengeCode.toPrecision(4)}`,
        to: phoneNumber
    };

    return challengeCode;
};
