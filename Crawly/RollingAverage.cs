using System;

namespace Crawly
{
    internal class RollingAverage
    {
        private readonly long[] _data;
        private readonly object _lock;
        private readonly int _sampleWidth;
        private long _counter;

        public RollingAverage(int sampleWidth)
        {
            _sampleWidth = sampleWidth;
            _counter = 0;
            _data = new long[sampleWidth];
            _lock = new object();
        }

        public void AddSample(long sample)
        {
            lock (_lock)
            {
                _counter++;
                _data[_counter%_sampleWidth] = sample;
            }
        }

        public long GetAverage()
        {
            long average = 0;
            for (var i = 0; i < Math.Min(_counter, _data.Length); i++)
            {
                average += _data[i]/Math.Min(_counter, _data.Length);
            }
            return average;
        }
    }
}