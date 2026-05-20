// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Timing;

namespace osu.Framework.Threading
{
    /// <summary>
    /// Marshals delegates to run from the Scheduler's base thread in a threadsafe manner
    /// </summary>
    public class Scheduler
    {
        // ConcurrentQueue allows producer threads to enqueue without any lock, eliminating
        // the contention that the previous Monitor-based Queue caused when audio/input threads
        // fire AddDelayed/Add at high frequency (e.g. 1 kHz input polling).
        private readonly ConcurrentQueue<ScheduledDelegate> runQueue = new ConcurrentQueue<ScheduledDelegate>();

        // ConcurrentQueue.Count is O(n); maintain our own O(1) counter via Interlocked.
        private int runQueueCount;

        private readonly List<ScheduledDelegate> timedTasks = new List<ScheduledDelegate>();
        private readonly List<ScheduledDelegate> perUpdateTasks = new List<ScheduledDelegate>();

        // Maps each once-delegate to the live ScheduledDelegate in runQueue so AddOnce<T>
        // can update its Data in O(1) (dict lookup) rather than the previous O(n) queue scan.
        // Uses default Delegate equality (same method + same target) which matches the original
        // sd.Task == task semantics; reference equality would break method-group dedup.
        private readonly ConcurrentDictionary<Delegate, ScheduledDelegate> runQueueOnceSet =
            new ConcurrentDictionary<Delegate, ScheduledDelegate>();

        private readonly Func<bool>? isCurrentThread;

        private IClock? clock;

        private double currentTime => clock?.CurrentTime ?? 0;

        private readonly Lock queueLock = new();

        internal const int LOG_EXCESSSIVE_QUEUE_LENGTH_INTERVAL = 1000;

        /// <summary>
        /// Whether there are any tasks queued to run (including delayed tasks in the future).
        /// </summary>
        public bool HasPendingTasks => TotalPendingTasks > 0;

        /// <summary>
        /// The total tasks this scheduler instance has run.
        /// </summary>
        public int TotalTasksRun { get; private set; }

        /// <summary>
        /// The total number of <see cref="ScheduledDelegate"/>s tracked by this instance for future execution.
        /// </summary>
        internal int TotalPendingTasks => Volatile.Read(ref runQueueCount) + timedTasks.Count + perUpdateTasks.Count;

        /// <summary>
        /// The base thread is assumed to be the thread on which the constructor is run.
        /// </summary>
        public Scheduler()
            : this(null, new StopwatchClock(true))
        {
            var constructedThread = Thread.CurrentThread;
            isCurrentThread = () => Thread.CurrentThread == constructedThread;
        }

        /// <summary>
        /// The base thread is assumed to be the thread on which the constructor is run.
        /// </summary>
        public Scheduler(Func<bool>? isCurrentThread, IClock? clock)
        {
            this.isCurrentThread = isCurrentThread;
            this.clock = clock;
        }

        public void UpdateClock(IClock newClock)
        {
            ArgumentNullException.ThrowIfNull(newClock);

            if (newClock == clock)
                return;

            lock (queueLock)
            {
                if (clock == null)
                {
                    // This is the first time we will get a valid time, so assume this is the
                    // reference point everything scheduled so far starts from.
                    foreach (var s in timedTasks)
                        s.ExecutionTime += newClock.CurrentTime;
                }

                clock = newClock;
            }
        }

        /// <summary>
        /// Returns whether we are on the main thread or not.
        /// </summary>
        internal bool IsMainThread => isCurrentThread?.Invoke() ?? true;

        private readonly List<ScheduledDelegate> tasksToSchedule = new List<ScheduledDelegate>();
        private readonly List<ScheduledDelegate> tasksToRemove = new List<ScheduledDelegate>();

        /// <summary>
        /// Run any pending work tasks.
        /// </summary>
        /// <returns>The number of tasks that were run.</returns>
        public int Update()
        {
            bool hasTimedTasks = timedTasks.Count > 0;
            bool hasPerUpdateTasks = perUpdateTasks.Count > 0;

            if (hasTimedTasks || hasPerUpdateTasks) // avoid taking out a lock if there are no items.
            {
                lock (queueLock)
                {
                    queueTimedTasks();
                    queuePerUpdateTasks();
                }
            }

            // O(1) read via Interlocked counter — ConcurrentQueue.Count is O(n).
            int countToRun = Volatile.Read(ref runQueueCount);

            if (countToRun == 0)
                return 0; // avoid dequeue overhead if there are no items.

            int countRun = 0;

            while (getNextTask(out ScheduledDelegate? sd))
            {
                sd.RunTaskInternal();

                TotalTasksRun++;

                if (++countRun == countToRun)
                    break;
            }

            return countRun;
        }

