using System;

namespace ScriptEngine
{
    /// <summary>
    /// Thrown when an error occurs during function registration (add/remove/update).
    /// Carries the name of the function that caused the failure for diagnostics.
    /// </summary>
    public class FunctionRegistrationException : ScriptExecutionException
    {
        /// <summary>
        /// The name of the function that caused the registration error.
        /// </summary>
        public string FunctionName { get; private set; }

        public FunctionRegistrationException(string functionName, string message) : base($"Error in function registration '{functionName}': {message}")
        {
            FunctionName = functionName;
        }
    }
}
