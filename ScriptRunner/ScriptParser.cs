using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ScriptEngine
{
    internal sealed class ScriptParser
    {
        private readonly string _expression;
        private int _position;
        private readonly ConcurrentDictionary<string, Func<object[], object>> _functions;
        private readonly ConcurrentDictionary<string, object> _variables;
        private readonly Dictionary<string, object> _localVariables;
        private readonly Action<string, object> _setVariableCallback;
        private int _skipDepth;

        public ScriptParser(string expression, ConcurrentDictionary<string, Func<object[], object>> functions, ConcurrentDictionary<string, object> variables)
            : this(expression, functions, variables, null)
        {
        }

        public ScriptParser(string expression, ConcurrentDictionary<string, Func<object[], object>> functions, ConcurrentDictionary<string, object> variables, Action<string, object> setVariableCallback)
        {
            _expression = expression?.Trim() ?? "";
            _position = 0;
            _functions = functions;
            _variables = variables;
            _localVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _setVariableCallback = setVariableCallback;
            _skipDepth = 0;
        }

        private bool IsSkipping => _skipDepth > 0;

        private void BeginSkip() { _skipDepth++; }
        private void EndSkip() { if (_skipDepth > 0) _skipDepth--; }

        public object Evaluate()
        {
            if (string.IsNullOrEmpty(_expression))
                return null;

            var result = ParseStatementList();
            SkipWhitespace();

            if (_position < _expression.Length)
                throw new FormatException($"Unexpected character at position {_position}: '{_expression[_position]}' in expression: '{_expression}'");

            return result;
        }

        private object ParseStatementList()
        {
            object result = ParseExpression();

            while (true)
            {
                SkipWhitespace();
                if (!Match(";"))
                    break;

                SkipWhitespace();
                if (_position >= _expression.Length)
                    break;

                result = ParseExpression();
            }

            return result;
        }

        private object ParseExpression()
        {
            return ParseAssignment();
        }

        private object ParseAssignment()
        {

            if (MatchWord("var"))
            {
                SkipWhitespace();
                string varName = ParseIdentifierName();
                if (string.IsNullOrEmpty(varName))
                    throw new FormatException("Expected variable name after 'var'");

                SkipWhitespace();
                Expect("=");
                SkipWhitespace();
                object value = ParseExpression();
                SetVariableValue(varName, value);
                return value;
            }

            int savedPos = _position;
            string peekedName = TryPeekIdentifier();

            if (peekedName != null && !IsKeyword(peekedName))
            {
                SkipWhitespace();
                if (PeekChar() == '=' && PeekChar(1) != '=')
                {
                    _position++;
                    SkipWhitespace();
                    object value = ParseExpression();
                    SetVariableValue(peekedName, value);
                    return value;
                }
            }

            _position = savedPos;
            return ParseConditionalExpression();
        }

        private object ParseConditionalExpression()
        {
            var condition = ParseNullCoalescing();

            SkipWhitespace();
            if (PeekChar() != '?')
                return condition;

            if (PeekChar(1) == '?')
                return condition;

            _position++;
            SkipWhitespace();

            if (IsSkipping)
            {
                ParseExpression();
                Expect(":");
                ParseExpression();
                return null;
            }

            bool cond = Convert.ToBoolean(condition);
            if (cond)
            {
                var trueValue = ParseExpression();
                Expect(":");
                BeginSkip();
                try { ParseExpression(); }
                finally { EndSkip(); }
                return trueValue;
            }
            else
            {
                BeginSkip();
                try { ParseExpression(); }
                finally { EndSkip(); }
                Expect(":");
                return ParseExpression();
            }
        }

        private object ParseNullCoalescing()
        {
            var left = ParseLogicalOr();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '?' && PeekChar(1) == '?')
                {
                    _position += 2;
                    if (left != null && !IsSkipping)
                    {
                        BeginSkip();
                        try { ParseLogicalOr(); }
                        finally { EndSkip(); }
                    }
                    else
                    {
                        left = ParseLogicalOr();
                    }
                }
                else break;
            }

            return left;
        }

        private object ParseLogicalOr()
        {
            var left = ParseLogicalAnd();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '|' && PeekChar(1) == '|')
                {
                    _position += 2;
                    bool leftBool = Convert.ToBoolean(left);
                    if (leftBool && !IsSkipping)
                    {
                        BeginSkip();
                        try { ParseLogicalAnd(); }
                        finally { EndSkip(); }
                        left = true;
                    }
                    else
                    {
                        var right = ParseLogicalAnd();
                        left = leftBool || Convert.ToBoolean(right);
                    }
                }
                else break;
            }

            return left;
        }

        private object ParseLogicalAnd()
        {
            var left = ParseBitwiseOr();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '&' && PeekChar(1) == '&')
                {
                    _position += 2;
                    bool leftBool = Convert.ToBoolean(left);
                    if (!leftBool && !IsSkipping)
                    {
                        BeginSkip();
                        try { ParseBitwiseOr(); }
                        finally { EndSkip(); }
                        left = false;
                    }
                    else
                    {
                        var right = ParseBitwiseOr();
                        left = leftBool && Convert.ToBoolean(right);
                    }
                }
                else break;
            }

            return left;
        }

        private object ParseBitwiseOr()
        {
            var left = ParseBitwiseXor();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '|' && PeekChar(1) != '|')
                {
                    _position++;
                    var right = ParseBitwiseXor();
                    if (!IsSkipping)
                        left = Convert.ToInt64(left) | Convert.ToInt64(right);
                }
                else break;
            }

            return left;
        }

        private object ParseBitwiseXor()
        {
            var left = ParseBitwiseAnd();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '^')
                {
                    _position++;
                    var right = ParseBitwiseAnd();
                    if (!IsSkipping)
                        left = Convert.ToInt64(left) ^ Convert.ToInt64(right);
                }
                else break;
            }

            return left;
        }

        private object ParseBitwiseAnd()
        {
            var left = ParseEquality();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '&' && PeekChar(1) != '&')
                {
                    _position++;
                    var right = ParseEquality();
                    if (!IsSkipping)
                        left = Convert.ToInt64(left) & Convert.ToInt64(right);
                }
                else break;
            }

            return left;
        }

        private object ParseEquality()
        {
            var left = ParseRelational();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '=' && PeekChar(1) == '=')
                {
                    _position += 2;
                    var right = ParseRelational();
                    if (!IsSkipping)
                        left = Equals(left, right);
                }
                else if (PeekChar() == '!' && PeekChar(1) == '=')
                {
                    _position += 2;
                    var right = ParseRelational();
                    if (!IsSkipping)
                        left = !Equals(left, right);
                }
                else break;
            }

            return left;
        }

        private object ParseRelational()
        {
            var left = ParseShift();

            while (true)
            {
                SkipWhitespace();
                string op = PeekRelationalOp();
                if (op == null) break;
                _position += op.Length;

                var right = ParseShift();
                if (!IsSkipping)
                    left = Compare(left, right, op);
            }

            return left;
        }

        private string PeekRelationalOp()
        {
            char c = PeekChar();
            char c2 = PeekChar(1);
            if (c == '<' && c2 == '=') return "<=";
            if (c == '>' && c2 == '=') return ">=";
            if (c == '<') return "<";
            if (c == '>') return ">";
            return null;
        }

        private object Compare(object left, object right, string op)
        {
            int cmp;
            if (left is string ls && right is string rs)
            {
                cmp = string.Compare(ls, rs, StringComparison.Ordinal);
            }
            else if (left is decimal || right is decimal)
            {
                cmp = decimal.Compare(Convert.ToDecimal(left), Convert.ToDecimal(right));
            }
            else if (IsInteger(left) && IsInteger(right))
            {
                cmp = Convert.ToInt64(left).CompareTo(Convert.ToInt64(right));
            }
            else
            {
                cmp = Convert.ToDouble(left).CompareTo(Convert.ToDouble(right));
            }

            switch (op)
            {
                case "<=": return cmp <= 0;
                case ">=": return cmp >= 0;
                case "<": return cmp < 0;
                case ">": return cmp > 0;
                default: throw new FormatException($"Unknown operator: {op}");
            }
        }

        private object ParseShift()
        {
            var left = ParseAdditive();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '<' && PeekChar(1) == '<')
                {
                    _position += 2;
                    var right = ParseAdditive();
                    if (!IsSkipping)
                        left = Convert.ToInt64(left) << (int)Convert.ToInt64(right);
                }
                else if (PeekChar() == '>' && PeekChar(1) == '>')
                {
                    _position += 2;
                    var right = ParseAdditive();
                    if (!IsSkipping)
                        left = Convert.ToInt64(left) >> (int)Convert.ToInt64(right);
                }
                else break;
            }

            return left;
        }

        private object ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '+' && PeekChar(1) != '+')
                {
                    _position++;
                    var right = ParseMultiplicative();
                    if (!IsSkipping)
                        left = Add(left, right);
                }
                else if (PeekChar() == '-' && PeekChar(1) != '-')
                {
                    _position++;
                    var right = ParseMultiplicative();
                    if (!IsSkipping)
                        left = Subtract(left, right);
                }
                else break;
            }

            return left;
        }

        private object ParseMultiplicative()
        {
            var left = ParseExponential();

            while (true)
            {
                SkipWhitespace();
                if (PeekChar() == '*' && PeekChar(1) != '*')
                {
                    _position++;
                    var right = ParseExponential();
                    if (!IsSkipping)
                        left = Multiply(left, right);
                }
                else if (PeekChar() == '/')
                {
                    _position++;
                    var right = ParseExponential();
                    if (!IsSkipping)
                        left = Divide(left, right);
                }
                else if (PeekChar() == '%')
                {
                    _position++;
                    var right = ParseExponential();
                    if (!IsSkipping)
                        left = Modulo(left, right);
                }
                else break;
            }

            return left;
        }

        private object ParseExponential()
        {
            var left = ParseUnary();

            SkipWhitespace();
            if (PeekChar() == '*' && PeekChar(1) == '*')
            {
                _position += 2;
                var right = ParseExponential();
                if (!IsSkipping)
                    left = Math.Pow(Convert.ToDouble(left), Convert.ToDouble(right));
            }

            return left;
        }

        private object ParseUnary()
        {
            SkipWhitespace();

            if (PeekChar() == '+' && PeekChar(1) == '+')
            {
                _position += 2;
                return PreIncrement();
            }
            if (PeekChar() == '-' && PeekChar(1) == '-')
            {
                _position += 2;
                return PreDecrement();
            }

            if (PeekChar() == '+')
            {
                _position++;
                return ParseUnary();
            }
            if (PeekChar() == '-')
            {
                _position++;
                var v = ParseUnary();
                return IsSkipping ? null : Negate(v);
            }
            if (PeekChar() == '!')
            {
                _position++;
                var v = ParseUnary();
                return IsSkipping ? null : Not(v);
            }
            if (PeekChar() == '~')
            {
                _position++;
                var v = ParseUnary();
                return IsSkipping ? null : BitwiseNot(v);
            }

            int savedPos = _position;
            string peekedName = TryPeekIdentifier();

            if (peekedName != null && !IsKeyword(peekedName))
            {
                SkipWhitespace();
                if (PeekChar() == '+' && PeekChar(1) == '+')
                {
                    _position += 2;
                    return PostIncrement(peekedName);
                }
                if (PeekChar() == '-' && PeekChar(1) == '-')
                {
                    _position += 2;
                    return PostDecrement(peekedName);
                }
            }

            _position = savedPos;

            var result = ParsePrimary();

            SkipWhitespace();
            if (PeekChar() == '+' && PeekChar(1) == '+')
                throw new FormatException("Postfix ++ is only supported on simple identifiers");
            if (PeekChar() == '-' && PeekChar(1) == '-')
                throw new FormatException("Postfix -- is only supported on simple identifiers");

            return result;
        }

        private object ParsePrimary()
        {
            SkipWhitespace();

            if (_position >= _expression.Length)
                throw new FormatException("Unexpected end of expression");

            char c = _expression[_position];

            if (char.IsDigit(c) || (c == '.' && _position + 1 < _expression.Length && char.IsDigit(_expression[_position + 1])))
                return ParseNumber();

            if (c == '"' || c == '\'')
                return ParseString();

            if (c == '[')
                return ParseArray();

            if (c == '(')
            {
                _position++;
                var result = ParseExpression();
                Expect(")");
                return result;
            }

            if (MatchWord("true")) return true;
            if (MatchWord("false")) return false;
            if (MatchWord("null")) return null;

            if (char.IsLetter(c) || c == '_')
                return ParseIdentifier();

            throw new FormatException($"Unexpected character at position {_position}: '{c}' in expression: '{_expression}'");
        }

        private object ParseNumber()
        {

            if (_position + 1 < _expression.Length && _expression[_position] == '0' &&
                (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X'))
            {
                _position += 2;
                return ParseHexNumber();
            }

            if (_position + 1 < _expression.Length && _expression[_position] == '0' &&
                (_expression[_position + 1] == 'b' || _expression[_position + 1] == 'B'))
            {
                _position += 2;
                return ParseBinaryNumber();
            }

            int start = _position;
            bool hasDecimal = false;
            bool hasExponent = false;

            while (_position < _expression.Length)
            {
                char ch = _expression[_position];

                if (char.IsDigit(ch))
                {
                    _position++;
                }
                else if (ch == '.' && !hasDecimal && !hasExponent)
                {
                    hasDecimal = true;
                    _position++;
                }
                else if ((ch == 'e' || ch == 'E') && !hasExponent)
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
                if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblResult))
                    return dblResult;
            }
            else
            {
                if (int.TryParse(numberStr, out int intResult))
                    return intResult;

                if (long.TryParse(numberStr, out long longResult))
                    return longResult;

                if (decimal.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decResult))
                    return decResult;

                if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblResult))
                    return dblResult;
            }

            throw new FormatException($"Invalid number format: '{numberStr}'");
        }

        private object ParseHexNumber()
        {
            int start = _position;
            while (_position < _expression.Length && IsHexDigit(_expression[_position]))
            {
                _position++;
            }

            string hex = _expression.Substring(start, _position - start);
            if (string.IsNullOrEmpty(hex))
                throw new FormatException("Invalid hex number: no digits after '0x'");

            if (long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long result))
                return result;

            throw new FormatException($"Invalid hex number: '0x{hex}'");
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private object ParseBinaryNumber()
        {
            int start = _position;
            while (_position < _expression.Length && (_expression[_position] == '0' || _expression[_position] == '1'))
            {
                _position++;
            }

            string bin = _expression.Substring(start, _position - start);
            if (string.IsNullOrEmpty(bin))
                throw new FormatException("Invalid binary number: no digits after '0b'");

            long result = 0;
            foreach (char c in bin)
            {
                result = result * 2 + (c - '0');
            }
            return result;
        }

        private string ParseString()
        {
            char quoteChar = _expression[_position];
            _position++;

            StringBuilder sb = new StringBuilder();
            bool escape = false;
            bool terminated = false;

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
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        case 'u':
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
                    terminated = true;
                    break;
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (!terminated)
                throw new FormatException("Unterminated string literal");

            return sb.ToString();
        }

        private object ParseArray()
        {
            Expect("[");
            var elements = new List<object>();

            SkipWhitespace();
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
            return ParsePostfix(identifier);
        }

        private object ParsePostfix(string identifier)
        {
            SkipWhitespace();

            object result;

            if (_position < _expression.Length && _expression[_position] == '(')
            {
                result = ParseFunctionCall(identifier);
            }
            else
            {
                result = IsSkipping ? null : GetVariableValue(identifier);
            }

            while (true)
            {
                SkipWhitespace();
                if (!Match("."))
                    break;

                result = ParseMemberAccess(result);
            }

            return result;
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

            if (string.Equals(functionName, "iif", StringComparison.OrdinalIgnoreCase))
            {
                return ParseIif();
            }
            if (string.Equals(functionName, "coalesce", StringComparison.OrdinalIgnoreCase))
            {
                return ParseCoalesce();
            }

            var parameters = new List<object>();

            SkipWhitespace();
            if (!Match(")"))
            {
                do
                {
                    parameters.Add(ParseExpression());
                }
                while (Match(","));

                Expect(")");
            }

            if (IsSkipping)
                return null;

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

        private object ParseIif()
        {

            object condition = ParseExpression();
            Expect(",");

            if (IsSkipping)
            {
                ParseExpression();
                Expect(",");
                ParseExpression();
                Expect(")");
                return null;
            }

            bool cond = Convert.ToBoolean(condition);
            if (cond)
            {
                var trueValue = ParseExpression();
                Expect(",");
                BeginSkip();
                try { ParseExpression(); }
                finally { EndSkip(); }
                Expect(")");
                return trueValue;
            }
            else
            {
                BeginSkip();
                try { ParseExpression(); }
                finally { EndSkip(); }
                Expect(",");
                var falseValue = ParseExpression();
                Expect(")");
                return falseValue;
            }
        }

        private object ParseCoalesce()
        {

            object result = null;
            bool found = false;

            while (true)
            {
                object value;
                if (found && !IsSkipping)
                {
                    BeginSkip();
                    try { value = ParseExpression(); }
                    finally { EndSkip(); }
                }
                else
                {
                    value = ParseExpression();
                    if (!IsSkipping && !found && value != null)
                    {
                        result = value;
                        found = true;
                    }
                }

                SkipWhitespace();
                if (!Match(","))
                    break;
            }

            Expect(")");
            return result;
        }

        private object ParseMemberAccess(object target)
        {
            string memberName = ParseIdentifierName();
            SkipWhitespace();

            if (_position < _expression.Length && _expression[_position] == '(')
            {
                return ParseMethodCall(target, memberName);
            }

            if (IsSkipping)
                return null;

            return GetMemberValue(target, memberName);
        }

        private object ParseMethodCall(object target, string methodName)
        {
            Expect("(");
            var parameters = new List<object>();

            SkipWhitespace();
            if (!Match(")"))
            {
                do
                {
                    parameters.Add(ParseExpression());
                }
                while (Match(","));

                Expect(")");
            }

            if (IsSkipping)
                return null;

            if (target == null)
                throw new FormatException($"Cannot call method '{methodName}' on null target");

            ScriptEngineSecurity.CheckReflectionAllowed(target.GetType());

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
            catch (Exception ex) when (!(ex is FormatException))
            {
                throw new FormatException($"Error calling method '{methodName}': {ex.Message}");
            }
        }

        private object GetMemberValue(object target, string memberName)
        {
            if (target == null)
                throw new FormatException($"Cannot access member '{memberName}' on null target");

            ScriptEngineSecurity.CheckReflectionAllowed(target.GetType());

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
            catch (Exception ex) when (!(ex is FormatException))
            {
                throw new FormatException($"Error accessing member '{memberName}': {ex.Message}");
            }
        }

        private object GetVariableValue(string variableName)
        {

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

        private object Add(object left, object right)
        {
            if (left is string || right is string)
                return $"{left}{right}";

            if (IsDouble(left) || IsDouble(right))
                return Convert.ToDouble(left) + Convert.ToDouble(right);

            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left) + Convert.ToDecimal(right);

            if (IsInteger(left) && IsInteger(right))
                return Convert.ToInt64(left) + Convert.ToInt64(right);

            return Convert.ToDouble(left) + Convert.ToDouble(right);
        }

        private object Subtract(object left, object right)
        {
            if (IsDouble(left) || IsDouble(right))
                return Convert.ToDouble(left) - Convert.ToDouble(right);

            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left) - Convert.ToDecimal(right);

            if (IsInteger(left) && IsInteger(right))
                return Convert.ToInt64(left) - Convert.ToInt64(right);

            return Convert.ToDouble(left) - Convert.ToDouble(right);
        }

        private object Multiply(object left, object right)
        {
            if (IsDouble(left) || IsDouble(right))
                return Convert.ToDouble(left) * Convert.ToDouble(right);

            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left) * Convert.ToDecimal(right);

            if (IsInteger(left) && IsInteger(right))
                return Convert.ToInt64(left) * Convert.ToInt64(right);

            return Convert.ToDouble(left) * Convert.ToDouble(right);
        }

        private object Divide(object left, object right)
        {
            if (IsDouble(left) || IsDouble(right))
            {
                double r = Convert.ToDouble(right);
                if (r == 0) throw new DivideByZeroException("Division by zero");
                return Convert.ToDouble(left) / r;
            }

            if (left is decimal || right is decimal)
            {
                decimal r = Convert.ToDecimal(right);
                if (r == 0) throw new DivideByZeroException("Division by zero");
                return Convert.ToDecimal(left) / r;
            }

            if (IsInteger(left) && IsInteger(right))
            {
                long l = Convert.ToInt64(left);
                long r = Convert.ToInt64(right);
                if (r == 0) throw new DivideByZeroException("Division by zero");
                if (l % r == 0)
                    return l / r;
                return (double)l / (double)r;
            }

            double dr = Convert.ToDouble(right);
            if (dr == 0) throw new DivideByZeroException("Division by zero");
            return Convert.ToDouble(left) / dr;
        }

        private object Modulo(object left, object right)
        {
            if (IsDouble(left) || IsDouble(right))
            {
                double r = Convert.ToDouble(right);
                if (r == 0) throw new DivideByZeroException("Division by zero in modulo operation");
                return Convert.ToDouble(left) % r;
            }

            if (left is decimal || right is decimal)
            {
                decimal r = Convert.ToDecimal(right);
                if (r == 0) throw new DivideByZeroException("Division by zero in modulo operation");
                return Convert.ToDecimal(left) % r;
            }

            if (IsInteger(left) && IsInteger(right))
            {
                long r = Convert.ToInt64(right);
                if (r == 0) throw new DivideByZeroException("Division by zero in modulo operation");
                return Convert.ToInt64(left) % r;
            }

            double dr2 = Convert.ToDouble(right);
            if (dr2 == 0) throw new DivideByZeroException("Division by zero in modulo operation");
            return Convert.ToDouble(left) % dr2;
        }

        private object Negate(object value)
        {
            if (value is decimal d) return -d;
            if (IsInteger(value)) return -Convert.ToInt64(value);
            return -Convert.ToDouble(value);
        }

        private object Not(object value)
        {
            return !Convert.ToBoolean(value);
        }

        private object BitwiseNot(object value)
        {
            return ~Convert.ToInt64(value);
        }

        private static bool IsInteger(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong;
        }

        private static bool IsDouble(object value)
        {
            return value is float || value is double;
        }

        private object PreIncrement()
        {
            string varName = ParseIdentifierName();
            if (string.IsNullOrEmpty(varName))
                throw new FormatException("Expected variable name after '++'");

            if (IsSkipping) return null;

            object value = GetVariableValue(varName);
            object newValue = IncrementValue(value, 1);
            SetVariableValue(varName, newValue);
            return newValue;
        }

        private object PreDecrement()
        {
            string varName = ParseIdentifierName();
            if (string.IsNullOrEmpty(varName))
                throw new FormatException("Expected variable name after '--'");

            if (IsSkipping) return null;

            object value = GetVariableValue(varName);
            object newValue = IncrementValue(value, -1);
            SetVariableValue(varName, newValue);
            return newValue;
        }

        private object PostIncrement(string varName)
        {
            if (IsSkipping) return null;

            object oldValue = GetVariableValue(varName);
            object newValue = IncrementValue(oldValue, 1);
            SetVariableValue(varName, newValue);
            return oldValue;
        }

        private object PostDecrement(string varName)
        {
            if (IsSkipping) return null;

            object oldValue = GetVariableValue(varName);
            object newValue = IncrementValue(oldValue, -1);
            SetVariableValue(varName, newValue);
            return oldValue;
        }

        private static object IncrementValue(object value, int delta)
        {
            if (value is decimal d) return d + delta;
            if (value is double dbl) return dbl + delta;
            if (value is float f) return f + delta;
            if (value is long l) return l + delta;
            if (value is int i) return i + delta;
            return Convert.ToDouble(value) + delta;
        }

        private void SetVariableValue(string varName, object value)
        {
            _localVariables[varName] = value;
            _setVariableCallback?.Invoke(varName, value);
        }

        private void SkipWhitespace()
        {
            while (_position < _expression.Length)
            {
                char c = _expression[_position];

                if (char.IsWhiteSpace(c))
                {
                    _position++;
                }
                else if (c == '/' && _position + 1 < _expression.Length && _expression[_position + 1] == '/')
                {

                    _position += 2;
                    while (_position < _expression.Length && _expression[_position] != '\n')
                        _position++;
                }
                else if (c == '/' && _position + 1 < _expression.Length && _expression[_position + 1] == '*')
                {

                    _position += 2;
                    bool closed = false;
                    while (_position + 1 < _expression.Length)
                    {
                        if (_expression[_position] == '*' && _expression[_position + 1] == '/')
                        {
                            _position += 2;
                            closed = true;
                            break;
                        }
                        _position++;
                    }
                    if (!closed)
                        throw new FormatException("Unterminated block comment");
                }
                else
                {
                    break;
                }
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

        private bool MatchWord(string expected)
        {
            SkipWhitespace();

            if (_position + expected.Length > _expression.Length)
                return false;

            string actual = _expression.Substring(_position, expected.Length);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return false;

            int nextPos = _position + expected.Length;
            if (nextPos < _expression.Length)
            {
                char next = _expression[nextPos];
                if (char.IsLetterOrDigit(next) || next == '_')
                    return false;
            }

            _position = nextPos;
            return true;
        }

        private void Expect(string expected)
        {
            if (!Match(expected))
                throw new FormatException($"Expected '{expected}' at position {_position} in expression: '{_expression}'");
        }

        private char PeekChar(int offset = 0)
        {
            int pos = _position + offset;
            return pos < _expression.Length ? _expression[pos] : '\0';
        }

        private string TryPeekIdentifier()
        {
            SkipWhitespace();

            if (_position >= _expression.Length)
                return null;

            if (!char.IsLetter(_expression[_position]) && _expression[_position] != '_')
                return null;

            int start = _position;
            _position++;

            while (_position < _expression.Length &&
                  (char.IsLetterOrDigit(_expression[_position]) || _expression[_position] == '_'))
            {
                _position++;
            }

            return _expression.Substring(start, _position - start);
        }

        private static bool IsKeyword(string name)
        {
            return string.Equals(name, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "false", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "null", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "var", StringComparison.OrdinalIgnoreCase);
        }
    }
}