        private void queueTimedTasks()
        {
            // Already checked before this method is called, but helps with path prediction?
            if (timedTasks.Count != 0)
            {
                double currentTimeLocal = currentTime;

                foreach (var sd in timedTasks)
                {
                    if (sd.ExecutionTime <= currentTimeLocal)
                    {
                        tasksToRemove.Add(sd);

                        if (sd.Cancelled) continue;

                        if (sd.RepeatInterval == 0)
                        {
                            // handling of every-frame tasks is slightly different to reduce overhead.
                            perUpdateTasks.Add(sd);
                            continue;
                        }

                        if (sd.RepeatInterval > 0)
                        {
                            if (timedTasks.Count > LOG_EXCESSSIVE_QUEUE_LENGTH_INTERVAL)
                                throw new ArgumentException("Too many timed tasks are in the queue!");

                            // schedule the next repeat of the task.
                            sd.SetNextExecution(currentTimeLocal);
                            tasksToSchedule.Add(sd);
                        }

                        if (!sd.Completed) enqueue(sd);
                    }
                }

                if (tasksToRemove.Count > 0)
                {
                    if (tasksToRemove.Count > 2)
                    {
                        // For larger batch removals, build a reference-equality set for O(1) per-element
                        // lookup so that RemoveAll's single-pass scan is O(n) total, not O(n*m).
                        var toRemoveSet = new HashSet<ScheduledDelegate>(tasksToRemove, ReferenceEqualityComparer.Instance);
                        timedTasks.RemoveAll(t => toRemoveSet.Contains(t));
                    }
                    else
                    {
                        foreach (var t in tasksToRemove)
                            timedTasks.Remove(t);
                    }

                    tasksToRemove.Clear();
                }

                foreach (var t in tasksToSchedule)
                    timedTasks.AddInPlace(t);

                tasksToSchedule.Clear();
            }
        }

        private void queuePerUpdateTasks()
        {
            // Already checked before this method is called, but helps with path prediction?
            if (perUpdateTasks.Count != 0)
            {
                for (int i = 0; i < perUpdateTasks.Count; i++)
                {
                    ScheduledDelegate task = perUpdateTasks[i];

                    task.SetNextExecution(null);

                    if (task.Cancelled)
                    {
                        perUpdateTasks.RemoveAt(i--);
                        continue;
                    }

                    enqueue(task);
                }
            }
        }

