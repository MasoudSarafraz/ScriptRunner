using System;

namespace ScriptEngine
{
    public abstract class ScriptExecutionException : Exception
    {
        protected ScriptExecutionException(string message) : base(message) { }
    }
    public class ExpressionEvaluationException : ScriptExecutionException
    {
        public string Expression { get; private set; }

        public ExpressionEvaluationException(string expression, string message) : base($"Error evaluating expression '{expression}': {message}")
        {
            Expression = expression;
        }
    }
    public class FunctionRegistrationException : ScriptExecutionException
    {
        public string FunctionName { get; private set; }

        public FunctionRegistrationException(string functionName, string message) : base($"Error in function registration '{functionName}': {message}")
        {
            FunctionName = functionName;
        }
    }
    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }

        public ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
