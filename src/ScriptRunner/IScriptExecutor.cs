using System;
using System.Collections.Generic;

namespace ScriptEngine
{
    /// <summary>
    /// Provides a thread-safe script execution engine with support for custom functions.
    /// This executor allows running mathematical and logical expressions with the ability
    /// to define global functions (shared across all threads) and local thread-specific functions.
    /// 
    /// Features:
    /// - Thread-safe execution environment
    /// - Support for both global and thread-local functions
    /// - Custom function management (add, remove, clear)
    /// - Error handling through events
    /// - Integration with NCalc expression evaluator
    /// 
    /// Global functions are shared across all threads and have lower priority than local functions.
    /// Local thread functions are isolated to each thread and take precedence over global functions
    /// when both have the same name.
    /// </summary>
    public interface IScriptExecutor : IDisposable
    {
        /// <summary>
        /// Occurs when an error happens during script execution or function management.
        /// </summary>
        event EventHandler<ErrorEventArgs> OnError;

        /// <summary>
        /// Executes the specified mathematical or logical expression.
        /// </summary>
        /// <param name="expression">The expression string to evaluate.</param>
        /// <returns>The result of the expression evaluation.</returns>
        dynamic Run(string expression);

        /// <summary>
        /// Adds a custom function to the current thread's local function collection.
        /// Local functions have higher priority than global functions.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="function">The function implementation.</param>
        void AddCustomFunction(string name, Func<object[], object> function);

        /// <summary>
        /// Removes a custom function from the current thread's local function collection.
        /// </summary>
        /// <param name="name">The name of the function to remove.</param>
        /// <returns>True if the function was found and removed; otherwise, false.</returns>
        bool RemoveCustomFunction(string name);

        /// <summary>
        /// Clears all custom functions from the current thread's local function collection.
        /// </summary>
        void ClearCustomFunctions();

        /// <summary>
        /// Gets a list of all custom function names in the current thread's local collection.
        /// </summary>
        /// <returns>A list of function names.</returns>
        List<string> GetCustomFunctionList();

        /// <summary>
        /// Adds a global function that is shared across all threads.
        /// Global functions have lower priority than local thread functions.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="function">The function implementation.</param>
        void AddGlobalFunction(string name, Func<object[], object> function);

        /// <summary>
        /// Adds a function to the current thread's local function collection.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="function">The function implementation.</param>
        void AddLocalThreadFunction(string name, Func<object[], object> function);

        /// <summary>
        /// Removes a global function from the shared function collection.
        /// </summary>
        /// <param name="name">The name of the function to remove.</param>
        /// <returns>True if the function was found and removed; otherwise, false.</returns>
        bool RemoveGlobalFunction(string name);

        /// <summary>
        /// Removes a function from the current thread's local function collection.
        /// </summary>
        /// <param name="name">The name of the function to remove.</param>
        /// <returns>True if the function was found and removed; otherwise, false.</returns>
        bool RemoveLocalThreadFunction(string name);

        /// <summary>
        /// Clears all global functions from the shared function collection.
        /// </summary>
        void ClearGlobalFunctions();

        /// <summary>
        /// Clears all functions from the current thread's local function collection.
        /// </summary>
        void ClearLocalThreadFunctions();

        /// <summary>
        /// Gets a list of all global function names shared across all threads.
        /// </summary>
        /// <returns>A list of global function names.</returns>
        List<string> GetGlobalFunctionList();

        /// <summary>
        /// Gets a list of all function names in the current thread's local collection.
        /// </summary>
        /// <returns>A list of local thread function names.</returns>
        List<string> GetLocalThreadFunctionList();

        /// <summary>
        /// Gets a combined list of all available function names (both global and local)
        /// that are accessible from the current thread.
        /// </summary>
        /// <returns>A list of all accessible function names.</returns>
        List<string> GetAllFunctionList();
    }
}
