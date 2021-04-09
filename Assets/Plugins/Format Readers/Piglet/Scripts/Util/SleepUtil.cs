using System.Collections;
using System.Diagnostics;

namespace Piglet
{
    public class SleepUtil
    {
        /// <summary>
        /// A coroutine-based sleep routine that works in
        /// both Edit Mode and Play Mode.
        /// </summary>
        static public IEnumerator SleepEnum(long milliseconds)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < milliseconds)
                yield return null;
        }
    }
}