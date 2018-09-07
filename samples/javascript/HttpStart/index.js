const uuidv1 = require("uuid/v1");

module.exports = function (context, req) {
    const id = uuidv1();

    let startArgs = [{
        FunctionName: req.params.functionName,
        Input: req.body,
        InstanceId: id
    }];

    context.bindings.starter = startArgs;

    context.done(null, {status: 202, body: id});
};