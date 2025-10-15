namespace ScriptEngine
{
    public static class ScriptEngineFactory
    {
        /// <summary>
        /// using Ncalc Package for calculation
        /// </summary>
        /// <returns></returns>
        public static IScriptExecutor Create()
        {
            return ScriptExecutor.Instance;
        }
        /// <summary>
        /// using local parser for calculation
        /// </summary>
        /// <returns></returns>
        public static IScriptExecutor CreateLocalScriptEngine()
        {
            return LocalScriptExecutor.Instance;
        }
    }
}
