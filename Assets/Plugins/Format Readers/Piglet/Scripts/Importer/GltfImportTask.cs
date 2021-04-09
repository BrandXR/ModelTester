using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Piglet
{
	/// <summary>
	/// Sequentially executes a set of subtasks (coroutines) in order
	/// to import a glTF model. Each subtask corresponds to importing a
	/// different type of glTF entity (buffers, textures, materials,
	/// meshes, etc.).
	///
	/// In principle, this class could be replaced by a simple wrapper
	/// coroutine method that iterates through the subtask coroutines in
	/// sequence.  However, this class provides the additional abilities
	/// to: (1) abort the import process, (2) specify user-defined
	/// callbacks for abortion/exception/completion, and (3) check the
	/// current execution state of the import task
	/// (running/aborted/exception/completed).
	/// </summary>
	public class GltfImportTask
	{
		/// <summary>
		/// The possible execution states of an import task.
		/// </summary>
		public enum ExecutionState
		{
			Running,
			Aborted,
			Exception,
			Completed
		};

		/// <summary>
		/// The current execution state of this import task (e.g. aborted).
		/// </summary>
		public ExecutionState State;

		/// <summary>
		/// Callback(s) that are invoked to report
		/// intermediate progress during a glTF import.
		/// </summary>
		public GltfImporter.ProgressCallback OnProgress;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// a glTF import is aborted by the user.
		/// </summary>
		public delegate void AbortedCallback();

		/// <summary>
		/// Callback(s) that are invoked when the glTF import is
		/// aborted by the user. This provides a
		/// useful hook for cleaning up the aborted import task.
		/// </summary>
		public AbortedCallback OnAborted;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// an exception occurs during a glTF import.
		/// </summary>
		public delegate void ExceptionCallback(Exception e);

		/// <summary>
		/// Callback(s) that are invoked when an exception
		/// is thrown during a glTF import. This provides a
		/// useful hook for cleaning up a failed import task
		/// and/or presenting error messages to the user.
		/// </summary>
		public ExceptionCallback OnException;

		/// <summary>
		/// If true, an exception will be rethrown after
		/// being passed to user-defined exception callbacks
		/// in OnException.
		/// </summary>
		public bool RethrowExceptionAfterCallbacks;

		/// <summary>
		/// Prototype for callbacks that are invoked when
		/// the glTF import task has successfully completed.
		/// </summary>
		public delegate void CompletedCallback(GameObject importedModel);

		/// <summary>
		/// Callback(s) that are invoked when the glTF import
		/// successfully completes.  The root GameObject of
		/// the imported model is passed as argument to these
		/// callbacks.
		/// </summary>
		public CompletedCallback OnCompleted;

		/// <summary>
		/// The list of subtasks (coroutines) that make up
		/// the overall glTF import task.
		/// </summary>
		List<IEnumerator> _tasks;

		/// <summary>
		/// Profiling data recorded for each call to
		/// IEnumerator.MoveNext().  This data is
		/// used to help determine which subtasks
		/// create the longest interruptions to
		/// the main Unity thread.
		/// </summary>
		private struct ProfilingRecord
		{
			/// <summary>
			/// The type of the IEnumerator that
			/// we called MoveNext() on.
			///
			/// By happy circumstance, the type of
			/// the IEnumerator contains the name of the
			/// method that generated the IEnumerator, and
			/// this allows us to distinguish between the
			/// different import subtasks (e.g. LoadTextures)
			/// in the reported profiling data.
			///
			/// This is a hack that is dependent
			/// on the particular type names that Mono
			/// auto-generates for IEnumerators, but it is
			/// a nice shortcut and works well enough for now.
			/// </summary>
			public Type TaskType;

			/// <summary>
			/// Number of milliseconds it took to
			/// execute IEnumerator.MoveNext().  This
			/// represents an interruption to the main
			/// Unity thread and should be kept as
			/// short as possible.
			/// </summary>
			public long Milliseconds;
		}

		/// <summary>
		/// Profiling data for calls to IEnumerator.MoveNext()
		/// during a glTF import.
		/// </summary>
		private List<ProfilingRecord> _profilingData;

		/// <summary>
		/// Stopwatch used to profile calls to IEnumerator.MoveNext().
		/// </summary>
		private Stopwatch _stopwatch = new Stopwatch();

		public GltfImportTask()
		{
			_tasks = new List<IEnumerator>();
			_profilingData = new List<ProfilingRecord>();
			State = ExecutionState.Running;
			RethrowExceptionAfterCallbacks = true;
		}

		/// <summary>
		/// Log profiling results regarding calls to IEnumerator.MoveNext().
		/// </summary>
		public void LogProfilingData()
		{
			foreach (var record in _profilingData)
				Debug.LogFormat("{0}\t{1}",
					record.TaskType, record.Milliseconds);
		}

		/// <summary>
		/// Report the longest time to execute a single
		/// task step by calling MoveNext().
		/// </summary>
		public long LongestStepInMilliseconds()
		{
			long max = 0;
			foreach (var record in _profilingData)
			{
				if (record.Milliseconds > max)
					max = record.Milliseconds;
			}
			return max;
		}

		/// <summary>
		/// Add a subtask to the front of the subtask list.
		/// </summary>
		public void PushTask(IEnumerator task)
		{
			_tasks.Insert(0, task);
		}

		/// <summary>
		/// Add a subtask to be executed during the import process.
		/// Subtasks are typically used for importing different
		/// types of glTF entities (e.g. buffers, textures, meshes).
		/// Subtasks are executed in the order that they are added.
		/// </summary>
		public void AddTask(IEnumerator task)
		{
			_tasks.Add(task);
		}

		/// <summary>
		/// Add a subtask to be executed during the import process.
		/// Subtasks are typically used for importing different
		/// types of glTF entities (e.g. buffers, textures, meshes).
		/// Subtasks are executed in the order that they are added.
		/// </summary>
		public void AddTask(IEnumerable task)
		{
			_tasks.Add(task.GetEnumerator());
		}

		/// <summary>
		/// Abort this import task.
		/// </summary>
		public void Abort()
		{
			State = ExecutionState.Aborted;
			OnAborted?.Invoke();
			Clear();
		}

		/// <summary>
		/// Clear the list of subtasks.
		/// </summary>
		public void Clear()
		{
			_tasks.Clear();
		}

		/// <summary>
		/// Advance execution of the current subtask by a single step.
		/// </summary>
		public bool MoveNext()
		{
			if (State != ExecutionState.Running)
				return false;

			bool moveNext = false;
			object current = null;
			try
			{
				while (!moveNext && _tasks.Count > 0)
				{
					_stopwatch.Restart();
					moveNext = _tasks[0].MoveNext();
					_stopwatch.Stop();

					_profilingData.Add(new ProfilingRecord {
						TaskType = _tasks[0].GetType(),
						Milliseconds = _stopwatch.ElapsedMilliseconds
					});

					current = _tasks[0].Current;
					if (!moveNext)
						_tasks.RemoveAt(0);
				}
			}
			catch (Exception e)
			{
				State = ExecutionState.Exception;
				OnException?.Invoke(e);
				Clear();

				if (RethrowExceptionAfterCallbacks)
					throw;

				return false;
			}

			if (_tasks.Count == 0)
			{
				State = ExecutionState.Completed;
				OnCompleted?.Invoke((GameObject)current);
				Clear();
			}

			return moveNext;
		}
	}
}