## Thread-Safe Script Runner

*This is a Thread-Safe Script Runner For Excecute All Type Of Script By Writing C# Code Only*

## Create new Instance Of Engine

```
using ScriptEngine;

var engine = ScriptEngineFactory.Create();
```
## Add Custome Function

```
  engine.AddCustomFunction("iff", parameters =>
  {
      if (parameters.Length != 3)
      {
          RaiseError(new Exception("iff have more than three parameters"));
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
```
var result = engine.Run("iff(1=1,abs(-90),100)");
```
