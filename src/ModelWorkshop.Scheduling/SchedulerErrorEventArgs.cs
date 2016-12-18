using System;

namespace ModelWorkshop.Scheduling
{
    /// <summary>
    /// Provide data for the <see cref="Scheduler{TItem}.SchedulerError"/> event.
    /// </summary>
    /// <typeparam name="TItem">Specifies the type of elements in the collection.</typeparam>
    public class SchedulerErrorEventArgs<TItem> : EventArgs
    {
        #region Fields

        private readonly TItem _item;
        private readonly Exception _error;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the item that caused <see cref="Scheduler{TItem}.Callback"/> to raise exception.
        /// </summary>
        public TItem Item
        {
            get { return this._item; }
        }

        /// <summary>
        /// Returns the exception raised by <see cref="Scheduler{TItem}.Callback"/>.
        /// </summary>
        public Exception Error
        {
            get { return this._error; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialize a new instance of <see cref="SchedulerErrorEventArgs{TItem}"/> class.
        /// </summary>
        /// <param name="item">The item that caused <see cref="Scheduler{TItem}.Callback"/> to raise exception.</param>
        /// <param name="error">The exception raised by <see cref="Scheduler{TItem}.Callback"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public SchedulerErrorEventArgs(TItem item, Exception error)
        {
#if NET_CORE
            if(error == null) throw new ArgumentNullException(nameof(error));
#else
            if (error == null) throw new ArgumentNullException("error");
#endif
            this._item = item;
            this._error = error;
        }

        #endregion
    }
}
