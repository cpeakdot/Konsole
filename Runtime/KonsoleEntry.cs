using System;
using UnityEngine;

namespace Cpeakdot.Logging
{
    [Serializable]
    public struct KonsoleEntry
    {
        public const string FRAME_FILE_MARKER = " (at ";
        public const int NO_FRAME = -1;
        public const int NO_CONTEXT = 0;

        private const string COMPILER_FRAME_NAME = "Compiler";

        [field: SerializeField] public KonsoleSeverity Severity { get; private set; }
        [field: SerializeField] public string Channel { get; private set; }
        [field: SerializeField] public string Message { get; private set; }
        [field: SerializeField] public string TimeText { get; private set; }

        [field: SerializeField] public string StackTrace { get; private set; }
        [field: SerializeField] public int ContextId { get; private set; }

        [field: SerializeField] public bool IsCompilerMessage { get; private set; }

        public KonsoleEntry(KonsoleSeverity severity, string channel, string message, string stackTrace, int frame, int contextId) : this()
        {
            if (channel == null)
            {
                channel = string.Empty;
            }

            if (message == null)
            {
                message = string.Empty;
            }

            Severity = severity;
            Channel = channel;
            Message = message;
            TimeText = CreateTimeText(frame);
            StackTrace = stackTrace;
            ContextId = contextId;
        }

        public static KonsoleEntry CreateCompilerMessage(KonsoleSeverity severity, string channel, string message, string filePath, int line)
        {
            string stackTrace = string.Empty;
            if (string.IsNullOrEmpty(filePath) == false)
            {
                stackTrace = COMPILER_FRAME_NAME + FRAME_FILE_MARKER + filePath + ":" + line + ")";
            }

            KonsoleEntry entry = new KonsoleEntry(severity, channel, message, stackTrace, NO_FRAME, NO_CONTEXT);
            entry.IsCompilerMessage = true;
            return entry;
        }

        public static bool TryParseFrame(string frameLine, out string filePath, out int line)
        {
            filePath = null;
            line = 0;

            int markerIndex = frameLine.LastIndexOf(FRAME_FILE_MARKER, StringComparison.Ordinal);
            int lineIndex = frameLine.LastIndexOf(':');
            if (markerIndex < 0 || lineIndex < markerIndex)
            {
                return false;
            }

            int pathIndex = markerIndex + FRAME_FILE_MARKER.Length;
            filePath = frameLine.Substring(pathIndex, lineIndex - pathIndex);
            return int.TryParse(frameLine.Substring(lineIndex + 1, frameLine.Length - lineIndex - 2), out line);
        }

        private static string CreateTimeText(int frame)
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            if (frame == NO_FRAME)
            {
                return time;
            }

            return time + " #" + frame;
        }
    }
}
