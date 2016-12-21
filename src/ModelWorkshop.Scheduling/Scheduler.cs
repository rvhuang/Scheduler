using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if DEBUG

using System.Diagnostics;

#endif

namespace ModelWorkshop.Scheduling
{
    /// <summary>
    /// Defines a thread-safe producer-consumer model that allows items to be added from multiple threads but allows only one thread to consume each of items.
    /// </summary>
    /// <typeparam name="TItem">Specifies the type of elements in the collection.</typeparam>
    public class Scheduler<TItem> : IDisposable
    {
        #region Fields

        private readonly IProducerConsumerCollection<TItem> _items;
        private readonly CancellationTokenSource _cancellation;

        private SpinLock _lock = new SpinLock();
        
        private Task _task = null;
        private Action _action = null;
        private Action<TItem> _callback = null;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the collection of items that will be taken by <see cref="Callback"/> delegate.
        /// </summary>
        public IProducerConsumerCollection<TItem> Items
        {
            get { return this._items; }
        }

        /// <summary>
        /// Gets the delegate that handles item taken from <see cref="Items"/>.
        /// </summary>
        public Action<TItem> Callback
        {
            get { return this._callback; }
        }
        
        /// <summary>
        /// Gets a value that indicates whether current instance is taking item from <see cref="Items"/>. 
        /// </summary>
        public bool IsBusy
        {
            get
            {
                if (this._lock.IsHeld)
                    return true;
                else
                    return this._task != null;
            }
        }

        #endregion

        #region Events
        
        /// <summary>
        /// Occurs when current instance is unable to start consuming each item from <see cref="Items"/>.
        /// </summary>
        public event EventHandler<ErrorEventArgs> Error;

        /// <summary>
        /// Occurs when <see cref="Callback"/> raises an exception.
        /// </summary>
        public event EventHandler<SchedulerErrorEventArgs<TItem>> SchedulerError;

        /// <summary>
        /// Occurs when items in <see cref="Items"/> are all consumed by <see cref="Callback"/>.
        /// </summary>
        public event EventHandler Completed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of this class with specific callback.
        /// </summary>
        /// <param name="callback">The delegate that handles item taken from <see cref="Items"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
        public Scheduler(Action<TItem> callback)
            : this(callback, new ConcurrentQueue<TItem>())
        {
        }

        /// <summary>
        /// Initializes a new instance of this class with specific callback and items.
        /// </summary>
        /// <param name="callback">The delegate that handles item taken from <see cref="Items"/>.</param>
        /// <param name="collection">The collection of items that will be taken by <see cref="Callback"/> delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> or <paramref name="collection"/> is null.</exception>
        public Scheduler(Action<TItem> callback, IEnumerable<TItem> collection)
            : this(callback, new ConcurrentQueue<TItem>(collection))
        {
        }

        /// <summary>
        /// Initializes a new instance of this class with specific callback and <see cref="IProducerConsumerCollection{T}"/> instance.
        /// </summary>
        /// <param name="callback">The delegate that handles item taken from <see cref="Items"/>.</param>
        /// <param name="items">An <see cref="IProducerConsumerCollection{T}"/> instance that will be consumed by <see cref="Callback"/> delegate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> or <paramref name="items"/> is null.</exception>
        public Scheduler(Action<TItem> callback, IProducerConsumerCollection<TItem> items)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (items == null) throw new ArgumentNullException("queue");

            this._items = items;
            this._callback = callback;
            this._cancellation = new CancellationTokenSource();
            this._action = this.TaskAction;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Start consuming each item from <see cref="Items"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
        public void Run()
        {
            var token = default(CancellationToken);

            try
            {
                token = this._cancellation.Token;
            }
            catch (ObjectDisposedException error)
            {
                throw new ObjectDisposedException("Object has been disposed.", error);
            }

            var taken = false;

            this._lock.TryEnter(ref taken);

            if (taken)
            {
                try
                {
                    if (this._task == null || this._task.IsCompleted)
                        this._task = Task.Factory.StartNew(this._action, token).ContinueWith(this.TaskContinuationAction);
#if DEBUG
                    Debug.WriteLine("New loop task is created.");
#endif
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("Unable to launch asynchronous operation.", error);
                }
                finally
                {
                    this._lock.Exit();
                }
            }
        }

