using System;
using System.Collections.Generic;

namespace ScriptEngine
{
    /// <summary>
    /// Security configuration for the script engine.
    /// Controls reflection-based member access (e.g., obj.Property, obj.Method())
    /// when evaluating expressions.
    /// 
    /// By default, reflection is enabled for backward compatibility.
    /// To restrict access, set <see cref="AllowedReflectionTypeNames"/> to a whitelist
    /// of fully-qualified type names, or set <see cref="ReflectionEnabled"/> to false
    /// to completely disable member access.
    /// </summary>
    public static class ScriptEngineSecurity
    {
        private static volatile bool _reflectionEnabled = true;
        private static volatile HashSet<string> _allowedReflectionTypeNames;

        /// <summary>
        /// Gets or sets whether reflection-based member access is allowed.
        /// Default is true for backward compatibility.
        /// Set to false to completely disable member access via reflection.
        /// </summary>
        public static bool ReflectionEnabled
        {
            get => _reflectionEnabled;
            set => _reflectionEnabled = value;
        }

        /// <summary>
        /// Gets or sets the set of fully-qualified type names that are allowed for reflection.
        /// If null (default), all types are allowed.
        /// If non-null, only types whose FullName is in this set are allowed.
        /// </summary>
        public static HashSet<string> AllowedReflectionTypeNames
        {
            get => _allowedReflectionTypeNames;
            set => _allowedReflectionTypeNames = value;
        }

        /// <summary>
        /// Checks whether reflection on the given type is allowed.
        /// Throws FormatException if access is denied.
        /// </summary>
        /// <param name="type">The type to check. If null, no check is performed.</param>
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
