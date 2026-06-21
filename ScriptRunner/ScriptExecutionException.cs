using System;

namespace ScriptEngine
{
    /// <summary>
    /// Base class for all script execution related exceptions.
    /// Provides a common type to catch when handling errors raised by the script engine.
    /// </summary>
    public abstract class ScriptExecutionException : Exception
    {
        protected ScriptExecutionException(string message) : base(message) { }
    }
}
