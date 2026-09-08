using System.Diagnostics;
using UnityEngine;

// Console log double click參考資料
// https://www.reddit.com/r/Unity3D/comments/17eikh0/i_found_a_way_to_go_to_the_right_line_in_your/


namespace GenjitsuLAB.Core
{
    public class DebugLogger
    {
        private const string TAG_FORMAT = "[{0}] - {1}";

        public static bool enableLog = true;
        public static bool enableAssertion = true;
        public static bool enableWarning = true;
        public static bool enableError = true;
        public static bool enableException = true;

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void Log(object message)
        {
            if (!enableLog)
            {
                return;
            }

            UnityEngine.Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void Log(object message, Object context)
        {
            if (!enableLog)
            {
                return;
            }

            UnityEngine.Debug.Log(message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogTag(string tag, object message)
        {
            LogFormat(TAG_FORMAT, tag, message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogTag(string tag, object message, Object context)
        {
            LogFormat(context, TAG_FORMAT, tag, message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormat(string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            UnityEngine.Debug.LogFormat(format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormat(Object context, string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            UnityEngine.Debug.LogFormat(context, format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormat(LogType logType, LogOption logOption, Object context, string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            UnityEngine.Debug.LogFormat(logType, logOption, context, format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormatTag(string tag, string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            LogFormat(string.Format(TAG_FORMAT, tag, format), args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormatTag(string tag, Object context, string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            LogFormat(context, string.Format(TAG_FORMAT, tag, format), args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogFormatTag(string tag, LogType logType, LogOption logOption, Object context, string format, params object[] args)
        {
            if (!enableLog)
            {
                return;
            }

            LogFormat(logType, logOption, context, string.Format(TAG_FORMAT, tag, format), args);
        }

        public static void LogAssertion(object message)
        {
            if (!enableAssertion)
            {
                return;
            }

            UnityEngine.Debug.LogAssertion(message);
        }

        public static void LogAssertion(object message, Object context)
        {
            if (!enableAssertion)
            {
                return;
            }

            UnityEngine.Debug.LogAssertion(message, context);
        }

        public static void LogAssertionTag(string tag, object message)
        {
            LogAssertionFormat(TAG_FORMAT, tag, message);
        }

        public static void LogAssertionTag(string tag, object message, Object context)
        {
            LogAssertionFormat(context, TAG_FORMAT, tag, message);
        }

        public static void LogAssertionFormat(string format, params object[] args)
        {
            if (!enableAssertion)
            {
                return;
            }

            UnityEngine.Debug.LogAssertionFormat(format, args);
        }

        public static void LogAssertionFormat(Object context, string format, params object[] args)
        {
            if (!enableAssertion)
            {
                return;
            }

            UnityEngine.Debug.LogAssertionFormat(context, format, args);
        }

        public static void LogAssertionFormatTag(string tag, string format, params object[] args)
        {
            if (!enableAssertion)
            {
                return;
            }

            LogAssertionFormat(string.Format(TAG_FORMAT, tag, format), args);
        }

        public static void LogAssertionFormatTag(string tag, Object context, string format, params object[] args)
        {
            if (!enableAssertion)
            {
                return;
            }

            LogAssertionFormat(context, string.Format(TAG_FORMAT, tag, format), args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarning(object message)
        {
            if (!enableWarning)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarning(object message, Object context)
        {
            if (!enableWarning)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningTag(string tag, object message)
        {
            LogWarningFormat(TAG_FORMAT, tag, message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningTag(string tag, object message, Object context)
        {
            LogWarningFormat(context, TAG_FORMAT, tag, message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningFormat(string format, params object[] args)
        {
            if (!enableWarning)
            {
                return;
            }

            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningFormat(Object context, string format, params object[] args)
        {
            if (!enableWarning)
            {
                return;
            }

            UnityEngine.Debug.LogWarningFormat(context, format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningFormatTag(string tag, string format, params object[] args)
        {
            if (!enableWarning)
            {
                return;
            }

            LogWarningFormat(string.Format(TAG_FORMAT, tag, format), args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("ENABLE_LOGS")]
        public static void LogWarningFormatTag(string tag, Object context, string format, params object[] args)
        {
            if (!enableWarning)
            {
                return;
            }

            LogWarningFormat(context, string.Format(TAG_FORMAT, tag, format), args);
        }

        public static void LogError(object message)
        {
            if (!enableError)
            {
                return;
            }

            UnityEngine.Debug.LogError(message);
        }

        public static void LogError(object message, Object context)
        {
            if (!enableError)
            {
                return;
            }

            UnityEngine.Debug.LogError(message, context);
        }

        public static void LogErrorTag(string tag, object message)
        {
            LogErrorFormat(TAG_FORMAT, tag, message);
        }

        public static void LogErrorTag(string tag, object message, Object context)
        {
            LogErrorFormat(context, TAG_FORMAT, tag, message);
        }

        public static void LogErrorFormat(string format, params object[] args)
        {
            if (!enableError)
            {
                return;
            }

            UnityEngine.Debug.LogErrorFormat(format, args);
        }

        public static void LogErrorFormat(Object context, string format, params object[] args)
        {
            if (!enableError)
            {
                return;
            }

            UnityEngine.Debug.LogErrorFormat(context, format, args);
        }

        public static void LogErrorFormatTag(string tag, string format, params object[] args)
        {
            if (!enableError)
            {
                return;
            }

            LogErrorFormat(string.Format(TAG_FORMAT, tag, format), args);
        }

        public static void LogErrorFormatTag(string tag, Object context, string format, params object[] args)
        {
            if (!enableError)
            {
                return;
            }

            LogErrorFormat(context, string.Format(TAG_FORMAT, tag, format), args);
        }

        public static void LogException(System.Exception exception)
        {
            if (!enableException)
            {
                return;
            }

            UnityEngine.Debug.LogException(exception);
        }

        public static void LogException(System.Exception exception, Object context)
        {
            if (!enableException)
            {
                return;
            }

            UnityEngine.Debug.LogException(exception, context);
        }
    }

}