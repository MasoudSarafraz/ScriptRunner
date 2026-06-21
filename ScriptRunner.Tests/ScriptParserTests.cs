using System;
using System.Collections.Concurrent;
using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class ScriptParserTests
    {
        private ScriptParser CreateParser(string expression)
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return new ScriptParser(expression, functions, variables);
        }

        [Theory]
        [InlineData("42", 42)]
        [InlineData("0", 0)]
        [InlineData("-5", -5)]
        [InlineData("+10", 10)]
        public void Number_Literal_Int(string expr, int expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("3.14", 3.14)]
        [InlineData("0.5", 0.5)]
        [InlineData(".25", 0.25)]
        [InlineData("1e3", 1000.0)]
        [InlineData("1.5e2", 150.0)]
        [InlineData("1E-2", 0.01)]
        public void Number_Literal_Double(string expr, double expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("0x1A", 26)]
        [InlineData("0xFF", 255)]
        [InlineData("0x0", 0)]
        [InlineData("0xabcdef", 11259375)]
        public void Number_Literal_Hex(string expr, long expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("0b1010", 10)]
        [InlineData("0b0", 0)]
        [InlineData("0b1111", 15)]
        [InlineData("0b10000000", 128)]
        public void Number_Literal_Binary(string expr, long expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Fact]
        public void Number_VeryLong_FallbackToDecimal()
        {
            var parser = CreateParser("12345678901234567890");
            var result = parser.Evaluate();
            Assert.IsType<decimal>(result);
            Assert.Equal(12345678901234567890m, result);
        }

        [Theory]
        [InlineData("2 + 3", 5)]
        [InlineData("10 - 4", 6)]
        [InlineData("6 * 7", 42)]
        [InlineData("20 / 5", 4)]
        [InlineData("17 % 5", 2)]
        [InlineData("2 + 3 * 4", 14)]
        [InlineData("(2 + 3) * 4", 20)]
        [InlineData("100 / 7", 100.0 / 7.0)]
        public void Arithmetic_Basic(string expr, object expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Fact]
        public void Arithmetic_IntegerDivision_Exact_ReturnsLong()
        {
            var parser = CreateParser("10 / 2");
            var result = parser.Evaluate();
            Assert.IsType<long>(result);
            Assert.Equal(5L, result);
        }

        [Fact]
        public void Arithmetic_IntegerDivision_Inexact_ReturnsDouble()
        {
            var parser = CreateParser("10 / 3");
            var result = parser.Evaluate();
            Assert.IsType<double>(result);
            Assert.Equal(10.0 / 3.0, result);
        }

        [Fact]
        public void Arithmetic_DivisionByZero_Throws()
        {
            var parser = CreateParser("10 / 0");
            Assert.Throws<DivideByZeroException>(() => parser.Evaluate());
        }

        [Fact]
        public void Arithmetic_ModuloByZero_Throws()
        {
            var parser = CreateParser("10 % 0");
            Assert.Throws<DivideByZeroException>(() => parser.Evaluate());
        }

        [Fact]
        public void Arithmetic_LongPreserved()
        {
            var parser = CreateParser("9223372036854775807");
            var result = parser.Evaluate();
            Assert.IsType<long>(result);
            Assert.Equal(long.MaxValue, result);
        }

        [Fact]
        public void Arithmetic_LongAddition()
        {
            var parser = CreateParser("9223372036854775807 + 0");
            var result = parser.Evaluate();
            Assert.Equal(long.MaxValue, result);
        }

        [Fact]
        public void Arithmetic_DecimalPreserved()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 1.5m;
            var parser = new ScriptParser("x * 100", functions, variables);
            var result = parser.Evaluate();
            Assert.IsType<decimal>(result);
            Assert.Equal(150m, result);
        }

        [Theory]
        [InlineData("2 ** 3", 8.0)]
        [InlineData("2 ** 3 ** 2", 512.0)]
        [InlineData("4 ** 0.5", 2.0)]
        [InlineData("0 ** 0", 1.0)]
        public void Arithmetic_Exponential_RightAssociative(string expr, double expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("\"hello\"", "hello")]
        [InlineData("'world'", "world")]
        [InlineData("\"hello\\nworld\"", "hello\nworld")]
        [InlineData("\"hello\\tworld\"", "hello\tworld")]
        [InlineData("\"hello\\\\world\"", "hello\\world")]
        [InlineData("\"\\u0041\"", "A")]
        [InlineData("\"\\x41\"", "A")]
        [InlineData("\"\"", "")]
        public void String_Literals(string expr, string expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Fact]
        public void String_Concatenation()
        {
            var parser = CreateParser("\"foo\" + \"bar\"");
            Assert.Equal("foobar", parser.Evaluate());
        }

        [Fact]
        public void String_Concatenation_With_Number()
        {
            var parser = CreateParser("\"Value: \" + 42");
            Assert.Equal("Value: 42", parser.Evaluate());
        }

        [Fact]
        public void String_Unterminated_Throws()
        {
            var parser = CreateParser("\"hello");
            Assert.Throws<FormatException>(() => parser.Evaluate());
        }

        [Theory]
        [InlineData("\"a\" < \"b\"", true)]
        [InlineData("\"b\" > \"a\"", true)]
        [InlineData("\"apple\" <= \"apple\"", true)]
        [InlineData("\"apple\" >= \"banana\"", false)]
        [InlineData("\"apple\" < \"banana\"", true)]
        public void String_Comparison(string expr, bool expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("null", null)]
        public void Keywords_Literals(string expr, object expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("true && true", true)]
        [InlineData("true && false", false)]
        [InlineData("false && true", false)]
        [InlineData("false || true", true)]
        [InlineData("true || false", true)]
        [InlineData("!true", false)]
        [InlineData("!false", true)]
        public void Logical_Operators(string expr, bool expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Fact]
        public void Logical_ShortCircuit_And()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 0;
            var parser = new ScriptParser("x != 0 && 10/x > 5", functions, variables);
            Assert.Equal(false, parser.Evaluate());
        }

        [Fact]
        public void Logical_ShortCircuit_Or()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 0;
            var parser = new ScriptParser("x == 0 || 10/x > 5", functions, variables);
            Assert.Equal(true, parser.Evaluate());
        }

        [Theory]
        [InlineData("1 == 1", true)]
        [InlineData("1 == 2", false)]
        [InlineData("1 != 2", true)]
        [InlineData("1 != 1", false)]
        [InlineData("\"a\" == \"a\"", true)]
        [InlineData("\"a\" == \"b\"", false)]
        public void Equality_Operators(string expr, bool expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("1 < 2", true)]
        [InlineData("2 < 1", false)]
        [InlineData("2 <= 2", true)]
        [InlineData("3 > 2", true)]
        [InlineData("2 > 3", false)]
        [InlineData("2 >= 2", true)]
        public void Relational_Operators(string expr, bool expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Theory]
        [InlineData("5 & 3", 1L)]
        [InlineData("5 | 2", 7L)]
        [InlineData("5 ^ 3", 6L)]
        [InlineData("~5", -6L)]
        [InlineData("1 << 4", 16L)]
        [InlineData("256 >> 4", 16L)]
        public void Bitwise_Operators(string expr, object expected)
        {
            var parser = CreateParser(expr);
            Assert.Equal(expected, parser.Evaluate());
        }

        [Fact]
        public void Conditional_Ternary()
        {
            var parser = CreateParser("true ? 1 : 2");
            Assert.Equal(1, parser.Evaluate());
        }

        [Fact]
        public void Conditional_Ternary_False()
        {
            var parser = CreateParser("false ? 1 : 2");
            Assert.Equal(2, parser.Evaluate());
        }

        [Fact]
        public void Conditional_Ternary_ShortCircuit()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 0;
            var parser = new ScriptParser("x == 0 ? 99 : 10/x", functions, variables);
            Assert.Equal(99, parser.Evaluate());
        }

        [Fact]
        public void NullCoalescing_LeftNotNull()
        {
            var parser = CreateParser("\"hello\" ?? \"world\"");
            Assert.Equal("hello", parser.Evaluate());
        }

        [Fact]
        public void NullCoalescing_LeftNull()
        {
            var parser = CreateParser("null ?? \"world\"");
            Assert.Equal("world", parser.Evaluate());
        }

        [Fact]
        public void Variable_Declaration_Var()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var parser = new ScriptParser("var x = 42", functions, variables);
            Assert.Equal(42, parser.Evaluate());
        }

        [Fact]
        public void Variable_Declaration_And_Use()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var parser = new ScriptParser("var x = 10; x + 5", functions, variables);
            Assert.Equal(15, parser.Evaluate());
        }

        [Fact]
        public void Variable_Assignment()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 10;
            var parser = new ScriptParser("x = 20", functions, variables);
            Assert.Equal(20, parser.Evaluate());
        }

        [Fact]
        public void Variable_Assignment_And_Read()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 10;
            var parser = new ScriptParser("x = 20; x + 5", functions, variables);
            Assert.Equal(25, parser.Evaluate());
        }

        [Fact]
        public void Variable_PreIncrement()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 5;
            var parser = new ScriptParser("++x", functions, variables);
            Assert.Equal(6L, parser.Evaluate());
        }

        [Fact]
        public void Variable_PreDecrement()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 5;
            var parser = new ScriptParser("--x", functions, variables);
            Assert.Equal(4L, parser.Evaluate());
        }

        [Fact]
        public void Variable_PostIncrement()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 5;
            var parser = new ScriptParser("x++", functions, variables);
            Assert.Equal(5, parser.Evaluate());
        }

        [Fact]
        public void Variable_PostDecrement()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["x"] = 5;
            var parser = new ScriptParser("x--", functions, variables);
            Assert.Equal(5, parser.Evaluate());
        }

        [Fact]
        public void Array_Literal()
        {
            var parser = CreateParser("[1, 2, 3]");
            var result = parser.Evaluate();
            Assert.IsType<object[]>(result);
            var arr = (object[])result;
            Assert.Equal(3, arr.Length);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
            Assert.Equal(3, arr[2]);
        }

        [Fact]
        public void Array_Empty()
        {
            var parser = CreateParser("[]");
            var result = parser.Evaluate();
            Assert.IsType<object[]>(result);
            Assert.Equal(0, ((object[])result).Length);
        }

        [Fact]
        public void Comments_LineComment()
        {
            var parser = CreateParser("1 + 2 // this is a comment");
            Assert.Equal(3, parser.Evaluate());
        }

        [Fact]
        public void Comments_BlockComment()
        {
            var parser = CreateParser("1 + /* comment */ 2");
            Assert.Equal(3, parser.Evaluate());
        }

        [Fact]
        public void Comments_MultilineBlockComment()
        {
            var parser = CreateParser("1 + /* multi\nline\ncomment */ 2");
            Assert.Equal(3, parser.Evaluate());
        }

        [Fact]
        public void Comments_UnterminatedBlockComment_Throws()
        {
            var parser = CreateParser("1 + /* comment");
            Assert.Throws<FormatException>(() => parser.Evaluate());
        }

        [Fact]
        public void MultipleStatements_Semicolon()
        {
            var parser = CreateParser("var x = 5; var y = 10; x + y");
            Assert.Equal(15, parser.Evaluate());
        }

        [Fact]
        public void MultipleStatements_TrailingSemicolon()
        {
            var parser = CreateParser("1 + 2;");
            Assert.Equal(3, parser.Evaluate());
        }

        [Fact]
        public void Keyword_NotMatchedAsPrefix_True()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["trueValue"] = 42;
            var parser = new ScriptParser("trueValue", functions, variables);
            Assert.Equal(42, parser.Evaluate());
        }

        [Fact]
        public void Keyword_NotMatchedAsPrefix_False()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["falseValue"] = 99;
            var parser = new ScriptParser("falseValue", functions, variables);
            Assert.Equal(99, parser.Evaluate());
        }

        [Fact]
        public void Keyword_NotMatchedAsPrefix_Null()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["nullable"] = "test";
            var parser = new ScriptParser("nullable ?? \"default\"", functions, variables);
            Assert.Equal("test", parser.Evaluate());
        }

        [Fact]
        public void Keyword_NotMatchedAsPrefix_Var()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["variance"] = 100;
            var parser = new ScriptParser("variance + 1", functions, variables);
            Assert.Equal(101, parser.Evaluate());
        }

        [Fact]
        public void FunctionCall_Custom()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            functions["double"] = args => Convert.ToInt32(args[0]) * 2;
            var parser = new ScriptParser("double(21)", functions, variables);
            Assert.Equal(42, parser.Evaluate());
        }

        [Fact]
        public void FunctionCall_Unknown_Throws()
        {
            var parser = CreateParser("unknown(1)");
            Assert.Throws<FormatException>(() => parser.Evaluate());
        }

        [Fact]
        public void FunctionCall_Nested()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            functions["add"] = args => Convert.ToInt32(args[0]) + Convert.ToInt32(args[1]);
            functions["double"] = args => Convert.ToInt32(args[0]) * 2;
            var parser = new ScriptParser("double(add(1, 2))", functions, variables);
            Assert.Equal(6, parser.Evaluate());
        }

        [Fact]
        public void MemberAccess_Property()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["dt"] = new DateTime(2023, 6, 15);
            var parser = new ScriptParser("dt.Year", functions, variables);
            Assert.Equal(2023, parser.Evaluate());
        }

        [Fact]
        public void MemberAccess_Chained()
        {
            var functions = new ConcurrentDictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            var variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            variables["dt"] = new DateTime(2023, 6, 15);
            var parser = new ScriptParser("dt.Year.ToString()", functions, variables);
            Assert.Equal("2023", parser.Evaluate());
        }

        [Fact]
        public void EmptyExpression_ReturnsNull()
        {
            var parser = CreateParser("");
            Assert.Null(parser.Evaluate());
        }

        [Fact]
        public void WhitespaceOnly_ReturnsNull()
        {
            var parser = CreateParser("   ");
            Assert.Null(parser.Evaluate());
        }

        [Fact]
        public void UnexpectedCharacter_Throws()
        {
            var parser = CreateParser("@#$");
            Assert.Throws<FormatException>(() => parser.Evaluate());
        }
    }
}
