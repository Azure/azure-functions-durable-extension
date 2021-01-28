// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The File logger for linux dedicated. Manages file rolling and is concurrency-safe.
    /// This is copied over from the azure-funtions-host codebase here:
    /// https://github.com/Azure/azure-functions-host/blob/35cf323fa3464a08b410a518bcab006e801301fe/src/WebJobs.Script.WebHost/Diagnostics/LinuxAppServiceFileLogger.cs
    /// We have modified their implementation to utilize syscall.rename instead of File.Move during file rolling.
    /// This change is necessary for older versions of fluent-bit, our logging infrastructure in linux dedicated, to properly deal with logfile archiving.
    /// </summary>
    public class LinuxAppServiceFileLogger
    {
        private readonly string logFileName;
        private readonly string logFileDirectory;
        private readonly string logFilePath;
        private readonly string archiveFilePath;
        private readonly BlockingCollection<string> buffer;
        private readonly List<string> currentBatch;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task outputTask;

        /// <summary>
        /// The File logger for linux dedicated. Manages file rolling and is concurrency-safe.
        /// </summary>
        /// <param name="logFileName">Name of target logfile.</param>
        /// <param name="logFileDirectory">Directory of target logfile.</param>
        /// <param name="startOnCreate">Whether or not to start monitoring the write buffer at initialization time.</param>
        public LinuxAppServiceFileLogger(string logFileName, string logFileDirectory, bool startOnCreate = true)
        {
            this.logFileName = logFileName;
            this.logFileDirectory = logFileDirectory;
            this.logFilePath = Path.Combine(this.logFileDirectory, this.logFileName);
            this.archiveFilePath = this.logFilePath + "1";
            this.buffer = new BlockingCollection<string>(new ConcurrentQueue<string>());
            this.currentBatch = new List<string>();
            this.cancellationTokenSource = new CancellationTokenSource();

            if (startOnCreate)
            {
                this.Start();
            }
        }

        // Maximum size of individual log file in MB
        private int MaxFileSizeMb { get; set; } = 10;

        // Maximum time between successive flushes (seconds)
        private int FlushFrequencySeconds { get; set; } = 30;

        /// <summary>
        /// Log a string.
        /// </summary>
        /// <param name="message">Message to log.</param>
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
        /// Flushes the write buffer, stops writing to logfile afterwards.
        /// </summary>
        /// <param name="timeSpan">Timeout in milliseconds for flushing task.</param>
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

            await this.InternalProcessLogQueue();

            // ReSharper disable once FunctionNeverReturns
        }

        // internal for unittests (in func host)
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
            // If the directory already exists, this does nothing
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
#if !FUNCTIONS_V1
            rename(this.logFilePath, this.archiveFilePath);
#endif

        }

#if !FUNCTIONS_V1
        [DllImport("libc", SetLastError = true)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern int rename(string oldPath, string newPath);
#pragma warning restore SA1300 // Element should begin with upper-case letter
#endif
    }
}
