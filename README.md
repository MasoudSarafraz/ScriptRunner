# ScriptEngine

`ScriptEngine` is a **thread-safe, high-performance C# expression evaluator**
It allows you to **inject global or thread-local custom functions**, evaluate arbitrary mathematical / logical expressions at run-time, and receive rich error feedback – all without ever touching the original source code.

---

## Create new Instance of Engine

```
using ScriptEngine;

var engine = ScriptEngineFactory.Create();// this instance use Ncalc package for calculation
var engine = ScriptEngineFactory.CreateLocalScriptEngine();// this instance use fast local parser for calculation
```

## Add Custom Function

```
  engine.AddCustomFunction("iif", parameters =>
  {
      if (parameters.Length != 3)
      {
          throw new Exception("iif requires exactly three parameters");
      }
      bool condition = Convert.ToBoolean(parameters[0]);
      return condition ? parameters[1] : parameters[2];
  });
  engine.AddCustomFunction("abs", parameters =>
  {
      var arg = parameters[0];
      if (arg is int)
      {
          return Math.Abs(Convert.ToInt64(arg));
      }
      if (arg is decimal)
      {
          return Math.Abs(Convert.ToDecimal(arg));
      }
      if (arg is double)
      {
          return Math.Abs(Convert.ToDouble(arg));
      }
      return 0;
  });
```

## Run Scripts

```
var result = engine.Run("iif(1==1, abs(-90), 100+1*8)");
```

## Variables

The local script engine supports variables that persist across `Run` calls:

```
var engine = ScriptEngineFactory.CreateLocalScriptEngine();

// Set global variable (shared across all threads)
engine.SetGlobalVariable("x", 10);

// Set thread-local variable (isolated per thread)
engine.SetThreadLocalVariable("y", 20);

// var declaration persists to thread-local storage
engine.Run("var z = 30");
// Assignment to existing variable updates it
engine.Run("x = 50");

var result = engine.Run("x + y + z"); // 100
```

## Built-in Functions (Local Script Engine)

The local script engine includes these built-in functions:

| Category | Functions |
|---|---|
| Math | `sqrt`, `pow`, `abs`, `round`, `ceil`, `floor`, `exp`, `log`, `log10` |
| Trigonometric | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2` |
| String | `length`, `substring`, `toupper`, `tolower`, `trim`, `concat` |
| Date/Time | `now`, `today`, `year`, `month`, `day` |
| Conditional | `iif` (short-circuit), `coalesce` (short-circuit), `isnull`, `isnumber`, `isstring` |
| Array | `array`, `count`, `sum`, `avg`, `min`, `max` |

## Built-in Constants (Local Script Engine)

| Constant | Value |
|---|---|
| `PI` | Math.PI |
| `E` | Math.E |
| `TRUE` | true |
| `FALSE` | false |
| `NULL` | null |

## Security Configuration

Member access via reflection (e.g., `obj.Property`, `obj.Method()`) can be restricted using `ScriptEngineSecurity`:

```
using ScriptEngine;

// Disable all reflection-based member access
ScriptEngineSecurity.ReflectionEnabled = false;

// Or allow only specific types
ScriptEngineSecurity.AllowedReflectionTypeNames = new HashSet<string>
{
    "System.DateTime",
    "System.String"
};
```

## Error Handling

Subscribe to the `OnError` event to handle errors, or set a global handler:

```
engine.OnError += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Exception.Message}");
};

// Or set a global handler (used when no OnError subscriber exists)
LocalScriptExecutor.GlobalUnhandledErrorHandler = ex =>
{
    logger.LogError(ex, "Script execution error");
};
```

## Features

- Thread-safe execution with global and thread-local function isolation
- Short-circuit evaluation for `&&`, `||`, `?:`, `??`, `iif()`, `coalesce()`
- Multiple statements separated by `;`
- Line (`//`) and block (`/* */`) comments
- Hex (`0x1A`) and binary (`0b1010`) number literals
- Variable declarations with `var` that persist across `Run` calls
- Pre/post increment (`++x`, `x++`) and decrement (`--x`, `x--`)
- Member access chaining (`a.b.c`, `now().Year.ToString()`)
- String comparison with `<`, `>`, `<=`, `>=`
- Type-preserving arithmetic (int, long, decimal, double)
- Right-associative exponentiation (`2 ** 3 ** 2` = 512)
