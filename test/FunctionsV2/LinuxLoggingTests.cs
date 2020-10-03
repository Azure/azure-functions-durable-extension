// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class LinuxLoggingTests
    {
        // private const string ContainerNameKey = "CONTAINER_NAME";
        // private const string AzureWebsiteInstanceIdKey = "WEBSITE_INSTANCE_ID";
        // private const string FunctionsLogsMountPathKey = "FUNCTIONS_LOGS_MOUNT_PATH";

        internal LinuxAppServiceLogger GetAppServiceLogger(string filePath, int count = 0, int maxMbSize = 1)
        {
            string containerName = "myContainer";
            string tenant = "myTenant";
            string stampName = "myStampName";
            LinuxAppServiceLogger logger = new LinuxAppServiceLogger(
                writeToConsole: true,
                containerName,
                tenant,
                stampName,
                filePath,
                count,
                maxMbSize);
            return logger;
        }

        private (string Name, string Path, string Dir) GenFileNameAndPath(string name)
        {
            string currentWorkingDir = Directory.GetCurrentDirectory();
            string logFileName = name + ".log";
            string directory = Path.Join(currentWorkingDir, name);
            string logFilePath = Path.Join(directory, logFileName);
            return (Name: logFileName, Path: logFilePath, Dir: directory);
        }

        private void GenerateBigLogFile(string fileName)
        {
            var sizeInMB = 1;
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(sizeInMB * 1024 * 1024);
            }
        }

        [Fact]
        public void ArchivesMultipleFiles()
        {
            var logTarget = this.GenFileNameAndPath("ArchivesMultipleFiles");
            LinuxAppServiceLogger logger = this.GetAppServiceLogger(logTarget.Path);
            this.GenerateBigLogFile(logTarget.Path);
            logger.LogFileMaintenance();

            this.GenerateBigLogFile(logTarget.Path);
            logger.LogFileMaintenance();

            string[] files = Directory.GetFiles(logTarget.Dir);
            Assert.Equal(2, files.Length);
            Assert.Contains(logTarget.Path + "1", files);
            Assert.Contains(logTarget.Path + "2", files);
        }

        [Fact]
        public void DeletesArchives()
        {
            var logTarget = this.GenFileNameAndPath("DeletesArchives");
            LinuxAppServiceLogger logger = this.GetAppServiceLogger(logTarget.Path, LinuxAppServiceLogger.MaxArchives);
            for (int count = 1; count < LinuxAppServiceLogger.MaxArchives; count++)
            {
                // We generate an empty archiveFile
                string archiveFilePath = logger.LoggingPath + count;
                this.GenerateBigLogFile(archiveFilePath);
            }

            logger.LogFileMaintenance();
            string[] files = Directory.GetFiles(logTarget.Dir);
            Assert.Single(files);
            Assert.Equal(logger.LoggingPath, files[0]);
        }

        private static Mock<INameResolver> GetNameResolverMock((string Key, string Value)[] settings)
        {
            var mock = new Mock<INameResolver>();
            foreach (var setting in settings)
            {
                mock.Setup(x => x.Resolve(setting.Key)).Returns(setting.Value);
            }

            return mock;
        }
    }

    /*
    private static Mock<INameResolver> GetNameResolverForLinuxDedicated()
    {
        var resolver = GetNameResolverMock(new[]
        { (ContainerNameKey, "val1"),
              (AzureWebsiteInstanceIdKey, "val2"),
            });
        return resolver;
    }

    private static Mock<INameResolver> GetNameResolverForLinuxConsumption()
    {
        var resolver = GetNameResolverMock(new[]
        {
              (FunctionsLogsMountPathKey, "val3"),
            });
        return resolver;
    }
    */
}