        private bool getNextTask([NotNullWhen(true)] out ScheduledDelegate? task)
        {
            // ConcurrentQueue.TryDequeue is lock-free. The consumer thread (Update) is the
            // only dequeuer, so this is safe without additional synchronisation.
            if (runQueue.TryDequeue(out task))
            {
                Interlocked.Decrement(ref runQueueCount);
                // Remove from the once-set so the same delegate can be re-queued next frame.
                // Use OnceKey (set by all AddOnce paths) rather than task.Task, because
                // ScheduledDelegateWithData<T> hides the base Task field and leaves it null.
                if (task.OnceKey != null)
                    runQueueOnceSet.TryRemove(task.OnceKey, out _);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cancel any pending work tasks.
        /// </summary>
        public void CancelDelayedTasks()
        {
            lock (queueLock)
            {
                foreach (var t in timedTasks)
                    t.Cancel();
                timedTasks.Clear();
            }
        }

        /// <summary>
        /// Add a task to be scheduled.
        /// </summary>
        /// <remarks>If scheduled, the task will be run on the next <see cref="Update"/> independent of the current clock time.</remarks>
        /// <param name="task">The work to be done.</param>
        /// <param name="data">The data to be passed to the task.</param>
        /// <param name="forceScheduled">If set to false, the task will be executed immediately if we are on the main thread.</param>
        /// <returns>The scheduled task, or <c>null</c> if the task was executed immediately.</returns>
        public ScheduledDelegate? Add<T>(Action<T> task, T data, bool forceScheduled = true)
        {
            if (!forceScheduled && IsMainThread)
            {
                //We are on the main thread already - don't need to schedule.
                task.Invoke(data);
                return null;
            }

            var del = new ScheduledDelegateWithData<T>(task, data);

            enqueue(del);

            return del;
        }

        /// <summary>
        /// Add a task to be scheduled.
        /// </summary>
        /// <remarks>If scheduled, the task will be run on the next <see cref="Update"/> independent of the current clock time.</remarks>
        /// <param name="task">The work to be done.</param>
        /// <param name="forceScheduled">If set to false, the task will be executed immediately if we are on the main thread.</param>
        /// <returns>The scheduled task, or <c>null</c> if the task was executed immediately.</returns>
        public ScheduledDelegate? Add(Action task, bool forceScheduled = true)
        {
            if (!forceScheduled && IsMainThread)
            {
                //We are on the main thread already - don't need to schedule.
                task.Invoke();
                return null;
            }

            var del = new ScheduledDelegate(task);

            enqueue(del);

            return del;
        }

        /// <summary>
        /// Add a task to be scheduled.
        /// </summary>
        /// <remarks>The task will be run on the next <see cref="Update"/> independent of the current clock time.</remarks>
        /// <param name="task">The scheduled delegate to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when attempting to add a scheduled delegate that has been already completed.</exception>
        public void Add(ScheduledDelegate task)
        {
            if (task.Completed)
                throw new InvalidOperationException($"Can not add a {nameof(ScheduledDelegate)} that has been already {nameof(ScheduledDelegate.Completed)}");

            lock (queueLock)
            {
                timedTasks.AddInPlace(task);

                if (timedTasks.Count % LOG_EXCESSSIVE_QUEUE_LENGTH_INTERVAL == 0)
                {
                    Logger.Log($"{this} has {timedTasks.Count} timed tasks pending", LoggingTarget.Performance);
                    Logger.Log($"- First task: {timedTasks[0]}", LoggingTarget.Performance);
                    Logger.Log($"- Last task: {timedTasks[^1]}", LoggingTarget.Performance);
                }
            }
        }

        /// <summary>
        /// Add a task which will be run after a specified delay from the current clock time.
        /// </summary>
        /// <param name="task">The work to be done.</param>
        /// <param name="data">The data to be passed to the task.</param>
        /// <param name="timeUntilRun">Milliseconds until run.</param>
        /// <param name="repeat">Whether this task should repeat.</param>
        /// <returns>Whether this is the first queue attempt of this work.</returns>
        public ScheduledDelegate AddDelayed<T>(Action<T> task, T data, double timeUntilRun, bool repeat = false)
        {
            // We are locking here already to make sure we have no concurrent access to currentTime
            lock (queueLock)
            {
                ScheduledDelegate del = new ScheduledDelegateWithData<T>(task, data, currentTime + timeUntilRun, repeat ? timeUntilRun : -1);
                Add(del);
                return del;
            }
        }

        /// <summary>
        /// Add a task which will be run after a specified delay from the current clock time.
        /// </summary>
        /// <param name="task">The work to be done.</param>
        /// <param name="timeUntilRun">Milliseconds until run.</param>
        /// <param name="repeat">Whether this task should repeat.</param>
        /// <returns>The scheduled task.</returns>
        public ScheduledDelegate AddDelayed(Action task, double timeUntilRun, bool repeat = false)
        {
            // We are locking here already to make sure we have no concurrent access to currentTime
            lock (queueLock)
            {
                ScheduledDelegate del = new ScheduledDelegate(task, currentTime + timeUntilRun, repeat ? timeUntilRun : -1);
                Add(del);
                return del;
            }
        }

        /// <summary>
        /// Adds a task which will only be run once per frame, no matter how many times it was scheduled in the previous frame.
        /// </summary>
        /// <remarks>The task will be run on the next <see cref="Update"/> independent of the current clock time.</remarks>
        /// <param name="task">The work to be done.</param>
        /// <param name="data">The data to be passed to the task. Note that duplicate schedules may result in previous data never being run.</param>
        /// <returns>Whether this is the first queue attempt of this work.</returns>
        public bool AddOnce<T>(Action<T> task, T data)
        {
            lock (queueLock)
            {
                // O(1) dictionary lookup by reference, replacing the previous O(n) runQueue scan.
                if (runQueueOnceSet.TryGetValue(task, out var existing))
                {
                    // Update the data on the already-queued delegate so the most recent value wins.
                    ((ScheduledDelegateWithData<T>)existing).Data = data;
                    return false;
                }

                var del = new ScheduledDelegateWithData<T>(task, data);
                del.OnceKey = task;
                runQueueOnceSet.TryAdd(task, del);
                enqueue(del);
            }

            return true;
        }

        /// <summary>
        /// Adds a task which will only be run once per frame, no matter how many times it was scheduled in the previous frame.
        /// </summary>
        /// <remarks>The task will be run on the next <see cref="Update"/> independent of the current clock time.</remarks>
        /// <param name="task">The work to be done. Avoid using inline delegates as they may not be cached, bypassing the once-per-frame guarantee.</param>
        /// <returns>Whether this is the first queue attempt of this work.</returns>
        public bool AddOnce(Action task)
        {
            lock (queueLock)
            {
                // ConcurrentDictionary.TryAdd is O(1) by reference identity.
                var del = new ScheduledDelegate(task);
                del.OnceKey = task;
                if (!runQueueOnceSet.TryAdd(task, del))
                    return false;

                enqueue(del);
            }

            return true;
        }

        private void enqueue(ScheduledDelegate task)
        {
            // ConcurrentQueue.Enqueue is lock-free — no lock needed for regular Add() callers.
            // AddOnce paths call this while holding queueLock (for atomicity), but that is safe
            // since ConcurrentQueue does not itself take a lock (no deadlock risk).
            runQueue.Enqueue(task);
            int count = Interlocked.Increment(ref runQueueCount);

            if (count % LOG_EXCESSSIVE_QUEUE_LENGTH_INTERVAL == 0)
                Logger.Log($"{this} has {count} tasks pending", LoggingTarget.Performance);
        }
    }
}
