namespace ScriptEngine
{
    public static class ScriptEngineFactory
    {
        public static IScriptExecutor Create()
        {
            return ScriptExecutor.Instance;
        }
    }
}
