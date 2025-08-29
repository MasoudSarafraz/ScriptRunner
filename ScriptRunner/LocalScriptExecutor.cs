using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ScriptEngine
{
    internal sealed class LocalScriptExecutor : IScriptExecutor
    {
        private static readonly Lazy<LocalScriptExecutor> _instance = new Lazy<LocalScriptExecutor>(() => new LocalScriptExecutor());
        private readonly ConcurrentDictionary<string, Func<object[], object>> _globalFunctions;
        private readonly ThreadLocal<ConcurrentDictionary<string, Func<object[], object>>> _threadLocalFunctions;
        private readonly ConcurrentDictionary<string, object> _globalVariables;
        private readonly ThreadLocal<ConcurrentDictionary<string, object>> _threadLocalVariables;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private EventHandler<ErrorEventArgs> _onError;
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

        private LocalScriptExecutor()
        {
            _globalFunctions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            _threadLocalFunctions = new ThreadLocal<ConcurrentDictionary<string, Func<object[], object>>>(() =>
                new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase));

            _globalVariables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _threadLocalVariables = new ThreadLocal<ConcurrentDictionary<string, object>>(() =>
                new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));

            AddDefaultFunctions();
            AddDefaultConstants();
        }

        private void AddDefaultFunctions()
        {
            // توابع ریاضی
            AddGlobalFunction("sqrt", parameters => Math.Sqrt(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("pow", parameters => Math.Pow(Convert.ToDouble(parameters[0]), Convert.ToDouble(parameters[1])));
            AddGlobalFunction("abs", parameters => Math.Abs(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("round", parameters =>
                parameters.Length == 1 ? Math.Round(Convert.ToDouble(parameters[0])) :
                Math.Round(Convert.ToDouble(parameters[0]), Convert.ToInt32(parameters[1])));
            AddGlobalFunction("ceil", parameters => Math.Ceiling(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("floor", parameters => Math.Floor(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("exp", parameters => Math.Exp(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("log", parameters =>
                parameters.Length == 1 ? Math.Log(Convert.ToDouble(parameters[0])) :
                Math.Log(Convert.ToDouble(parameters[0]), Convert.ToDouble(parameters[1])));
            AddGlobalFunction("log10", parameters => Math.Log10(Convert.ToDouble(parameters[0])));
            // توابع مثلثاتی
            AddGlobalFunction("sin", parameters => Math.Sin(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("cos", parameters => Math.Cos(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("tan", parameters => Math.Tan(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("asin", parameters => Math.Asin(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("acos", parameters => Math.Acos(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("atan", parameters => Math.Atan(Convert.ToDouble(parameters[0])));
            AddGlobalFunction("atan2", parameters => Math.Atan2(Convert.ToDouble(parameters[0]), Convert.ToDouble(parameters[1])));
            // توابع رشته‌ای
            AddGlobalFunction("length", parameters => parameters[0] is string str ? str.Length : ((Array)parameters[0]).Length);
            AddGlobalFunction("substring", parameters => ((string)parameters[0]).Substring(Convert.ToInt32(parameters[1]), parameters.Length > 2 ? Convert.ToInt32(parameters[2]) : ((string)parameters[0]).Length - Convert.ToInt32(parameters[1])));
            AddGlobalFunction("toupper", parameters => ((string)parameters[0]).ToUpper());
            AddGlobalFunction("tolower", parameters => ((string)parameters[0]).ToLower());
            AddGlobalFunction("trim", parameters => ((string)parameters[0]).Trim());
            AddGlobalFunction("concat", parameters => string.Concat(parameters.Select(p => p?.ToString())));
            // توابع تاریخ و زمان
            AddGlobalFunction("now", parameters => DateTime.Now);
            AddGlobalFunction("today", parameters => DateTime.Today);
            AddGlobalFunction("year", parameters => ((DateTime)parameters[0]).Year);
            AddGlobalFunction("month", parameters => ((DateTime)parameters[0]).Month);
            AddGlobalFunction("day", parameters => ((DateTime)parameters[0]).Day);

            // توابع شرطی
            AddGlobalFunction("iif", parameters => Convert.ToBoolean(parameters[0]) ? parameters[1] : parameters[2]);
            AddGlobalFunction("coalesce", parameters => parameters.FirstOrDefault(p => p != null) ?? parameters[parameters.Length - 1]);
            AddGlobalFunction("isnull", parameters => parameters[0] == null);
            AddGlobalFunction("isnumber", parameters => IsNumber(parameters[0]));
            AddGlobalFunction("isstring", parameters => parameters[0] is string);
            // توابع آرایه
            AddGlobalFunction("array", parameters => parameters);
            AddGlobalFunction("count", parameters =>
                parameters[0] is Array array ? array.Length :
                parameters[0] is IEnumerable<object> enumerable ? enumerable.Count() : 1);
            AddGlobalFunction("sum", parameters => parameters[0] is Array array ? array.Cast<object>().Sum(item => Convert.ToDouble(item)) : parameters.Sum(p => Convert.ToDouble(p)));
            AddGlobalFunction("avg", parameters =>
                parameters[0] is Array array ? array.Cast<object>().Average(item => Convert.ToDouble(item)) :
                parameters.Average(p => Convert.ToDouble(p)));
            AddGlobalFunction("min", parameters => parameters[0] is Array array ? array.Cast<object>().Min(item => Convert.ToDouble(item)) : parameters.Min(p => Convert.ToDouble(p)));
            AddGlobalFunction("max", parameters =>
                parameters[0] is Array array ? array.Cast<object>().Max(item => Convert.ToDouble(item)) :
                parameters.Max(p => Convert.ToDouble(p)));
        }

        private void AddDefaultConstants()
        {
            SetGlobalVariable("PI", Math.PI);
            SetGlobalVariable("E", Math.E);
            SetGlobalVariable("TRUE", true);
            SetGlobalVariable("FALSE", false);
            SetGlobalVariable("NULL", null);
        }

        private bool IsNumber(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
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
                return false;

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

        public void ClearThreadLocalFunctions()
        {
            var localFunctions = _threadLocalFunctions.Value;
            localFunctions.Clear();
        }

        public void ClearCustomFunctions()
        {
            ClearThreadLocalFunctions();
        }

        public int GetGlobalFunctionCount()
        {
            _lock.EnterReadLock();
            try
            {
                return _globalFunctions.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public int GetLocalThreadFunctionCount()
        {
            var localFunctions = _threadLocalFunctions.Value;
            return localFunctions.Count;
        }

        public int GetCustomFunctionCount()
        {
            return GetLocalThreadFunctionCount();
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

        public object GetGlobalVariable(string name)
        {
            _lock.EnterReadLock();
            try
            {
                return _globalVariables.TryGetValue(name, out object value) ? value : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool RemoveGlobalVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            _lock.EnterWriteLock();
            try
            {
                return _globalVariables.TryRemove(name, out _);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void SetThreadLocalVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be null or empty", nameof(name));

            var localVariables = _threadLocalVariables.Value;
            localVariables.AddOrUpdate(name, value, (key, oldValue) => value);
        }

        public object GetThreadLocalVariable(string name)
        {
            var localVariables = _threadLocalVariables.Value;
            return localVariables.TryGetValue(name, out object value) ? value : null;
        }

        public bool RemoveLocalThreadVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var localVariables = _threadLocalVariables.Value;
            return localVariables.TryRemove(name, out _);
        }

        public dynamic Run(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }                
            // ترکیب توابع و متغیرهای عمومی و thread-local
            var combinedFunctions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var combinedVariables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            // اضافه کردن توابع و متغیرهای عمومی
            _lock.EnterReadLock();
            try
            {
                foreach (var func in _globalFunctions)
                {
                    combinedFunctions.TryAdd(func.Key, func.Value);
                }

                foreach (var variable in _globalVariables)
                {
                    combinedVariables.TryAdd(variable.Key, variable.Value);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
            // اضافه کردن توابع و متغیرهای مخصوص thread جاری (با اولویت بالاتر)
            var localFunctions = _threadLocalFunctions.Value;
            var localVariables = _threadLocalVariables.Value;

            foreach (var func in localFunctions)
            {
                combinedFunctions.AddOrUpdate(func.Key, func.Value, (key, oldValue) => func.Value);
            }

            foreach (var variable in localVariables)
            {
                combinedVariables.AddOrUpdate(variable.Key, variable.Value, (key, oldValue) => variable.Value);
            }
            try
            {
                var parser = new ScriptParser(expression, combinedFunctions, combinedVariables);
                return parser.Evaluate();
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            _onError = null;
            _globalFunctions.Clear();
            _globalVariables.Clear();
            _threadLocalFunctions.Dispose();
            _threadLocalVariables.Dispose();
            _lock.Dispose();
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
                    // Ignore
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
                    //Ignore
                }
            }
        }

        public void ClearLocalThreadFunctions()
        {
            var localFunctions = _threadLocalFunctions.Value;
            localFunctions.Clear();
        }

    }

}