using System;

namespace ScriptEngine
{
    /// <summary>
    /// Event arguments used by <see cref="IScriptExecutor.OnError"/> to deliver
    /// the exception that occurred during script execution or function management.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that occurred.
        /// </summary>
        public Exception Exception { get; private set; }

        public ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
