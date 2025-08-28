using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ScriptEngine
{
    public class ExpressionParser
    {
        private readonly string _expression;
        private int _position;
        private readonly ConcurrentDictionary<string, Func<object[], object>> _functions;
        private readonly ConcurrentDictionary<string, object> _variables;
        private readonly Dictionary<string, object> _localVariables;
        private readonly Stack<Dictionary<string, object>> _scopeStack;
        public ExpressionParser(string expression, ConcurrentDictionary<string, Func<object[], object>> functions, ConcurrentDictionary<string, object> variables)
        {
            _expression = expression?.Trim() ?? "";
            _position = 0;
            _functions = functions;
            _variables = variables;
            _localVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _scopeStack = new Stack<Dictionary<string, object>>();
        }
        public object Evaluate()
        {
            PushScope();
            try
            {
                var result = ParseExpression();
                SkipWhitespace();

                if (_position < _expression.Length)
                    throw new FormatException($"Unexpected character at position {_position}: '{_expression[_position]}' in expression: '{_expression}'");

                return result;
            }
            finally
            {
                PopScope();
            }
        }

        private void PushScope()
        {
            _scopeStack.Push(new Dictionary<string, object>(_localVariables, StringComparer.OrdinalIgnoreCase));
        }

        private void PopScope()
        {
            if (_scopeStack.Count > 0)
            {
                _localVariables.Clear();
                foreach (var item in _scopeStack.Pop())
                {
                    _localVariables[item.Key] = item.Value;
                }
            }
        }

        private object ParseExpression()
        {
            return ParseAssignment();
        }
        private object ParseAssignment()
        {
            // بررسی برای تعریف متغیر (var x = 10)
            if (Match("var", true))
            {
                SkipWhitespace();
                string varName2 = ParseIdentifierName();
                SkipWhitespace();

                if (!Match("="))
                    throw new FormatException("Expected '=' after variable declaration");

                SkipWhitespace();
                object value = ParseExpression();
                _localVariables[varName2] = value;
                return value;
            }

            // بررسی برای انتساب متغیر (x = 10)
            var left = ParseConditionalExpression();

            if (Match("=") && left is string varName)
            {
                object value = ParseExpression();

                // اولویت با متغیرهای محلی، سپس متغیرهای thread-local، سپس متغیرهای global
                if (_localVariables.ContainsKey(varName))
                {
                    _localVariables[varName] = value;
                }
                else if (_variables.ContainsKey(varName))
                {
                    _variables[varName] = value;
                }
                else
                {
                    _localVariables[varName] = value;
                }

                return value;
            }

            return left;
        }

        private object ParseConditionalExpression()
        {
            var condition = ParseNullCoalescing();

            if (Match("?"))
            {
                var trueValue = ParseExpression();
                Expect(":");
                var falseValue = ParseExpression();
                return Convert.ToBoolean(condition) ? trueValue : falseValue;
            }

            return condition;
        }

        private object ParseNullCoalescing()
        {
            var left = ParseLogicalOr();

            while (Match("??"))
            {
                var right = ParseLogicalOr();
                left = left ?? right;
            }

            return left;
        }
        private object ParseLogicalOr()
        {
            var left = ParseLogicalAnd();

            while (Match("||"))
            {
                var right = ParseLogicalAnd();
                left = Convert.ToBoolean(left) || Convert.ToBoolean(right);
            }

            return left;
        }
        private object ParseLogicalAnd()
        {
            var left = ParseBitwiseOr();

            while (Match("&&"))
            {
                var right = ParseBitwiseOr();
                left = Convert.ToBoolean(left) && Convert.ToBoolean(right);
            }

            return left;
        }

        private object ParseBitwiseOr()
        {
            var left = ParseBitwiseXor();

            while (Match("|"))
            {
                var right = ParseBitwiseXor();
                left = Convert.ToInt32(left) | Convert.ToInt32(right);
            }

            return left;
        }

        private object ParseBitwiseXor()
        {
            var left = ParseBitwiseAnd();

            while (Match("^"))
            {
                var right = ParseBitwiseAnd();
                left = Convert.ToInt32(left) ^ Convert.ToInt32(right);
            }

            return left;
        }

        private object ParseBitwiseAnd()
        {
            var left = ParseEquality();

            while (Match("&"))
            {
                var right = ParseEquality();
                left = Convert.ToInt32(left) & Convert.ToInt32(right);
            }

            return left;
        }

        private object ParseEquality()
        {
            var left = ParseRelational();

            while (true)
            {
                if (Match("==") || Match("="))
                {
                    var right = ParseRelational();
                    left = Equals(left, right);
                }
                else if (Match("!="))
                {
                    var right = ParseRelational();
                    left = !Equals(left, right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private object ParseRelational()
        {
            var left = ParseShift();

            while (true)
            {
                if (Match("<="))
                {
                    var right = ParseShift();
                    left = Convert.ToDouble(left) <= Convert.ToDouble(right);
                }
                else if (Match(">="))
                {
                    var right = ParseShift();
                    left = Convert.ToDouble(left) >= Convert.ToDouble(right);
                }
                else if (Match("<"))
                {
                    var right = ParseShift();
                    left = Convert.ToDouble(left) < Convert.ToDouble(right);
                }
                else if (Match(">"))
                {
                    var right = ParseShift();
                    left = Convert.ToDouble(left) > Convert.ToDouble(right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private object ParseShift()
        {
            var left = ParseAdditive();

            while (true)
            {
                if (Match("<<"))
                {
                    var right = ParseAdditive();
                    left = Convert.ToInt32(left) << Convert.ToInt32(right);
                }
                else if (Match(">>"))
                {
                    var right = ParseAdditive();
                    left = Convert.ToInt32(left) >> Convert.ToInt32(right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private object ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (true)
            {
                if (Match("+"))
                {
                    var right = ParseMultiplicative();
                    left = Add(left, right);
                }
                else if (Match("-"))
                {
                    var right = ParseMultiplicative();
                    left = Subtract(left, right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private object ParseMultiplicative()
        {
            var left = ParseExponential();

            while (true)
            {
                if (Match("*"))
                {
                    var right = ParseExponential();
                    left = Multiply(left, right);
                }
                else if (Match("/"))
                {
                    var right = ParseExponential();
                    left = Divide(left, right);
                }
                else if (Match("%"))
                {
                    var right = ParseExponential();
                    left = Modulo(left, right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private object ParseExponential()
        {
            var left = ParseUnary();

            while (Match("**"))
            {
                var right = ParseUnary();
                left = Math.Pow(Convert.ToDouble(left), Convert.ToDouble(right));
            }

            return left;
        }

        private object ParseUnary()
        {
            if (Match("+")) return ParseUnary();
            if (Match("-")) return Negate(ParseUnary());
            if (Match("!")) return Not(ParseUnary());
            if (Match("~")) return BitwiseNot(ParseUnary());
            if (Match("++")) return PreIncrement();
            if (Match("--")) return PreDecrement();

            // Postfix operators
            var result = ParsePrimary();

            if (Match("++")) return PostIncrement(result);
            if (Match("--")) return PostDecrement(result);

            return result;
        }

        private object ParsePrimary()
        {
            SkipWhitespace();

            if (_position >= _expression.Length)
                throw new FormatException("Unexpected end of expression");

            // بررسی عدد
            if (char.IsDigit(_expression[_position]) || _expression[_position] == '.')
                return ParseNumber();

            // بررسی رشته
            if (_expression[_position] == '"' || _expression[_position] == '\'')
                return ParseString();

            // بررسی آرایه
            if (_expression[_position] == '[')
                return ParseArray();

            // بررسی پرانتز
            if (Match("("))
            {
                var result = ParseExpression();
                Expect(")");
                return result;
            }

            // بررسی مقادیر بولین و null
            if (Match("true", true)) return true;
            if (Match("false", true)) return false;
            if (Match("null", true)) return null;

            // بررسی توابع، متغیرها و خصوصیات
            if (char.IsLetter(_expression[_position]) || _expression[_position] == '_')
                return ParseIdentifier();

            throw new FormatException($"Unexpected character at position {_position}: '{_expression[_position]}' in expression: '{_expression}'");
        }

        private object ParseNumber()
        {
            int start = _position;
            bool hasDecimal = false;
            bool hasExponent = false;

            while (_position < _expression.Length)
            {
                char c = _expression[_position];

                if (char.IsDigit(c))
                {
                    _position++;
                }
                else if (c == '.' && !hasDecimal && !hasExponent)
                {
                    hasDecimal = true;
                    _position++;
                }
                else if ((c == 'e' || c == 'E') && !hasExponent)
                {
                    hasExponent = true;
                    _position++;

                    if (_position < _expression.Length && (_expression[_position] == '+' || _expression[_position] == '-'))
                    {
                        _position++;
                    }
                }
                else
                {
                    break;
                }
            }

            string numberStr = _expression.Substring(start, _position - start);

            if (hasDecimal || hasExponent)
            {
                if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    return result;
            }
            else
            {
                if (int.TryParse(numberStr, out int result))
                    return result;

                if (long.TryParse(numberStr, out long longResult))
                    return longResult;
            }

            throw new FormatException($"Invalid number format: '{numberStr}'");
        }
        private string ParseString()
        {
            char quoteChar = _expression[_position];
            Expect(quoteChar.ToString());

            StringBuilder sb = new StringBuilder();
            bool escape = false;

            while (_position < _expression.Length)
            {
                char c = _expression[_position];
                _position++;

                if (escape)
                {
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '0': sb.Append('\0'); break;
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'v': sb.Append('\v'); break;
                        case 'u':
                            // Unicode escape
                            if (_position + 4 <= _expression.Length)
                            {
                                string hex = _expression.Substring(_position, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    _position += 4;
                                }
                                else
                                {
                                    throw new FormatException("Invalid unicode escape sequence");
                                }
                            }
                            else
                            {
                                throw new FormatException("Incomplete unicode escape sequence");
                            }
                            break;
                        case 'x':
                            // Hex escape
                            if (_position + 2 <= _expression.Length)
                            {
                                string hex = _expression.Substring(_position, 2);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    _position += 2;
                                }
                                else
                                {
                                    throw new FormatException("Invalid hex escape sequence");
                                }
                            }
                            else
                            {
                                throw new FormatException("Incomplete hex escape sequence");
                            }
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == quoteChar)
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (_position > _expression.Length)
                throw new FormatException("Unterminated string literal");

            return sb.ToString();
        }

        private object ParseArray()
        {
            Expect("[");
            var elements = new List<object>();

            if (!Match("]"))
            {
                do
                {
                    elements.Add(ParseExpression());
                }
                while (Match(","));

                Expect("]");
            }

            return elements.ToArray();
        }

        private object ParseIdentifier()
        {
            string identifier = ParseIdentifierName();
            SkipWhitespace();

            // بررسی اگر تابع است
            if (_position < _expression.Length && _expression[_position] == '(')
            {
                return ParseFunctionCall(identifier);
            }

            // بررسی اگر خصوصیت یا متد است
            if (Match("."))
            {
                return ParseMemberAccess(identifier);
            }

            // بررسی اگر متغیر است
            return GetVariableValue(identifier);
        }

        private string ParseIdentifierName()
        {
            int start = _position;

            if (_position < _expression.Length && (char.IsLetter(_expression[_position]) || _expression[_position] == '_'))
            {
                _position++;

                while (_position < _expression.Length &&
                      (char.IsLetterOrDigit(_expression[_position]) || _expression[_position] == '_'))
                {
                    _position++;
                }
            }

            return _expression.Substring(start, _position - start);
        }

        private object ParseFunctionCall(string functionName)
        {
            Expect("(");
            var parameters = new List<object>();

            if (!Match(")"))
            {
                do
                {
                    parameters.Add(ParseExpression());
                }
                while (Match(","));

                Expect(")");
            }

            if (_functions.TryGetValue(functionName, out var function))
            {
                try
                {
                    return function(parameters.ToArray());
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Error executing function '{functionName}': {ex.Message}");
                }
            }

            throw new FormatException($"Unknown function: '{functionName}'");
        }

        private object ParseMemberAccess(object target)
        {
            string memberName = ParseIdentifierName();
            SkipWhitespace();

            // بررسی اگر متد است
            if (_position < _expression.Length && _expression[_position] == '(')
            {
                return ParseMethodCall(target, memberName);
            }

            // بررسی اگر خصوصیت است
            return GetMemberValue(target, memberName);
        }

        private object ParseMethodCall(object target, string methodName)
        {
            Expect("(");
            var parameters = new List<object>();

            if (!Match(")"))
            {
                do
                {
                    parameters.Add(ParseExpression());
                }
                while (Match(","));

                Expect(")");
            }

            // استفاده از reflection برای فراخوانی متد
            try
            {
                var method = target.GetType().GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                    null, parameters.Select(p => p?.GetType() ?? typeof(object)).ToArray(), null);

                if (method != null)
                {
                    return method.Invoke(target, parameters.ToArray());
                }

                throw new FormatException($"Unknown method: '{methodName}'");
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error calling method '{methodName}': {ex.Message}");
            }
        }

        private object GetMemberValue(object target, string memberName)
        {
            // استفاده از reflection برای دسترسی به خصوصیت
            try
            {
                var property = target.GetType().GetProperty(memberName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    return property.GetValue(target, null);
                }

                var field = target.GetType().GetField(memberName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    return field.GetValue(target);
                }

                throw new FormatException($"Unknown member: '{memberName}'");
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error accessing member '{memberName}': {ex.Message}");
            }
        }

        private object GetVariableValue(string variableName)
        {
            // اولویت با متغیرهای محلی، سپس متغیرهای سراسری
            if (_localVariables.TryGetValue(variableName, out object value))
            {
                return value;
            }

            if (_variables.TryGetValue(variableName, out value))
            {
                return value;
            }

            throw new FormatException($"Unknown variable: '{variableName}'");
        }

        // عملگرهای ریاضی با پشتیبانی از انواع مختلف
        private object Add(object left, object right)
        {
            if (left is string || right is string)
                return $"{left}{right}";

            return Convert.ToDouble(left) + Convert.ToDouble(right);
        }

        private object Subtract(object left, object right)
        {
            return Convert.ToDouble(left) - Convert.ToDouble(right);
        }

        private object Multiply(object left, object right)
        {
            return Convert.ToDouble(left) * Convert.ToDouble(right);
        }

        private object Divide(object left, object right)
        {
            if (Convert.ToDouble(right) == 0)
                throw new DivideByZeroException("Division by zero");

            return Convert.ToDouble(left) / Convert.ToDouble(right);
        }

        private object Modulo(object left, object right)
        {
            if (Convert.ToDouble(right) == 0)
                throw new DivideByZeroException("Division by zero in modulo operation");

            return Convert.ToDouble(left) % Convert.ToDouble(right);
        }

        private object Negate(object value)
        {
            return -Convert.ToDouble(value);
        }

        private object Not(object value)
        {
            return !Convert.ToBoolean(value);
        }

        private object BitwiseNot(object value)
        {
            return ~Convert.ToInt32(value);
        }

        private object PreIncrement()
        {
            // فقط برای متغیرها قابل استفاده است
            string varName = ParseIdentifierName();
            object value = GetVariableValue(varName);
            value = Convert.ToDouble(value) + 1;
            SetVariableValue(varName, value);
            return value;
        }

        private object PreDecrement()
        {
            string varName = ParseIdentifierName();
            object value = GetVariableValue(varName);
            value = Convert.ToDouble(value) - 1;
            SetVariableValue(varName, value);
            return value;
        }

        private object PostIncrement(object value)
        {
            if (!(value is string varName))
                throw new FormatException("Post-increment can only be applied to variables");

            object oldValue = GetVariableValue(varName);
            SetVariableValue(varName, Convert.ToDouble(oldValue) + 1);
            return oldValue;
        }

        private object PostDecrement(object value)
        {
            if (!(value is string varName))
                throw new FormatException("Post-decrement can only be applied to variables");

            object oldValue = GetVariableValue(varName);
            SetVariableValue(varName, Convert.ToDouble(oldValue) - 1);
            return oldValue;
        }

        private void SetVariableValue(string varName, object value)
        {
            if (_localVariables.ContainsKey(varName))
            {
                _localVariables[varName] = value;
            }
            else if (_variables.ContainsKey(varName))
            {
                _variables[varName] = value;
            }
            else
            {
                _localVariables[varName] = value;
            }
        }

        private void SkipWhitespace()
        {
            while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
            {
                _position++;
            }
        }

        private bool Match(string expected, bool ignoreCase = false)
        {
            SkipWhitespace();

            if (_position + expected.Length > _expression.Length)
                return false;

            string actual = _expression.Substring(_position, expected.Length);
            bool matches = ignoreCase
                ? string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                : actual == expected;

            if (matches)
                _position += expected.Length;

            return matches;
        }

        private void Expect(string expected)
        {
            if (!Match(expected))
                throw new FormatException($"Expected '{expected}' at position {_position} in expression: '{_expression}'");
        }
    }
}