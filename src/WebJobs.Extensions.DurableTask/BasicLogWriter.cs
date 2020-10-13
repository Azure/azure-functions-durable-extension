// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
#if !FUNCTIONS_V1
using Mono.Unix.Native;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// BasicLogWriter:
    /// Simple file writer (which doesn't hold the handle open, with basic rollover).
    /// </summary>
    public class BasicLogWriter
    {
        private long fileSize;

        /// <summary>
        /// Simple log writer:
        /// Rolls over at specified size (bytes)
        /// Maximum of 2 files retained.
        /// </summary>
        /// <param name="logFolder">Directory where the logfile will be created.</param>
        /// <param name="fileName">Log file name.</param>
        /// <param name="rollOverSize">Maximum size of file (in bytes) before archiving it.</param>
        public BasicLogWriter(string logFolder, string fileName, int rollOverSize)
        {
            this.LogFolder = logFolder;
            this.RollOverSize = rollOverSize;
            this.FileName = fileName;
            this.Initialize();
        }

        internal long RollOverSize { get; private set; }

        internal string LogFolder { get; private set; }

        internal string FileName { get; private set; }

        internal bool BufferingEnabled { get; set; }

        private string LogFile
        {
            get
            {
                return Path.Combine(this.LogFolder, this.FileName);
            }
        }

        private string LogFileBackup
        {
            get
            {
                return Path.Combine(this.LogFolder, this.FileName + ".1");
            }
        }

        /// <summary>
        /// Writes string argument to the log file.
        /// </summary>
        /// <param name="input">string to log.</param>
        public void WriteLog(string input)
        {
            this.WriteDataInternal(input);
        }

        private void WriteDataInternal(string input)
        {
            try
            {
                // Roll-over if needed
                if (this.fileSize >= this.RollOverSize)
                {
                    if (File.Exists(this.LogFileBackup))
                    {
                        try
                        {
                            File.Delete(this.LogFileBackup);
                        }
                        catch (Exception)
                        {
                        }
                    }
#if !FUNCTIONS_V1
                    Syscall.rename(this.LogFile, this.LogFileBackup);
                    this.fileSize = 0;
#endif
                }
            }
            catch (Exception)
            {
            }

            try
            {
                byte[] dataToWrite = Encoding.UTF8.GetBytes(input);
                this.fileSize += dataToWrite.Length;

                // Write the data out
                AppendAllBytes(this.LogFile, dataToWrite);
            }
            catch (Exception)
            {
            }
        }

        private static void AppendAllBytes(string fileName, byte[] data)
        {
            using (FileStream fileStream = new FileStream(fileName, File.Exists(fileName) ? FileMode.Append : FileMode.OpenOrCreate, FileAccess.Write))
            {
                fileStream.Write(data, 0, data.Length);
                fileStream.Close();
            }
        }

        private void Initialize()
        {
            if (File.Exists(this.LogFile))
            {
                this.fileSize = new FileInfo(this.LogFile).Length;
            }
            else
            {
                this.fileSize = 0;
            }
        }
    }
}
