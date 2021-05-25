module.exports = function (input){
    if (typeof input !== "number"){
        throw new Error(`Input was expected to be of type number but received type: ${typeof input}`);
    }

    if (typeof input < 1){
        throw new Error(`Input was expected to be larger of equal to 1 but received: ${input}`);
    }
}