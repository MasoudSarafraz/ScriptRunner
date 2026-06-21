using System;
using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class LocalScriptExecutorTests
    {
        [Theory]
        [InlineData("1 + 2", 3)]
        [InlineData("10 * 5", 50)]
        [InlineData("100 / 4", 25)]
        [InlineData("2 + 3 * 4", 14)]
        [InlineData("(2 + 3) * 4", 20)]
        public void Run_BasicArithmetic(string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Fact]
        public void Run_NullOrWhitespace_ReturnsNull()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Null(engine.Run(null));
            Assert.Null(engine.Run(""));
            Assert.Null(engine.Run("   "));
        }

        [Theory]
        [InlineData("sqrt", "sqrt(16)", 4.0)]
        [InlineData("pow", "pow(2, 10)", 1024.0)]
        [InlineData("abs", "abs(-42)", 42.0)]
        [InlineData("ceil", "ceil(3.2)", 4.0)]
        [InlineData("floor", "floor(3.8)", 3.0)]
        [InlineData("round", "round(3.5)", 4.0)]
        [InlineData("exp", "exp(0)", 1.0)]
        public void BuiltIn_MathFunctions(string func, string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Theory]
        [InlineData("sin(0)", 0.0)]
        [InlineData("cos(0)", 1.0)]
        [InlineData("tan(0)", 0.0)]
        public void BuiltIn_TrigFunctions(string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Theory]
        [InlineData("length(\"hello\")", 5)]
        [InlineData("toupper(\"hello\")", "HELLO")]
        [InlineData("tolower(\"HELLO\")", "hello")]
        [InlineData("trim(\"  hi  \")", "hi")]
        public void BuiltIn_StringFunctions(string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Fact]
        public void BuiltIn_StringFunctions_Substring()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal("ell", engine.Run("substring(\"hello\", 1, 3)"));
        }

        [Fact]
        public void BuiltIn_StringFunctions_Concat()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal("foobar", engine.Run("concat(\"foo\", \"bar\")"));
        }

        [Fact]
        public void BuiltIn_Length_Null_ReturnsZero()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", null);
            Assert.Equal(0, engine.Run("length(x)"));
        }

        [Theory]
        [InlineData("year(now())", 0)]
        public void BuiltIn_DateFunctions(string expr, int _)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var now = DateTime.Now;
            Assert.Equal(now.Year, engine.Run("year(now())"));
        }

        [Theory]
        [InlineData("iif(true, 1, 2)", 1)]
        [InlineData("iif(false, 1, 2)", 2)]
        [InlineData("iif(1 == 1, \"yes\", \"no\")", "yes")]
        public void BuiltIn_Conditional_Iif(string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Fact]
        public void BuiltIn_Conditional_Iif_ShortCircuit()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", 0);
            Assert.Equal(99, engine.Run("iif(x == 0, 99, 10/x)"));
        }

        [Fact]
        public void BuiltIn_Conditional_Iif_InvalidParams_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.ThrowsAny<Exception>(() => engine.Run("iif(true, 1)"));
        }

        [Fact]
        public void BuiltIn_Conditional_Coalesce_FirstNonNull()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", null);
            Assert.Equal("default", engine.Run("coalesce(x, \"default\")"));
        }

        [Fact]
        public void BuiltIn_Conditional_Coalesce_ShortCircuit()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", 0);
            Assert.Equal(99, engine.Run("coalesce(x == 0 ? 99 : null, 10/x)"));
        }

        [Theory]
        [InlineData("isnull(null)", true)]
        [InlineData("isnull(42)", false)]
        [InlineData("isnumber(42)", true)]
        [InlineData("isnumber(\"42\")", false)]
        [InlineData("isstring(\"42\")", true)]
        [InlineData("isstring(42)", false)]
        public void BuiltIn_TypeCheck(string expr, bool expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Fact]
        public void BuiltIn_Array_Count()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(3, engine.Run("count([1, 2, 3])"));
        }

        [Fact]
        public void BuiltIn_Array_Sum()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(6.0, engine.Run("sum([1, 2, 3])"));
        }

        [Fact]
        public void BuiltIn_Array_Sum_Empty()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(0, engine.Run("sum([])"));
        }

        [Fact]
        public void BuiltIn_Array_Avg()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(2.0, engine.Run("avg([1, 2, 3])"));
        }

        [Fact]
        public void BuiltIn_Array_Avg_Empty_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.ThrowsAny<Exception>(() => engine.Run("avg([])"));
        }

        [Fact]
        public void BuiltIn_Array_Min()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(1.0, engine.Run("min([3, 1, 2])"));
        }

        [Fact]
        public void BuiltIn_Array_Max()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(3.0, engine.Run("max([1, 3, 2])"));
        }

        [Fact]
        public void BuiltIn_Array_Min_Empty_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.ThrowsAny<Exception>(() => engine.Run("min([])"));
        }

        [Fact]
        public void BuiltIn_Count_NonArray_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.ThrowsAny<Exception>(() => engine.Run("count(42)"));
        }

        [Theory]
        [InlineData("PI", 3.141592653589793)]
        [InlineData("E", 2.718281828459045)]
        [InlineData("TRUE", true)]
        [InlineData("FALSE", false)]
        public void BuiltIn_Constants(string expr, object expected)
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(expected, engine.Run(expr));
        }

        [Fact]
        public void BuiltIn_Constant_Null()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Null(engine.Run("NULL"));
        }

        [Fact]
        public void GlobalVariable_SetAndGet()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", 42);
            Assert.Equal(42, engine.Run("x"));
        }

        [Fact]
        public void GlobalVariable_Remove()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("temp", 99);
            Assert.True(engine.RemoveGlobalVariable("temp"));
            Assert.ThrowsAny<Exception>(() => engine.Run("temp"));
        }

        [Fact]
        public void GlobalVariable_Remove_NonExistent_ReturnsFalse()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.False(engine.RemoveGlobalVariable("nonexistent"));
        }

        [Fact]
        public void GlobalVariable_Get_NonExistent_ReturnsNull()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Null(engine.GetGlobalVariable("nonexistent"));
        }

        [Fact]
        public void GlobalVariable_Set_EmptyName_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Throws<ArgumentException>(() => engine.SetGlobalVariable("", 1));
        }

        [Fact]
        public void ThreadLocalVariable_SetAndGet()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetThreadLocalVariable("y", 77);
            Assert.Equal(77, engine.Run("y"));
        }

        [Fact]
        public void ThreadLocalVariable_Remove()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetThreadLocalVariable("temp", 1);
            Assert.True(engine.RemoveLocalThreadVariable("temp"));
        }

        [Fact]
        public void ThreadLocalVariable_OverridesGlobal()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", 1);
            engine.SetThreadLocalVariable("x", 2);
            Assert.Equal(2, engine.Run("x"));
        }

        [Fact]
        public void CustomFunction_AddAndCall()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("square", args => Convert.ToInt32(args[0]) * Convert.ToInt32(args[0]));
            Assert.Equal(25, engine.Run("square(5)"));
        }

        [Fact]
        public void CustomFunction_AddGlobalAndCall()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
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
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("temp", args => 1);
            Assert.True(engine.RemoveCustomFunction("temp"));
            Assert.ThrowsAny<Exception>(() => engine.Run("temp()"));
        }

        [Fact]
        public void CustomFunction_Remove_NonExistent_ReturnsFalse()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.False(engine.RemoveCustomFunction("nonexistent"));
        }

        [Fact]
        public void CustomFunction_Clear()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddCustomFunction("a", args => 1);
            engine.AddCustomFunction("b", args => 2);
            engine.ClearCustomFunctions();
            Assert.Empty(engine.GetCustomFunctionList());
        }

        [Fact]
        public void CustomFunction_Add_EmptyName_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Throws<ArgumentException>(() => engine.AddCustomFunction("", args => 1));
        }

        [Fact]
        public void CustomFunction_Add_NullFunction_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Throws<ArgumentNullException>(() => engine.AddCustomFunction("x", null));
        }

        [Fact]
        public void FunctionList_GlobalFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            var list = engine.GetGlobalFunctionList();
            Assert.Contains("sqrt", list);
            Assert.Contains("iif", list);
            Assert.Contains("length", list);
        }

        [Fact]
        public void FunctionList_LocalThreadFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("custom", args => 1);
            var list = engine.GetLocalThreadFunctionList();
            Assert.Contains("custom", list);
        }

        [Fact]
        public void FunctionList_AllFunctions()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("custom1", args => 1);
            var list = engine.GetAllFunctionList();
            Assert.Contains("sqrt", list);
            Assert.Contains("custom1", list);
        }

        [Fact]
        public void FunctionList_Distinct_WhenBothDefined()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("sqrt", args => 999);
            var list = engine.GetAllFunctionList();
            Assert.Single(list.FindAll(x => string.Equals(x, "sqrt", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void VariablePersistence_VarDeclaration()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.Run("var counter = 0");
            Assert.Equal(0, engine.Run("counter"));
        }

        [Fact]
        public void VariablePersistence_Assignment()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", 10);
            engine.Run("x = 50");
            Assert.Equal(50, engine.GetGlobalVariable("x"));
        }

        [Fact]
        public void VariablePersistence_MultipleRuns()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.Run("var total = 0");
            engine.Run("total = total + 10");
            engine.Run("total = total + 20");
            Assert.Equal(30, engine.Run("total"));
        }

        [Fact]
        public void Comments_Supported()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(3, engine.Run("1 + 2 // comment"));
            Assert.Equal(3, engine.Run("1 + /* block */ 2"));
        }

        [Fact]
        public void MultipleStatements_Supported()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(15, engine.Run("var x = 5; var y = 10; x + y"));
        }

        [Fact]
        public void HexAndBinaryNumbers_Supported()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(255L, engine.Run("0xFF"));
            Assert.Equal(10L, engine.Run("0b1010"));
        }

        [Fact]
        public void ErrorHandling_OnErrorEvent()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Exception caught = null;
            engine.OnError += (sender, e) => caught = e.Exception;

            try { engine.Run("unknown_function()"); } catch { }

            Assert.NotNull(caught);
            Assert.Contains("Unknown function", caught.Message);
        }

        [Fact]
        public void ErrorHandling_PropagatesException()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Throws<FormatException>(() => engine.Run("1 / 0"));
        }

        [Fact]
        public void GlobalUnhandledErrorHandler_Static()
        {
            Exception caught = null;
            LocalScriptExecutor.GlobalUnhandledErrorHandler = ex => caught = ex;

            try
            {
                var engine = ScriptEngineFactory.CreateLocalScriptEngine();
                try { engine.Run("unknown_xyz()"); } catch { }
                Assert.NotNull(caught);
            }
            finally
            {
                LocalScriptExecutor.GlobalUnhandledErrorHandler = null;
            }
        }

        [Fact]
        public void Dispose_DoesNotBreakSingleton()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddLocalThreadFunction("temp", args => 1);
            engine.Dispose();

            var engine2 = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(2, engine2.Run("1 + 1"));
        }

        [Fact]
        public void ClearGlobalFunctions_RemovesAll()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.AddGlobalFunction("custom", args => 1);
            engine.ClearGlobalFunctions();
            Assert.DoesNotContain("custom", engine.GetGlobalFunctionList());
        }

        [Fact]
        public void CaseInsensitivity_FunctionNames()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            Assert.Equal(4.0, engine.Run("SQRT(16)"));
            Assert.Equal(4.0, engine.Run("Sqrt(16)"));
            Assert.Equal(4.0, engine.Run("sqrt(16)"));
        }

        [Fact]
        public void CaseInsensitivity_VariableNames()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("MyVar", 42);
            Assert.Equal(42, engine.Run("myvar"));
            Assert.Equal(42, engine.Run("MYVAR"));
        }

        [Fact]
        public void MemberAccess_DateTimeProperty()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("dt", new DateTime(2023, 6, 15, 10, 30, 0));
            Assert.Equal(2023, engine.Run("dt.Year"));
            Assert.Equal(6, engine.Run("dt.Month"));
            Assert.Equal(15, engine.Run("dt.Day"));
            Assert.Equal(10, engine.Run("dt.Hour"));
        }

        [Fact]
        public void MemberAccess_StringProperty()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("s", "hello");
            Assert.Equal(5, engine.Run("s.Length"));
        }

        [Fact]
        public void MemberAccess_ChainedMethods()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("s", "  hello  ");
            Assert.Equal("HELLO", engine.Run("s.Trim().ToUpper()"));
        }

        [Fact]
        public void MemberAccess_NullTarget_Throws()
        {
            var engine = ScriptEngineFactory.CreateLocalScriptEngine();
            engine.SetGlobalVariable("x", null);
            Assert.ThrowsAny<Exception>(() => engine.Run("x.Length"));
        }
    }
}
