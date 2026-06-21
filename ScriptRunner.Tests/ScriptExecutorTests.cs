using System;
using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class ScriptExecutorTests
    {
        [Theory]
        [InlineData("1 + 2", 3)]
        [InlineData("10 * 5", 50)]
        [InlineData("100 / 4", 25)]
        public void Run_BasicArithmetic(string expr, object expected)
        {
            var engine = ScriptEngineFactory.Create();
            var result = engine.Run(expr);
            Assert.Equal(Convert.ToDouble(expected), Convert.ToDouble(result));
        }

        [Fact]
        public void Run_NullOrWhitespace_ReturnsNull()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.Null(engine.Run(null));
            Assert.Null(engine.Run(""));
        }

        [Fact]
        public void CustomFunction_AddAndCall()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddCustomFunction("square", args => Convert.ToInt32(args[0]) * Convert.ToInt32(args[0]));
            Assert.Equal(25, engine.Run("square(5)"));
        }

        [Fact]
        public void CustomFunction_AddGlobalAndCall()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddGlobalFunction("cube", args =>
            {
                var v = Convert.ToInt64(args[0]);
                return v * v * v;
            });
            Assert.Equal(27L, engine.Run("cube(3)"));
        }

        [Fact]
        public void CustomFunction_Remove()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddCustomFunction("temp", args => 1);
            Assert.True(engine.RemoveCustomFunction("temp"));
        }

        [Fact]
        public void CustomFunction_Clear()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddCustomFunction("a", args => 1);
            engine.AddCustomFunction("b", args => 2);
            engine.ClearCustomFunctions();
            Assert.Empty(engine.GetCustomFunctionList());
        }

        [Fact]
        public void CustomFunction_Add_EmptyName_Throws()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.Throws<ArgumentException>(() => engine.AddCustomFunction("", args => 1));
        }

        [Fact]
        public void CustomFunction_Add_NullFunction_Throws()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.Throws<ArgumentNullException>(() => engine.AddCustomFunction("x", null));
        }

        [Fact]
        public void FunctionList_GlobalFunctions()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddGlobalFunction("custom", args => 1);
            var list = engine.GetGlobalFunctionList();
            Assert.Contains("custom", list);
        }

        [Fact]
        public void FunctionList_LocalThreadFunctions()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddLocalThreadFunction("local", args => 1);
            var list = engine.GetLocalThreadFunctionList();
            Assert.Contains("local", list);
        }

        [Fact]
        public void FunctionList_AllFunctions()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddGlobalFunction("g", args => 1);
            engine.AddLocalThreadFunction("l", args => 1);
            var list = engine.GetAllFunctionList();
            Assert.Contains("g", list);
            Assert.Contains("l", list);
        }

        [Fact]
        public void ErrorHandling_OnErrorEvent()
        {
            var engine = ScriptEngineFactory.Create();
            Exception caught = null;
            EventHandler<ErrorEventArgs> handler = (sender, e) => caught = e.Exception;
            engine.OnError += handler;

            try { engine.Run("invalid function call @@@"); } catch { }

            Assert.NotNull(caught);
            engine.OnError -= handler;
        }

        [Fact]
        public void Dispose_DoesNotBreakSingleton()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddLocalThreadFunction("temp", args => 1);
            engine.Dispose();

            var engine2 = ScriptEngineFactory.Create();
            Assert.Equal(2, engine2.Run("1 + 1"));
        }

        [Fact]
        public void GlobalUnhandledErrorHandler_Static()
        {
            var engine = ScriptEngineFactory.Create();
            engine.Dispose();

            Exception caught = null;
            ScriptExecutor.GlobalUnhandledErrorHandler = ex => caught = ex;

            try
            {
                var engine2 = ScriptEngineFactory.Create();
                try { engine2.Run("@@@invalid"); } catch { }
                Assert.NotNull(caught);
            }
            finally
            {
                ScriptExecutor.GlobalUnhandledErrorHandler = null;
            }
        }

        [Fact]
        public void NestedFunctionCalls()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddGlobalFunction("add", args => Convert.ToInt32(args[0]) + Convert.ToInt32(args[1]));
            engine.AddGlobalFunction("double", args => Convert.ToInt32(args[0]) * 2);
            Assert.Equal(6, engine.Run("double(add(1, 2))"));
        }

        [Fact]
        public void ClearGlobalFunctions_RemovesAll()
        {
            var engine = ScriptEngineFactory.Create();
            engine.AddGlobalFunction("custom", args => 1);
            engine.RemoveGlobalFunction("custom");
            Assert.DoesNotContain("custom", engine.GetGlobalFunctionList());
        }

        [Fact]
        public void RemoveGlobalFunction_NonExistent_ReturnsFalse()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.False(engine.RemoveGlobalFunction("nonexistent"));
        }

        [Fact]
        public void RemoveCustomFunction_NonExistent_ReturnsFalse()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.False(engine.RemoveCustomFunction("nonexistent"));
        }
    }
}
