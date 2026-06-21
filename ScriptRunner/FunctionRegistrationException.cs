using System;

namespace ScriptEngine
{

    public class FunctionRegistrationException : ScriptExecutionException
    {

        public string FunctionName { get; private set; }

        public FunctionRegistrationException(string functionName, string message) : base($"Error in function registration '{functionName}': {message}")
        {
            FunctionName = functionName;
        }
    }
}
