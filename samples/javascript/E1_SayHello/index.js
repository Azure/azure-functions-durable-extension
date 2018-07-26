// This is an activity called by the chaining pattern sample orchestrator.

module.exports = function(context) {
    context.done(null, `Hello ${context.bindings.name}!`);
};