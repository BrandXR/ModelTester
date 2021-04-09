using System.Diagnostics;

namespace Piglet
{
    /// <summary>
    /// This class tracks the of amount time that has passed since
    /// the Piglet import coroutines have last yielded to the main
    /// Unity thread (with a `yield return` statement). This is used
    /// to ensure glTF import operations to do not interrupt the
    /// main Unity thread for too long, causing undesirable drops
    /// in frame rate.
    /// </summary>
    public class YieldTimer : Singleton<YieldTimer>
    {
        /// <summary>
        /// If any operation takes longer than this number
        /// of milliseconds, the active coroutine should yield
        /// at the next possible opportunity.
        /// </summary>
        protected readonly int MillisecondsPerYield;

        /// <summary>
        /// Timer used to track the amount of time since Piglet
        /// last yielded to the main Unity thread.
        /// </summary>
        protected readonly Stopwatch Stopwatch;

        /// <summary>
        /// Returns true when we have exceeded the maximum amount
        /// of time that Piglet should run before yielding control
        /// back to the main Unity thread.
        /// </summary>
        public bool Expired
        {
            get
            {
                return Stopwatch.ElapsedMilliseconds > MillisecondsPerYield;
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public YieldTimer()
        {
            MillisecondsPerYield = 10;
            Stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Restart the timer. This should be called immediately
        /// following a `yield return` statement.
        /// </summary>
        public void Restart()
        {
            Stopwatch.Restart();
        }
    }
}