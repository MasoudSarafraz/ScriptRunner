using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NCalc;

namespace ScriptEngine
{
    internal sealed class ScriptExecutor : IScriptExecutor
    {
        private static readonly Lazy<ScriptExecutor> _instance = new Lazy<ScriptExecutor>(() => new ScriptExecutor());

        private readonly ConcurrentDictionary<string, Func<object[], object>> _globalFunctions;
        private readonly ConcurrentDictionary<string, object> _globalVariables;
        private readonly ThreadLocal<ConcurrentDictionary<string, Func<object[], object>>> _threadLocalFunctions;
        private readonly ThreadLocal<ConcurrentDictionary<string, object>> _threadLocalVariables;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private EventHandler<ErrorEventArgs> _onError;

        private static volatile Action<Exception> _globalUnhandledErrorHandler;

        public static Action<Exception> GlobalUnhandledErrorHandler
        {
            get => _globalUnhandledErrorHandler;
            set => _globalUnhandledErrorHandler = value;
        }
        public event EventHandler<ErrorEventArgs> OnError
        {
            add
            {
                _lock.EnterWriteLock();
                try
                {
                    _onError += value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            remove
            {
                _lock.EnterWriteLock();
                try
                {
                    _onError -= value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        private ScriptExecutor()
        {
            _globalFunctions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            _globalVariables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _threadLocalFunctions = new ThreadLocal<ConcurrentDictionary<string, Func<object[], object>>>(() => new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase));
            _threadLocalVariables = new ThreadLocal<ConcurrentDictionary<string, object>>(() => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));

        }
        public static IScriptExecutor Instance => _instance.Value;

        public void AddGlobalFunction(string name, Func<object[], object> function)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Function name cannot be null or empty", nameof(name));
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            _lock.EnterWriteLock();
            try
            {
                _globalFunctions.AddOrUpdate(name, function, (key, oldValue) => function);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void AddLocalThreadFunction(string name, Func<object[], object> function)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Function name cannot be null or empty", nameof(name));
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            var localFunctions = _threadLocalFunctions.Value;
            localFunctions.AddOrUpdate(name, function, (key, oldValue) => function);
        }

        public void AddCustomFunction(string name, Func<object[], object> function)
        {
            AddLocalThreadFunction(name, function);
        }

        public bool RemoveGlobalFunction(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            _lock.EnterWriteLock();
            try
            {
                Func<object[], object> removedFunction;
                return _globalFunctions.TryRemove(name, out removedFunction);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool RemoveLocalThreadFunction(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            var localFunctions = _threadLocalFunctions.Value;
            Func<object[], object> removedFunction;
            return localFunctions.TryRemove(name, out removedFunction);
        }

        public bool RemoveCustomFunction(string name)
        {
            return RemoveLocalThreadFunction(name);
        }

        public void ClearGlobalFunctions()
        {
            _lock.EnterWriteLock();
            try
            {
                _globalFunctions.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ClearLocalThreadFunctions()
        {
            var localFunctions = _threadLocalFunctions.Value;
            localFunctions.Clear();
        }

        public void ClearCustomFunctions()
        {
            ClearLocalThreadFunctions();
        }

        public List<string> GetGlobalFunctionList()
        {
            _lock.EnterReadLock();
            try
            {
                return _globalFunctions.Keys.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<string> GetLocalThreadFunctionList()
        {
            var localFunctions = _threadLocalFunctions.Value;
            return localFunctions.Keys.ToList();
        }
        public List<string> GetCustomFunctionList()
        {
            return GetLocalThreadFunctionList();
        }
        public List<string> GetAllFunctionList()
        {
            var globalFunctions = GetGlobalFunctionList();
            var localFunctions = GetLocalThreadFunctionList();
            return globalFunctions.Concat(localFunctions).Distinct().ToList();
        }
        public dynamic Run(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var combinedFunctions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);

            _lock.EnterReadLock();
            try
            {
                foreach (var func in _globalFunctions)
                {
                    combinedFunctions.TryAdd(func.Key, func.Value);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            var localFunctions = _threadLocalFunctions.Value;
            foreach (var func in localFunctions)
            {
                combinedFunctions.AddOrUpdate(func.Key, func.Value, (key, oldValue) => func.Value);
            }
            try
            {
                var expr = new Expression(expression);
                expr.EvaluateFunction += (name, args) =>
                {
                    Func<object[], object> customFunction;
                    if (combinedFunctions.TryGetValue(name, out customFunction))
                    {
                        var parameters = new object[args.Parameters.Length];
                        for (int i = 0; i < args.Parameters.Length; i++)
                        {
                            parameters[i] = args.Parameters[i].Evaluate();
                        }

                        args.Result = customFunction(parameters);
                    }
                    else
                    {
                        throw new Exception($"Function '{name}' is not defined");
                    }
                };
                return expr.Evaluate();
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            _lock.EnterWriteLock();
            try
            {
                _onError = null;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (_threadLocalFunctions.IsValueCreated)
            {
                try { _threadLocalFunctions.Value.Clear(); }
                catch {   }
            }
            if (_threadLocalVariables.IsValueCreated)
            {
                try { _threadLocalVariables.Value.Clear(); }
                catch {   }
            }
        }

        public void SetGlobalVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be null or empty", nameof(name));
            _lock.EnterWriteLock();
            try
            {
                _globalVariables.AddOrUpdate(name, value, (key, oldValue) => value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetGlobalVariable(string name, out object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                value = null;
                return false;
            }
            _lock.EnterReadLock();
            try
            {
                return _globalVariables.TryGetValue(name, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryRemoveGlobalVariable(string name, out object oldValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                oldValue = null;
                return false;
            }
            _lock.EnterWriteLock();
            try
            {
                return _globalVariables.TryRemove(name, out oldValue);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsGlobalVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            _lock.EnterReadLock();
            try
            {
                return _globalVariables.ContainsKey(name);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void SetThreadLocalVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be null or empty", nameof(name));
            var localVariables = _threadLocalVariables.Value;
            localVariables.AddOrUpdate(name, value, (key, oldValue) => value);
        }

        public bool TryGetThreadLocalVariable(string name, out object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                value = null;
                return false;
            }
            var localVariables = _threadLocalVariables.Value;
            return localVariables.TryGetValue(name, out value);
        }

        public bool TryRemoveThreadLocalVariable(string name, out object oldValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                oldValue = null;
                return false;
            }
            var localVariables = _threadLocalVariables.Value;
            return localVariables.TryRemove(name, out oldValue);
        }

        public bool ContainsThreadLocalVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var localVariables = _threadLocalVariables.Value;
            return localVariables.ContainsKey(name);
        }

        private void RaiseError(Exception exception)
        {
            var handler = _onError;
            if (handler != null)
            {
                try
                {
                    handler(this, new ErrorEventArgs(exception));
                }
                catch
                {

                }
            }
            else
            {
                var global = _globalUnhandledErrorHandler;
                if (global != null)
                {
                    try
                    {
                        global(exception);
                    }
                    catch
                    {

                    }
                }
                else
                {
                    try
                    {
                        Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Unhandled Error] {exception.Message}");
                        if (exception.InnerException != null)
                        {
                            Console.Error.WriteLine($"Inner Exception: {exception.InnerException.Message}");
                        }
                    }
                    catch
                    {

                    }
                }
            }
        }
    }
}