        /// <summary>
        /// Add an item to the <see cref="Items"/> and start consuming each item from collection.
        /// </summary>
        /// <param name="item">A <typeparamref name="TItem"/> instance to be added.</param>
        /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
        public void AddAndRun(TItem item)
        {
            while (!this.TryAddAndRun(item)) ;
        }

        /// <summary>
        /// Try to add an item to the <see cref="Items"/> and start consuming each item from collection.
        /// </summary>
        /// <param name="item">A <typeparamref name="TItem"/> instance to be added.</param>
        /// <returns><c>true</c> if item is successfully added and starts to be .</returns>
        /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
        public bool TryAddAndRun(TItem item)
        {
            var token = default(CancellationToken);

            try
            {
                token = this._cancellation.Token;
            }
            catch (ObjectDisposedException error)
            {
                throw new ObjectDisposedException("Object has been disposed.", error);
            }
            if (this._items.TryAdd(item))
            {
                var taken = false;

                this._lock.TryEnter(ref taken);

                if (taken)
                {
                    try
                    {
                        if (this._task == null || this._task.IsCompleted)
                            this._task = Task.Factory.StartNew(this._action, token).ContinueWith(this.TaskContinuationAction);
#if DEBUG
                        Debug.WriteLine("New loop task is created.");
#endif
                    }
                    catch (Exception error)
                    {
                        throw new InvalidOperationException("Unable to launch asynchronous operation.", error);
                    }
                    finally
                    {
                        this._lock.Exit();
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stop comsuming each item from collection.
        /// </summary>
        public void Stop()
        {
            this._cancellation.Cancel();
        }

        #endregion

        #region Task Related 

        private void TaskAction()
        {
            var item = default(TItem);

            while (this._items.Count > 0)
            {
                try
                {
                    if (this._items.TryTake(out item))
                        this._callback(item);
                }
                catch (Exception error)
                {
                    this.OnSchedulerError(new SchedulerErrorEventArgs<TItem>(item, error));
                }
                this._cancellation.Token.ThrowIfCancellationRequested();
#if DEBUG
                Debug.WriteLine("Remaining items: {0}.", this._items.Count);
#endif
            }
        }
        
        private void TaskContinuationAction(Task task)
        {
            if (task.Exception != null)
                task.Exception.Handle(this.TaskExceptionHandler);
            else
                this.OnCompleted(EventArgs.Empty);
#if !NET_CORE
            task.Dispose();
#endif
        }

        private bool TaskExceptionHandler(Exception error)
        {
            this.OnError(new ErrorEventArgs(error));
            return true;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Stop current instance and releases all resources. 
        /// </summary>
        public void Dispose()
        {
            this._cancellation.Dispose();
        }

        #endregion

        #region Event Raiser

        /// <summary>
        /// Raise the <see cref="Error"/> event.
        /// </summary>
        /// <param name="e">An <see cref="ErrorEventArgs"/> that contains the event data.</param>
        protected virtual void OnError(ErrorEventArgs e)
        {
            try
            {
                this.Error(this, e);
            }
            catch (Exception error)
            {
                if (this.Error != null) throw error;
            }
        }

        /// <summary>
        /// Raise the <see cref="SchedulerError"/> event.
        /// </summary>
        /// <param name="e">An <see cref="SchedulerErrorEventArgs{TItem}"/> that contains the event data.</param>
        protected virtual void OnSchedulerError(SchedulerErrorEventArgs<TItem> e)
        {
            try
            {
                this.SchedulerError(this, e);
            } 
            catch (Exception error)
            {
                if (this.SchedulerError != null) throw error; 
            }
        }

        /// <summary>
        /// Raise the <see cref="Completed"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCompleted(EventArgs e)
        {
            try
            {
                this.Completed(this, e);
            }
            catch (Exception error)
            {
                if (this.Completed != null) throw error;
            }
        }

        #endregion
    }
}
