using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Crawly
{
    public class ResultsLogger
    {
        private readonly BlockingCollection<string> _blockingCollection = new BlockingCollection<string>(10000);
        private Task _loggingTask;
        public string FilePath { get; }

        public ResultsLogger(string logFile)
        {
            FilePath = logFile;
            _loggingTask = Task.Factory.StartNew(Consume, TaskCreationOptions.LongRunning);
        }

        private void Consume()
        {
            using (StreamWriter outputFile = new StreamWriter(FilePath))
            {
                while (true)
                {
                    outputFile.WriteLine(_blockingCollection.Take());
                }
            }
        }

        public void Log(string message)
        {
            _blockingCollection.Add(message);
        }

    }
}