using System;
using System.Collections.Generic;

namespace ScriptEngine
{

    public static class ScriptEngineSecurity
    {
        private static volatile bool _reflectionEnabled = true;
        private static volatile HashSet<string> _allowedReflectionTypeNames;

        public static bool ReflectionEnabled
        {
            get => _reflectionEnabled;
            set => _reflectionEnabled = value;
        }

        public static HashSet<string> AllowedReflectionTypeNames
        {
            get => _allowedReflectionTypeNames;
            set => _allowedReflectionTypeNames = value;
        }

        internal static void CheckReflectionAllowed(Type type)
        {
            if (type == null)
                return;

            if (!_reflectionEnabled)
                throw new FormatException($"Reflection is disabled. Cannot access members of type '{type.FullName}'.");

            var allowed = _allowedReflectionTypeNames;
            if (allowed != null && !allowed.Contains(type.FullName))
                throw new FormatException($"Reflection on type '{type.FullName}' is not allowed.");
        }
    }
}
