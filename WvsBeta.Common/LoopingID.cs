using System.Threading;

namespace WvsBeta.Common
{
    public class LoopingID
    {
        private int _current;
        public int Current => _current;
        public int Minimum { get; }
        public int Maximum { get; }

        public LoopingID() : this(1, int.MaxValue)
        {
        }

        public LoopingID(int min, int max)
        {
            Minimum = _current = min;
            Maximum = max;
        }

        public int NextValue()
        {
            int ret = Current;
            if (Current == Maximum)
            {
                Reset();
            }
            else
            {
                Interlocked.Increment(ref _current);
            }
            return ret;
        }

        public void Reset() => Reset(Minimum);

        public void Reset(int val) => Interlocked.Exchange(ref _current, val);
    }
}
