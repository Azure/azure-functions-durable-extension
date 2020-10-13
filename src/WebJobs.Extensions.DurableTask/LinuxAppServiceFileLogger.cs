// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix.Native;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// TBD>.
    /// </summary>
    public class LinuxAppServiceFileLogger
    {
        private readonly string logFileName;
        private readonly string logFileDirectory;
        private readonly string logFilePath;
        private readonly BlockingCollection<string> buffer;
        private readonly List<string> currentBatch;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task outputTask;

        /// <summary>
        /// TBD.
        /// </summary>
        /// <param name="logFileName">TBD1.</param>
        /// <param name="logFileDirectory">TBD2.</param>
        /// <param name="startOnCreate">TBD3.</param>
        public LinuxAppServiceFileLogger(string logFileName, string logFileDirectory, bool startOnCreate = true)
        {
            this.logFileName = logFileName;
            this.logFileDirectory = logFileDirectory;
            this.logFilePath = Path.Combine(this.logFileDirectory, this.logFileName + ".log");
            this.buffer = new BlockingCollection<string>(new ConcurrentQueue<string>());
            this.currentBatch = new List<string>();
            this.cancellationTokenSource = new CancellationTokenSource();

            if (startOnCreate)
            {
                this.Start();
            }
        }

        // Maximum number of files
        private int MaxFileCount { get; set; } = 3;

        // Maximum size of individual log file in MB
        private int MaxFileSizeMb { get; set; } = 10;

        // Maximum time between successive flushes (seconds)
        private int FlushFrequencySeconds { get; set; } = 30;

        /// <summary>
        /// TBD.
        /// </summary>
        /// <param name="message">TBD 1.</param>
        public virtual void Log(string message)
        {
            try
            {
                this.buffer.Add(message);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Start()
        {
            if (this.outputTask == null)
            {
                this.outputTask = Task.Factory.StartNew(this.ProcessLogQueue, null, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <param name="timeSpan">TBD 1.</param>
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

        private async Task ProcessLogQueue(object state)
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
            Directory.CreateDirectory(this.logFileDirectory);

            var fileInfo = new FileInfo(this.logFilePath);
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
            using (var streamWriter = File.AppendText(filePath))
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

            Syscall.rename(this.logFilePath, this.GetCurrentFileName(DateTime.UtcNow));

            var fileInfoBases = this.ListFiles(this.logFileDirectory, this.logFileName + "*", SearchOption.TopDirectoryOnly);

            if (fileInfoBases.Length >= this.MaxFileCount)
            {
                var oldestFile = fileInfoBases.OrderByDescending(f => f.Name).Last();
                oldestFile.Delete();
            }
        }

        private FileInfo[] ListFiles(string directoryPath, string pattern, SearchOption searchOption)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
            return dirInfo.GetFiles(pattern, searchOption);
        }

        private string GetCurrentFileName(DateTime dateTime)
        {
            return Path.Combine(this.logFileDirectory, $"{this.logFileName}{dateTime:yyyyMMddHHmmss}.log");
        }
    }
}
