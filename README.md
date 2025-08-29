# ScriptEngine – README

`ScriptEngine` is a **thread-safe, high-performance C# expression evaluator**
It allows you to **inject global or thread-local custom functions**, evaluate arbitrary mathematical / logical expressions at run-time, and receive rich error feedback – all without ever touching the original source code.

---

## Create new Instance of Engine

```
using ScriptEngine;

var engine = ScriptEngineFactory.Create();// this instance use Ncalc package for calculation
var engine = ScriptEngineFactory.CreateLocalScriptEngine();// this instance use fast local parser for calculation
```
## Add Custome Function

```
  engine.AddCustomFunction("iff", parameters =>
  {
      if (parameters.Length != 3)
      {
          throw new Exception("iff have more than three parameters");
      }
      bool condition = Convert.ToBoolean(parameters[0]);
      return condition ? parameters[1] : parameters[2];
  });
  engine.AddCustomFunction("Abs", parameters =>
  {
      var arg = parameters[0];
      if (arg.GetType() == typeof(int))
      {
          return Math.Abs(Convert.ToInt64(arg));
      }
      if (arg.GetType() == typeof(decimal))
      {
          return Math.Abs(Convert.ToDecimal(arg));
      }
      if (arg.GetType() == typeof(double))
      {
          return Math.Abs(Convert.ToDouble(arg));
      }
      return 0;
  });
```
## Run Scripts
````
var result = engine.Run("iff(1=1,abs(-90),100+1*8)");
```
