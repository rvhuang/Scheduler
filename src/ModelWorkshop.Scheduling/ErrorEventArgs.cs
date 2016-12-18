using System;

namespace ModelWorkshop.Scheduling
{
    /// <summary>
    /// Provide data for the <see cref="Scheduler{TItem}.Error"/> event.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Returns the exception occurs within the <see cref="Scheduler{TItem}"/> instance.
        /// </summary>
        public Exception Error
        {
            get; private set;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialize a new instance of <see cref="ErrorEventArgs"/> instance.
        /// </summary>
        /// <param name="error">The exception occurs within the <see cref="Scheduler{TItem}"/> instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public ErrorEventArgs(Exception error)
        {
            if (error == null) throw new ArgumentNullException("error");

            this.Error = error;
        }

        #endregion
    }
}