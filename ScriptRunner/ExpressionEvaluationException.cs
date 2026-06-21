using System;

namespace ScriptEngine
{
    /// <summary>
    /// Thrown when an error occurs while evaluating an expression.
    /// Carries the original expression that caused the failure for diagnostics.
    /// </summary>
    public class ExpressionEvaluationException : ScriptExecutionException
    {
        /// <summary>
        /// The expression that caused the evaluation error.
        /// </summary>
        public string Expression { get; private set; }

        public ExpressionEvaluationException(string expression, string message) : base($"Error evaluating expression '{expression}': {message}")
        {
            Expression = expression;
        }
    }
}
