const fs = require("fs");
const path = require("path");
const storage = require("azure-storage");

module.exports = function (context, filePath) {
    const container = "backups";
    const root = path.parse(filePath).root;
    const blobPath = filePath
        .substring(root.length)
        .replace("\\", "/");
    const outputLocation = `backups/${blobPath}`;
    const blobService = storage.createBlobService();

    blobService.createContainerIfNotExists(container, (error) => {
        if (error) {
            throw error;
        }

        fs.stat(filePath, function (error, stats) {
            if (error) {
                throw error;
            }
            context.log(`Copying '${filePath}' to '${outputLocation}'. Total bytes = ${stats.size}.`);

            const readStream = fs.createReadStream(filePath);

            blobService.createBlockBlobFromStream(container, blobPath, readStream, stats.size, function (error) {
                if (error) {
                    throw error;
                }

                context.done(null, stats.size);
            });
        });
    });
};