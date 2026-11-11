using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Cpeakdot.Logging
{
    public static class Konsole
    {
        // Device logs (logcat, Xcode) print rich-text tags as raw text.
#if UNITY_EDITOR
        private const string ERROR_PREFIX = "<color=#FF4444>[ERROR]</color> ";
#else
        private const string ERROR_PREFIX = "[ERROR] ";
#endif

        public const string UNITY_CHANNEL = "unity";

        private const int MAX_FRAME_COUNT = 16;

        private static int m_MainThreadId;

        [ThreadStatic] private static bool m_IsOwnWarning;

        public static event Action<KonsoleEntry> OnEntryLogged;

        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void Info(string message, Object context = null)
        {
            Info(null, message, context);
        }

        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void Info(string channel, string message, Object context = null)
        {
            Debug.Log(GetConsoleText(channel, message), context);
            ReportEntry(KonsoleSeverity.Info, channel, message, context);
        }

        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void Warning(string message, Object context = null)
        {
            Warning(null, message, context);
        }

        [Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        public static void Warning(string channel, string message, Object context = null)
        {
            // Unity calls the log callback inside Debug.LogWarning, so the flag prevents a second row for this warning.
            m_IsOwnWarning = true;
            Debug.LogWarning(GetConsoleText(channel, message), context);
            m_IsOwnWarning = false;
            ReportEntry(KonsoleSeverity.Warning, channel, message, context);
        }

        [HideInCallstack]
        public static void Error(string message, Object context = null)
        {
            Error(null, message, context);
        }

        [HideInCallstack]
        public static void Error(string channel, string message, Object context = null)
        {
            Debug.Log(ERROR_PREFIX + GetConsoleText(channel, message), context);
            ReportEntry(KonsoleSeverity.Error, channel, message, context);
        }

        private static void Application_OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (m_IsOwnWarning || type == LogType.Log)
            {
                return;
            }

            Action<KonsoleEntry> handler = OnEntryLogged;
            if (handler == null)
            {
                return;
            }

            KonsoleSeverity severity = type == LogType.Warning ? KonsoleSeverity.Warning : KonsoleSeverity.Error;
            string trace = string.IsNullOrEmpty(stackTrace) ? string.Empty : stackTrace.TrimEnd('\n');
            handler.Invoke(new KonsoleEntry(severity, UNITY_CHANNEL, condition, trace, GetFrameOnMainThread(), KonsoleEntry.NO_CONTEXT));
        }

        private static string GetConsoleText(string channel, string message)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return message;
            }

            return "[" + channel + "] " + message;
        }

        [Conditional("UNITY_EDITOR")]
        private static void ReportEntry(KonsoleSeverity severity, string channel, string message, Object context)
        {
            Action<KonsoleEntry> handler = OnEntryLogged;
            if (handler == null)
            {
                return;
            }

            handler.Invoke(new KonsoleEntry(severity, channel, message, GetCallerStackTrace(), GetFrameOnMainThread(), GetContextId(context)));
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PrepareMainThreadId()
        {
            m_MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void PrepareUnityLogCapture()
        {
            Application.logMessageReceivedThreaded += Application_OnLogMessageReceived;
        }
#endif

        private static int GetFrameOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
            {
                return KonsoleEntry.NO_FRAME;
            }

            return Time.frameCount;
        }

        private static int GetContextId(Object context)
        {
            if (ReferenceEquals(context, null))
            {
                return KonsoleEntry.NO_CONTEXT;
            }

            return context.GetInstanceID();
        }

        private static string GetCallerStackTrace()
        {
            StackTrace stackTrace = new StackTrace(true);
            StringBuilder builder = new StringBuilder();
            int frameCount = 0;

            for (int i = 0; i < stackTrace.FrameCount && frameCount < MAX_FRAME_COUNT; i++)
            {
                StackFrame frame = stackTrace.GetFrame(i);
                MethodBase method = frame.GetMethod();
                if (method == null || method.DeclaringType == typeof(Konsole))
                {
                    continue;
                }

                AppendFrame(builder, method, frame);
                frameCount++;
            }

            return builder.ToString();
        }

        private static void AppendFrame(StringBuilder builder, MethodBase method, StackFrame frame)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            if (method.DeclaringType != null)
            {
                builder.Append(method.DeclaringType.FullName).Append('.');
            }

            builder.Append(method.Name);

            string filePath = frame.GetFileName();
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            builder.Append(KonsoleEntry.FRAME_FILE_MARKER).Append(filePath).Append(':').Append(frame.GetFileLineNumber()).Append(')');
        }
    }
}
