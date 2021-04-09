using System.Collections;
using System.Collections.Generic;

namespace Piglet
{
    /// <summary>
    /// A wrapper class that executes a set of coroutines ("tasks")
    /// in interleaved fashion (i.e. "in parallel"). All tasks must
    /// enumerate the same type, determined by the type parameter "T".
    /// </summary>
    public class InterleavedTaskSet<T> : IEnumerable<IEnumerator<T>>
    {
        /// <summary>
        /// Member coroutines to be executed in round-robin fashion
        /// when MoveNext() is called.
        /// </summary>
        public readonly List<IEnumerator<T>> Tasks;
        
        /// <summary>
        /// The integer indices of completed tasks in the Tasks list.
        /// </summary>
        public readonly HashSet<int> CompletedTasks;

        /// <summary>
        /// Prototype for callbacks that are invoked when an individual
        /// task has completed.
        /// </summary>
        /// <param name="taskIndex">
        /// the index of the task (IEnumerator<T>) that has completed
        /// </param>
        /// <param name="result">
        /// the most recent value return by the task
        /// (i.e. IEnumerator<T>.Current)
        /// </param>
        public delegate void CompletedCallback(int taskIndex, T result);

        /// <summary>
        /// Callback(s) that are invoked when a individual task has completed.
        /// The callbacks are passed the index of the completed task
        /// and the most recently returned value (`Current`) as parameters.
        /// </summary>
        public CompletedCallback OnCompleted;
        
        /// <summary>
        /// The number of tasks that has completed thus far.
        /// </summary>
        public int NumCompleted
        {
            get { return CompletedTasks.Count; }
        }
        
        /// <summary>
        /// The index of the next task that will advanced by calling MoveNext().
        /// </summary>
        private int _taskIndex;

        /// <summary>
        /// Constructor.
        /// </summary>
        public InterleavedTaskSet()
        {
            Tasks = new List<IEnumerator<T>>();
            CompletedTasks = new HashSet<int>();
            _taskIndex = 0;
        }

        /// <summary>
        /// Add a task (coroutine) to the set of tasks to be executed
        /// by this object.
        /// </summary>
        public void Add(IEnumerator<T> task)
        {
            Tasks.Add(task);            
        }
        
        /// <summary>
        /// Advance execution of one member task (coroutine) by
        /// calling MoveNext() on it. Execution of tasks is interleaved
        /// in round-robin fashion.
        /// </summary>
        public bool MoveNext()
        {
            bool moveNext = false;
            
            while (!moveNext && NumCompleted < Tasks.Count)
            {
                if (!CompletedTasks.Contains(_taskIndex))
                {
                    moveNext = Tasks[_taskIndex].MoveNext();

                    if (!moveNext)
                    {
                        CompletedTasks.Add(_taskIndex);
                        OnCompleted?.Invoke(_taskIndex, Tasks[_taskIndex].Current);
                    }
                }

                _taskIndex++;
                if (_taskIndex >= Tasks.Count)
                    _taskIndex = 0;
            }

            return moveNext;
        }

        /// <summary>
        /// Get enumerator over member tasks (coroutines).
        /// </summary>
        public IEnumerator<IEnumerator<T>> GetEnumerator()
        {
            return Tasks.GetEnumerator();
        }

        /// <summary>
        /// Get enumerator over member tasks (coroutines).
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
