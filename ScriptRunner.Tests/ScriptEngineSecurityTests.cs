using System;
using System.Collections.Generic;
using ScriptEngine;
using Xunit;

namespace ScriptEngine.Tests
{
    public class ScriptEngineSecurityTests
    {
        [Fact]
        public void ReflectionEnabled_DefaultTrue()
        {
            Assert.True(ScriptEngineSecurity.ReflectionEnabled);
        }

        [Fact]
        public void AllowedReflectionTypeNames_DefaultNull()
        {
            Assert.Null(ScriptEngineSecurity.AllowedReflectionTypeNames);
        }

        [Fact]
        public void ReflectionEnabled_CanBeDisabled()
        {
            var original = ScriptEngineSecurity.ReflectionEnabled;
            try
            {
                ScriptEngineSecurity.ReflectionEnabled = false;
                Assert.False(ScriptEngineSecurity.ReflectionEnabled);
            }
            finally
            {
                ScriptEngineSecurity.ReflectionEnabled = original;
            }
        }

        [Fact]
        public void ReflectionDisabled_BlocksMemberAccess()
        {
            var originalEnabled = ScriptEngineSecurity.ReflectionEnabled;
            try
            {
                ScriptEngineSecurity.ReflectionEnabled = false;

                var engine = ScriptEngineFactory.CreateLocalScriptEngine();
                engine.SetGlobalVariable("dt", new DateTime(2023, 6, 15));

                Assert.ThrowsAny<Exception>(() => engine.Run("dt.Year"));
            }
            finally
            {
                ScriptEngineSecurity.ReflectionEnabled = originalEnabled;
            }
        }

        [Fact]
        public void ReflectionEnabled_AllowsMemberAccess()
        {
            var originalEnabled = ScriptEngineSecurity.ReflectionEnabled;
            var originalAllowed = ScriptEngineSecurity.AllowedReflectionTypeNames;
            try
            {
                ScriptEngineSecurity.ReflectionEnabled = true;
                ScriptEngineSecurity.AllowedReflectionTypeNames = null;

                var engine = ScriptEngineFactory.CreateLocalScriptEngine();
                engine.SetGlobalVariable("dt", new DateTime(2023, 6, 15));

                Assert.Equal(2023, engine.Run("dt.Year"));
            }
            finally
            {
                ScriptEngineSecurity.ReflectionEnabled = originalEnabled;
                ScriptEngineSecurity.AllowedReflectionTypeNames = originalAllowed;
            }
        }

        [Fact]
        public void AllowedReflectionTypeNames_WhitelistAllows()
        {
            var originalEnabled = ScriptEngineSecurity.ReflectionEnabled;
            var originalAllowed = ScriptEngineSecurity.AllowedReflectionTypeNames;
            try
            {
                ScriptEngineSecurity.ReflectionEnabled = true;
                ScriptEngineSecurity.AllowedReflectionTypeNames = new HashSet<string>
                {
                    "System.DateTime"
                };

                var engine = ScriptEngineFactory.CreateLocalScriptEngine();
                engine.SetGlobalVariable("dt", new DateTime(2023, 6, 15));

                Assert.Equal(2023, engine.Run("dt.Year"));
            }
            finally
            {
                ScriptEngineSecurity.ReflectionEnabled = originalEnabled;
                ScriptEngineSecurity.AllowedReflectionTypeNames = originalAllowed;
            }
        }

        [Fact]
        public void AllowedReflectionTypeNames_WhitelistBlocks()
        {
            var originalEnabled = ScriptEngineSecurity.ReflectionEnabled;
            var originalAllowed = ScriptEngineSecurity.AllowedReflectionTypeNames;
            try
            {
                ScriptEngineSecurity.ReflectionEnabled = true;
                ScriptEngineSecurity.AllowedReflectionTypeNames = new HashSet<string>
                {
                    "System.DateTime"
                };

                var engine = ScriptEngineFactory.CreateLocalScriptEngine();
                engine.SetGlobalVariable("s", "hello");

                Assert.ThrowsAny<Exception>(() => engine.Run("s.Length"));
            }
            finally
            {
                ScriptEngineSecurity.ReflectionEnabled = originalEnabled;
                ScriptEngineSecurity.AllowedReflectionTypeNames = originalAllowed;
            }
        }

        [Fact]
        public void AllowedReflectionTypeNames_CanBeCleared()
        {
            var originalAllowed = ScriptEngineSecurity.AllowedReflectionTypeNames;
            try
            {
                ScriptEngineSecurity.AllowedReflectionTypeNames = new HashSet<string> { "System.String" };
                ScriptEngineSecurity.AllowedReflectionTypeNames = null;
                Assert.Null(ScriptEngineSecurity.AllowedReflectionTypeNames);
            }
            finally
            {
                ScriptEngineSecurity.AllowedReflectionTypeNames = originalAllowed;
            }
        }
    }
}
