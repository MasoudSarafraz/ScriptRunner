using System;
using System.Collections.Generic;

namespace ScriptEngine
{

    public interface IScriptExecutor : IDisposable
    {

        event EventHandler<ErrorEventArgs> OnError;

        dynamic Run(string expression);

        void AddCustomFunction(string name, Func<object[], object> function);

        bool RemoveCustomFunction(string name);

        void ClearCustomFunctions();

        List<string> GetCustomFunctionList();

        void AddGlobalFunction(string name, Func<object[], object> function);

        void AddLocalThreadFunction(string name, Func<object[], object> function);

        bool RemoveGlobalFunction(string name);

        bool RemoveLocalThreadFunction(string name);

        void ClearGlobalFunctions();

        void ClearLocalThreadFunctions();

        List<string> GetGlobalFunctionList();

        List<string> GetLocalThreadFunctionList();

        List<string> GetAllFunctionList();

        void SetGlobalVariable(string name, object value);

        bool TryGetGlobalVariable(string name, out object value);

        bool TryRemoveGlobalVariable(string name, out object oldValue);

        bool ContainsGlobalVariable(string name);

        void SetThreadLocalVariable(string name, object value);

        bool TryGetThreadLocalVariable(string name, out object value);

        bool TryRemoveThreadLocalVariable(string name, out object oldValue);

        bool ContainsThreadLocalVariable(string name);
    }
}
