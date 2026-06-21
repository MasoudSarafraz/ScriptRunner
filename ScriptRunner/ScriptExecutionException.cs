using System;

namespace ScriptEngine
{

    public abstract class ScriptExecutionException : Exception
    {
        protected ScriptExecutionException(string message) : base(message) { }
    }
}
