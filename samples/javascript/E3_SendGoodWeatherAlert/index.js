module.exports = function(context, phoneNumber) {
    context.bindings.message = {
        body: `The weather's clear outside! Go take a walk!`,
        to: phoneNumber
    };

    context.done();
};