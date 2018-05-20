const readdirp = require("readdirp");

module.exports = function (context, rootDirectory) {
    context.log(`Searching for files under '${rootDirectory}'...`);
    const allFilePaths = [];

    readdirp(
        {root: rootDirectory, entryType: 'all'},
        function (fileInfo) {
            if (!fileInfo.stat.isDirectory()) {
                allFilePaths.push(fileInfo.fullPath);
            }
        },
        function (err, res) {
            if (err) {
                throw err;
            }

            context.log(`Found ${allFilePaths.length} under ${rootDirectory}.`);
            context.done(null, allFilePaths);
        }
    );
};