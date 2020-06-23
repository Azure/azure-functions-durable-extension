// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics
{
    public class LinuxDedicatedLogger
    {
        private readonly string logFileName;
        private readonly string logFileDirectory;
        private readonly string logFilePath;
        private readonly BlockingCollection<string> buffer;
        private readonly List<string> currentBatch;
        private readonly IFileSystem fileSystem;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task outputTask;

        public LinuxDedicatedLogger(string logFileName, string logFileDirectory, IFileSystem fileSystem, bool startOnCreate = true)
        {
            this.logFileName = logFileName;
            this.logFileDirectory = logFileDirectory;
            this.logFilePath = Path.Combine(this.logFileDirectory, this.logFileName + ".log");
            this.buffer = new BlockingCollection<string>(new ConcurrentQueue<string>());
            this.currentBatch = new List<string>();
            this.fileSystem = fileSystem;
            this.cancellationTokenSource = new CancellationTokenSource();

            if (startOnCreate)
            {
                this.Start();
            }
        }

        // Maximum number of files
        public int MaxFileCount { get; set; } = 3;

        // Maximum size of individual log file in MB
        public int MaxFileSizeMb { get; set; } = 10;

        // Maximum time between successive flushes (seconds)
        public int FlushFrequencySeconds { get; set; } = 30;

        public virtual void Log(string message)
        {
            try
            {
                buffer.Add(message);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void Start()
        {
            if (this.outputTask == null)
            {
                this.outputTask = Task.Factory.StartNew(this.ProcessLogQueue, null, TaskCreationOptions.LongRunning);
            }
        }

        public void Stop(TimeSpan timeSpan)
        {
            this.cancellationTokenSource.Cancel();

            try
            {
                this.outputTask?.Wait(timeSpan);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public virtual async Task ProcessLogQueue(object state)
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                await this.InternalProcessLogQueue();
                await Task.Delay(TimeSpan.FromSeconds(this.FlushFrequencySeconds), this.cancellationTokenSource.Token).ContinueWith(task => { });
            }
            // ReSharper disable once FunctionNeverReturns
        }

        // internal for unittests
        internal async Task InternalProcessLogQueue()
        {
            string currentMessage;
            while (this.buffer.TryTake(out currentMessage))
            {
                this.currentBatch.Add(currentMessage);
            }

            if (this.currentBatch.Any())
            {
                try
                {
                    await this.WriteLogs(this.currentBatch);
                }
                catch (Exception)
                {
                    // Ignored
                }

                this.currentBatch.Clear();
            }
        }

        private async Task WriteLogs(IEnumerable<string> currentBatch)
        {
            this.fileSystem.Directory.CreateDirectory(this.logFileDirectory);

            var fileInfo = this.fileSystem.FileInfo.FromFileName(this.logFilePath);
            if (fileInfo.Exists)
            {
                if (fileInfo.Length / (1024 * 1024) >= this.MaxFileSizeMb)
                {
                    this.RollFiles();
                }
            }

            await this.AppendLogs(this.logFilePath, currentBatch);
        }

        private async Task AppendLogs(string filePath, IEnumerable<string> logs)
        {
            using (var streamWriter = this.fileSystem.File.AppendText(filePath))
            {
                foreach (var log in logs)
                {
                    await streamWriter.WriteLineAsync(log);
                }
            }
        }

        private void RollFiles()
        {
            // Rename current file to older file.
            // Empty current file.
            // Delete oldest file if exceeded configured max no. of files.

            this.fileSystem.File.Move(this.logFilePath, this.GetCurrentFileName(DateTime.UtcNow));

            var fileInfoBases = this.ListFiles(this.logFileDirectory, this.logFileName + "*", SearchOption.TopDirectoryOnly);

            if (fileInfoBases.Length >= this.MaxFileCount)
            {
                var oldestFile = fileInfoBases.OrderByDescending(f => f.Name).Last();
                oldestFile.Delete();
            }
        }

        private IFileInfo[] ListFiles(string directoryPath, string pattern, SearchOption searchOption)
        {
            return this.fileSystem.DirectoryInfo.FromDirectoryName(directoryPath).GetFiles(pattern, searchOption);
        }

        public string GetCurrentFileName(DateTime dateTime)
        {
            return Path.Combine(this.logFileDirectory, $"{this.logFileName}{dateTime:yyyyMMddHHmmss}.log");
        }
    }
}
