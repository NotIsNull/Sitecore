using System;
using System.IO;

namespace HI.InstallPackage
{
    public class LogReader : IDisposable
    {
        public string FilePath { get; set; }
        private bool _continueMonitoring;
        private const int SleepTime = 500;

        public LogReader(string filePath)
        {
            FilePath = filePath;
        }

        private FileStream OpenFileStream()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new Exception("FilePath cannot be empty.");
            }

            try
            {
                return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error opening FileStream to {0}", FilePath), ex);
            }
        }

        public long GetFileLength()
        {
            long fileLength;
            using (var stream = OpenFileStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    fileLength = reader.BaseStream.Length;
                }
            }

            return fileLength;
        }

        public string GetUpdates(long startingPosition, out long fileLength)
        {
            using (var stream = OpenFileStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    fileLength = reader.BaseStream.Length;

                    // no change
                    if (fileLength == startingPosition)
                    {
                        return string.Empty;
                    }

                    reader.BaseStream.Seek(startingPosition, SeekOrigin.Begin);
                    return reader.ReadToEnd();
                }
            }
        }

        #region Tail

        /// <summary>
        /// Opens a StreamReader for the file. Checks every [SleepTime] ms. Fires OnLogChanged when new content is added to the end of the file.
        /// </summary>
        public void BeginLogMonitor()
        {
            // prevent simultaneous monitors from the same instance
            if (_continueMonitoring)
            {
                return;
            }

            _continueMonitoring = true;
            using (var stream = OpenFileStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    var fileLength = reader.BaseStream.Length;

                    while (_continueMonitoring && reader.BaseStream.CanRead)
                    {
                        System.Threading.Thread.Sleep(SleepTime);

                        if (reader.BaseStream.Length == fileLength)
                        {
                            continue;
                        }

                        reader.BaseStream.Seek(fileLength, SeekOrigin.Begin);

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            OnLogChanged(new LogChangedEventArgs(fileLength, line));
                        }

                        fileLength = reader.BaseStream.Position;
                    }
                }
            }
        }

        public void EndLogMonitor()
        {
            _continueMonitoring = false;
        }

        protected virtual void OnLogChanged(LogChangedEventArgs e)
        {
            if (LogChanged != null)
            {
                LogChanged(this, e);
            }
        }

        public event LogChangedEventHandler LogChanged;

        public delegate void LogChangedEventHandler(object sender, LogChangedEventArgs e);

        public class LogChangedEventArgs : EventArgs
        {
            public LogChangedEventArgs(long fileLength, string newContent)
            {
                FileLength = fileLength;
                NewContent = newContent;
            }

            public long FileLength { get; set; }
            public string NewContent { get; set; }
        }

        #endregion

        public void Dispose()
        {
            FilePath = string.Empty;
            _continueMonitoring = false;
        }
    }
}
