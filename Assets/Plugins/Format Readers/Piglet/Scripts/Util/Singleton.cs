namespace Piglet
{
    /// <summary>
    /// Inherit from this base class to create a singleton,
    /// e.g. public class MyClassName : Singleton<MyClassName> {}
    /// </summary>
    public class Singleton<T> where T : new()
    {
        private static readonly object _lock = new object();
        private static T _instance;

        public static T Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new T();

                    return _instance;
                }
            }
        }
    }
}