namespace Points.ViewModels.Home
{
    internal sealed class HomeLoadedRangeTracker
    {
        private readonly object _sync = new();
        private DateTime _start;
        private DateTime _end;
        private bool _hasLoadedRange;

        public bool IsLoaded(DateTime start, DateTime end)
        {
            lock (_sync)
            {
                return _hasLoadedRange
                    && _start == start
                    && _end == end;
            }
        }

        public void MarkLoaded(DateTime start, DateTime end)
        {
            lock (_sync)
            {
                _start = start;
                _end = end;
                _hasLoadedRange = true;
            }
        }
    }
}
