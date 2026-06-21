using System;

namespace ScriptEngine
{

    public static class ScriptEngineOptions
    {

        private static volatile Action<Exception> _globalUnhandledErrorHandler;

        public static Action<Exception> GlobalUnhandledErrorHandler
        {
            get => _globalUnhandledErrorHandler;
            set => _globalUnhandledErrorHandler = value;
        }
    }
}
