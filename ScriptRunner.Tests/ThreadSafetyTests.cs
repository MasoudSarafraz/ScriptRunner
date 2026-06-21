using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class ThreadSafetyTests
    {
        private void CleanupVariables()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            object _;
            engine.TryRemoveLocalThreadVariable("x", out _);
            engine.TryRemoveLocalThreadVariable("y", out _);
            engine.TryRemoveLocalThreadVariable("counter", out _);
            engine.TryRemoveLocalThreadVariable("total", out _);
            engine.TryRemoveLocalThreadVariable("temp", out _);
            engine.TryRemoveGlobalVariable("x", out _);
            engine.TryRemoveGlobalVariable("y", out _);
            engine.TryRemoveGlobalVariable("counter", out _);
            engine.TryRemoveGlobalVariable("total", out _);
            engine.TryRemoveGlobalVariable("temp", out _);
            engine.RemoveCustomFunction("sqrt");
            engine.RemoveCustomFunction("temp");
            engine.RemoveCustomFunction("square");
            engine.RemoveCustomFunction("cube");
            engine.RemoveCustomFunction("double");
            engine.RemoveCustomFunction("add");
            engine.RemoveCustomFunction("test");
            engine.RemoveCustomFunction("custom");
            engine.RemoveCustomFunction("global_test");
            engine.RemoveCustomFunction("local_test");
            engine.RemoveCustomFunction("custom1");
            engine.RemoveGlobalFunction("custom");
            engine.RemoveGlobalFunction("global_test");
        }

        [Fact]
        public void ThreadLocalVariable_Isolation_BetweenThreads()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var results = new ConcurrentDictionary<int, object>();

            var threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                int threadId = i;
                var t = new Thread(() =>
                {
                    engine.SetThreadLocalVariable("x", threadId);
                    Thread.Sleep(10);
                    var value = engine.Run("x");
                    results[threadId] = value;
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Equal(10, results.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i, results[i]);
            }
        }

        [Fact]
        public void GlobalVariable_Shared_BetweenThreads()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("shared", 42);

            var results = new ConcurrentBag<object>();
            var threads = new List<Thread>();

            for (int i = 0; i < 20; i++)
            {
                var t = new Thread(() =>
                {
                    results.Add(engine.Run("shared"));
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Equal(20, results.Count);
            foreach (var r in results)
            {
                Assert.Equal(42, r);
            }
        }

        [Fact]
        public void GlobalVariable_ConcurrentWrite_LastValuePersisted()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var threads = new List<Thread>();
            for (int i = 0; i < 50; i++)
            {
                int val = i;
                var t = new Thread(() =>
                {
                    engine.SetGlobalVariable("counter", val);
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.True(engine.TryGetGlobalVariable("counter", out var finalValue));
            Assert.IsType<int>(finalValue);
        }

        [Fact]
        public void LocalThreadFunction_Isolation_BetweenThreads()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var results = new ConcurrentDictionary<int, object>();

            var threads = new List<Thread>();
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                var t = new Thread(() =>
                {
                    engine.AddLocalThreadFunction("myfunc", args => threadId * 100);
                    Thread.Sleep(20);
                    var value = engine.Run("myfunc()");
                    results[threadId] = value;
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Equal(5, results.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i * 100, results[i]);
            }
        }

        [Fact]
        public void Run_ConcurrentCalls_DifferentExpressions_NoCrash()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var expressions = new[]
            {
                "1 + 2",
                "3 * 4",
                "10 - 5",
                "100 / 4",
                "2 + 3 * 4",
                "sqrt(16)",
                "iif(true, 1, 2)",
                "abs(-42)",
                "pow(2, 10)",
                "max(1, 2, 3)"
            };

            var expected = new object[]
            {
                3L,
                12L,
                5L,
                25L,
                14L,
                4.0,
                1,
                42.0,
                1024.0,
                3.0
            };

            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 20; i++)
            {
                int idx = i % expressions.Length;
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 50; j++)
                        {
                            var result = engine.Run(expressions[idx]);
                            Assert.Equal(expected[idx], result);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
        }

        [Fact]
        public void Run_ConcurrentCalls_SameExpression_CorrectResults()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("a", 10);
            engine.SetGlobalVariable("b", 20);

            var results = new ConcurrentBag<object>();
            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 30; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            results.Add(engine.Run("a + b"));
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.Equal(3000, results.Count);
            foreach (var r in results)
            {
                Assert.Equal(30L, r);
            }
        }

        [Fact]
        public void GlobalFunction_ConcurrentAddRemove_NoCrash()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 50; j++)
                        {
                            string name = "concurrent_func_" + idx;
                            engine.AddGlobalFunction(name, args => idx);
                            engine.RemoveGlobalFunction(name);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
        }

        [Fact]
        public void OnError_ConcurrentSubscribeUnsubscribe_NoCrash()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();
            EventHandler<ErrorEventArgs> handler = (sender, e) => { };

            var threads = new List<Thread>();
            for (int i = 0; i < 20; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            engine.OnError += handler;
                            engine.OnError -= handler;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
        }

        [Fact]
        public void OnError_ConcurrentRaise_AllHandlersInvoked()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            int handlerCallCount = 0;
            EventHandler<ErrorEventArgs> handler = (sender, e) =>
            {
                Interlocked.Increment(ref handlerCallCount);
            };

            engine.OnError += handler;

            var threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        engine.Run("unknown_function_xyz()");
                    }
                    catch { }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            engine.OnError -= handler;
            Assert.Equal(10, handlerCallCount);
        }

        [Fact]
        public void StressTest_100Threads_10000Operations()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("base", 5);

            int successCount = 0;
            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 100; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            var result = engine.Run("base * 2 + 1");
                            if (object.Equals(result, 11L))
                            {
                                Interlocked.Increment(ref successCount);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.Equal(10000, successCount);
        }

        [Fact]
        public void ConcurrentRun_WithVarDeclaration_ThreadLocal()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var results = new ConcurrentBag<object>();
            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                int threadId = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        engine.Run("var local = " + threadId);
                        Thread.Sleep(10);
                        results.Add(engine.Run("local"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.Equal(10, results.Count);

            var found = new HashSet<int>();
            foreach (var r in results)
            {
                found.Add((int)r);
            }
            Assert.Equal(10, found.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(i, found);
            }
        }

        [Fact]
        public void ConcurrentAddGlobalFunction_SameName_NoCorruption()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 20; i++)
            {
                int val = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        engine.AddGlobalFunction("shared_func", args => val);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.Contains("shared_func", engine.GetGlobalFunctionList());

            var result = engine.Run("shared_func()");
            Assert.IsType<int>(result);
            int intResult = (int)result;
            Assert.True(intResult >= 0 && intResult < 20);
        }

        [Fact]
        public void ConcurrentGetFunctionList_NoCrash()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();
            var listCounts = new ConcurrentBag<int>();

            var threads = new List<Thread>();
            for (int i = 0; i < 20; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 50; j++)
                        {
                            var list = engine.GetGlobalFunctionList();
                            listCounts.Add(list.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.Equal(1000, listCounts.Count);
        }

        [Fact]
        public void ConcurrentGetAllFunctionList_NoCrash()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();

            var threads = new List<Thread>();
            for (int i = 0; i < 20; i++)
            {
                int idx = i;
                var t = new Thread(() =>
                {
                    try
                    {
                        if (idx % 2 == 0)
                        {
                            engine.AddLocalThreadFunction("local_" + idx, args => idx);
                        }
                        for (int j = 0; j < 50; j++)
                        {
                            engine.GetAllFunctionList();
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
        }

        [Fact]
        public void ThreadLocal_DoesNotLeakAcrossThreadReuse()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            for (int iteration = 0; iteration < 3; iteration++)
            {
                int expectedValue = iteration * 10;

                var t = new Thread(() =>
                {
                    engine.SetThreadLocalVariable("iter", expectedValue);
                });
                t.Start();
                t.Join();
            }

            Assert.False(engine.TryGetThreadLocalVariable("iter", out _));
        }

        [Fact]
        public void ParallelInvoke_MultipleOperations()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();

            var errors = new ConcurrentBag<Exception>();
            var results = new ConcurrentBag<object>();

            Parallel.For(0, 100, i =>
            {
                try
                {
                    var r = engine.Run("1 + 1");
                    results.Add(r);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            Assert.Empty(errors);
            Assert.Equal(100, results.Count);
            foreach (var r in results)
            {
                Assert.Equal(2L, r);
            }
        }

        [Fact]
        public void ConcurrentVariableReadWrite_NoDeadlock()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("rw", 0);

            var errors = new ConcurrentBag<Exception>();

            var writers = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            engine.SetGlobalVariable("rw", j);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                writers.Add(t);
                t.Start();
            }

            var readers = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            engine.TryGetGlobalVariable("rw", out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                readers.Add(t);
                t.Start();
            }

            foreach (var t in writers) t.Join();
            foreach (var t in readers) t.Join();

            Assert.Empty(errors);
        }

        [Fact]
        public void MixedConcurrentOperations_AllSucceed()
        {
            CleanupVariables();
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("gvar", 100);

            var errors = new ConcurrentBag<Exception>();
            int successCount = 0;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(2000);

            var threads = new List<Thread>();

            for (int i = 0; i < 5; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            engine.Run("gvar + 1");
                            Interlocked.Increment(ref successCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            for (int i = 0; i < 5; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        int v = 0;
                        while (!cts.Token.IsCancellationRequested)
                        {
                            engine.SetGlobalVariable("gvar", v++);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            for (int i = 0; i < 3; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            engine.GetGlobalFunctionList();
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();

            Assert.Empty(errors);
            Assert.True(successCount > 0);
        }
    }
}
