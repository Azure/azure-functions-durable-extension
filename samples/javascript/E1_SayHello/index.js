module.exports = async function(context) {
    return `Hello ${context.bindings.name}!`;
};
