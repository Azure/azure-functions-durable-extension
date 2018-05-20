module.exports = function(context) {
    context.done(null, `Hello ${context.bindings.name}!`);
};