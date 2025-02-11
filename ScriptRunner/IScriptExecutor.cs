using System;

namespace ScriptEngine
{
    public interface IScriptExecutor : IDisposable
    {
        event EventHandler<ErrorEventArgs> OnError;
        dynamic Run(string expression);
        void AddCustomFunction(string name, Func<object[], object> function);
    }
}
