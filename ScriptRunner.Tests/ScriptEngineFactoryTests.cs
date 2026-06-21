using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class ScriptEngineFactoryTests
    {
        [Fact]
        public void Create_ReturnsScriptExecutor()
        {
            var engine = ScriptEngineFactory.Create();
            Assert.NotNull(engine);
            Assert.IsAssignableFrom<IScriptExecutor>(engine);
        }

        [Fact]
        public void CreateLocalScriptEngine_ReturnsLocalScriptExecutor()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.NotNull(engine);
            Assert.IsAssignableFrom<IScriptExecutor>(engine);
        }

        [Fact]
        public void Create_ReturnsSameInstance_Singleton()
        {
            var engine1 = ScriptEngineFactory.Create();
            var engine2 = ScriptEngineFactory.Create();
            Assert.Same(engine1, engine2);
        }

        [Fact]
        public void CreateLocalScriptEngine_ReturnsSameInstance_Singleton()
        {
            var engine1 = ScriptEngineFactory.CreateLocalScriptEngine();
            var engine2 = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Same(engine1, engine2);
        }

        [Fact]
        public void Create_And_CreateLocalScriptEngine_ReturnDifferentInstances()
        {
            var engine1 = ScriptEngineFactory.Create();
            var engine2 = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.NotSame(engine1, engine2);
        }

        [Fact]
        public void IScriptExecutor_HasOnErrorEvent()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.NotNull(engine.GetType().GetEvent("OnError"));
        }

        [Fact]
        public void IScriptExecutor_HasRunMethod()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var result = engine.Run("1 + 1");
            Assert.Equal(2, result);
        }

        [Fact]
        public void IScriptExecutor_HasAddCustomFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("test", args => 42);
            Assert.Equal(42, engine.Run("test()"));
        }

        [Fact]
        public void IScriptExecutor_HasRemoveCustomFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("test", args => 42);
            Assert.True(engine.RemoveCustomFunction("test"));
        }

        [Fact]
        public void IScriptExecutor_HasClearCustomFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("test", args => 42);
            engine.ClearCustomFunctions();
            Assert.Empty(engine.GetCustomFunctionList());
        }

        [Fact]
        public void IScriptExecutor_HasGetCustomFunctionList()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("test", args => 42);
            var list = engine.GetCustomFunctionList();
            Assert.Contains("test", list);
        }

        [Fact]
        public void IScriptExecutor_HasAddGlobalFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddGlobalFunction("global_test", args => 1);
            Assert.Contains("global_test", engine.GetGlobalFunctionList());
        }

        [Fact]
        public void IScriptExecutor_HasAddLocalThreadFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("local_test", args => 1);
            Assert.Contains("local_test", engine.GetLocalThreadFunctionList());
        }

        [Fact]
        public void IScriptExecutor_HasRemoveGlobalFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddGlobalFunction("test", args => 1);
            Assert.True(engine.RemoveGlobalFunction("test"));
        }

        [Fact]
        public void IScriptExecutor_HasRemoveLocalThreadFunction()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("test", args => 1);
            Assert.True(engine.RemoveLocalThreadFunction("test"));
        }

        [Fact]
        public void IScriptExecutor_HasClearGlobalFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddGlobalFunction("test", args => 1);
            engine.ClearGlobalFunctions();
            Assert.DoesNotContain("test", engine.GetGlobalFunctionList());
        }

        [Fact]
        public void IScriptExecutor_HasClearLocalThreadFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("test", args => 1);
            engine.ClearLocalThreadFunctions();
            Assert.DoesNotContain("test", engine.GetLocalThreadFunctionList());
        }

        [Fact]
        public void IScriptExecutor_HasGetGlobalFunctionList()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var list = engine.GetGlobalFunctionList();
            Assert.NotNull(list);
        }

        [Fact]
        public void IScriptExecutor_HasGetLocalThreadFunctionList()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var list = engine.GetLocalThreadFunctionList();
            Assert.NotNull(list);
        }

        [Fact]
        public void IScriptExecutor_HasGetAllFunctionList()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var list = engine.GetAllFunctionList();
            Assert.NotNull(list);
        }

        [Fact]
        public void IScriptExecutor_ImplementsIDisposable()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.IsAssignableFrom<System.IDisposable>(engine);
            engine.Dispose();
        }
    }
}
