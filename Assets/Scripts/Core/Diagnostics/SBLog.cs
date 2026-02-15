using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenBattles.Core.Diagnostics
{
    /// <summary>
    /// Editor-only logging facade.
    /// Methods are conditionally compiled out in non-Editor builds,
    /// including call-site argument evaluation (e.g., string interpolation).
    /// </summary>
    public static class SBLog
    {
        private const string PREFIX = "[SB]";

        // Set to true if you want the caller type name appended automatically in Editor logs.
        private const bool INCLUDE_CALLER_TYPE_NAME = false;

        [Conditional("UNITY_EDITOR")]
        public static void Info(string message, Object context = null)
        {
            UnityEngine.Debug.Log(FormatMessage(message), context);
        }

        [Conditional("UNITY_EDITOR")]
        public static void Warn(string message, Object context = null)
        {
            UnityEngine.Debug.LogWarning(FormatMessage(message), context);
        }

        [Conditional("UNITY_EDITOR")]
        public static void Error(string message, Object context = null)
        {
            UnityEngine.Debug.LogError(FormatMessage(message), context);
        }

        private static string FormatMessage(string message)
        {
            if (!INCLUDE_CALLER_TYPE_NAME)
            {
                return string.IsNullOrEmpty(PREFIX) ? message : $"{PREFIX} {message}";
            }

            var frame = new StackFrame(2, false);
            var callerTypeName = frame.GetMethod()?.DeclaringType?.Name;

            if (string.IsNullOrEmpty(callerTypeName))
            {
                return string.IsNullOrEmpty(PREFIX) ? message : $"{PREFIX} {message}";
            }

            if (string.IsNullOrEmpty(PREFIX))
            {
                return $"[{callerTypeName}] {message}";
            }

            return $"{PREFIX}[{callerTypeName}] {message}";
        }
    }
}
