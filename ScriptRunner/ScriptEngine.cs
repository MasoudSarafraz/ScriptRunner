using System;
using System.Collections.Concurrent;
using System.Threading;
using NCalc;
namespace ScriptEngine
{
    #region Error Handling

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

    #endregion

    #region Script Executor

    public interface IScriptExecutor : IDisposable
    {
        event EventHandler<ErrorEventArgs> OnError;

        dynamic Run(string expression);

        void AddCustomFunction(string name, Func<object[], object> function);
    }

    internal sealed class ScriptExecutor : IScriptExecutor
    {
        private static readonly Lazy<ScriptExecutor> _instance = new Lazy<ScriptExecutor>(() => new ScriptExecutor());
        private readonly ConcurrentDictionary<string, Func<object[], object>> _customFunctions;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
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

        private ScriptExecutor()
        {
            _customFunctions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            // Add Defualt Function
            AddCustomFunction("iif", parameters =>
            {
                if (parameters.Length != 3)
                {
                    RaiseError(new Exception("iif have more than three parameter"));
                }
                bool condition = Convert.ToBoolean(parameters[0]);
                return condition ? parameters[1] : parameters[2];
            });
        }

        public static IScriptExecutor Instance
        {
            get { return _instance.Value; }
        }

        public void AddCustomFunction(string name, Func<object[], object> function)
        {
            _lock.EnterWriteLock();
            try
            {
                _customFunctions.TryAdd(name, function);
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        public dynamic Run(string expression)
        {
            Func<string, Func<object[], object>> getFunction = null;

            _lock.EnterReadLock();
            try
            {                
                var localFunctions = new ConcurrentDictionary<string, Func<object[], object>>(_customFunctions, StringComparer.OrdinalIgnoreCase);
                getFunction = name => localFunctions.TryGetValue(name, out var func) ? func : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
            var expr = new Expression(expression);
            expr.EvaluateFunction += (name, args) =>
            {
                var customFunction = getFunction(name);
                if (customFunction != null)
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
                    throw new ExpressionEvaluationException(expression, $"function '{name}' not declared");
                }
            };

            return expr.Evaluate();
        }
        public void Dispose()
        {
            _lock.EnterReadLock();
            try
            {
                _onError = null;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _lock.Dispose();
        }

        private void RaiseError(Exception exception)
        {
            EventHandler<ErrorEventArgs> handler;
            _lock.EnterReadLock();
            try
            {
                handler = _onError;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            if (handler != null)
            {
                handler(this, new ErrorEventArgs(exception));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Unhandled Error] {exception.Message}");
                Console.ResetColor();
            }
        }
    }
    public static class ScriptEngineFactory
    {
        public static IScriptExecutor Create()
        {
            return ScriptExecutor.Instance;
        }
    }
    #endregion
}
