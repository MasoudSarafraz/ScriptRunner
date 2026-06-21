using System;

namespace ScriptEngine
{

    public class ExpressionEvaluationException : ScriptExecutionException
    {

        public string Expression { get; private set; }

        public ExpressionEvaluationException(string expression, string message) : base($"Error evaluating expression '{expression}': {message}")
        {
            Expression = expression;
        }
    }
}
