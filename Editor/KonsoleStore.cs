using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Cpeakdot.Logging
{
    internal sealed class KonsoleStore : ScriptableSingleton<KonsoleStore>
    {
        public const int SEVERITY_COUNT = 3;

        private const int MAX_ENTRY_COUNT = 10000;
        private const int TRIM_ENTRY_COUNT = 1000;
        private const string ERROR_PAUSE_PREF_KEY = "Cpeakdot.Konsole.ErrorPause";
        private const string COMPILER_ERROR_MARKER = "): error ";
        private const string COMPILER_WARNING_MARKER = "): warning ";

        private static readonly ConcurrentQueue<KonsoleEntry> PENDING_ENTRIES = new ConcurrentQueue<KonsoleEntry>();

        [SerializeField] private List<KonsoleEntry> m_Entries = new List<KonsoleEntry>();
        public IReadOnlyList<KonsoleEntry> Entries => m_Entries;

        public bool IsErrorPauseEnabled { get; private set; }

        private readonly int[] m_SeverityCounts = new int[SEVERITY_COUNT];

        public event Action OnChanged;

        private void OnEnable()
        {
            IsErrorPauseEnabled = EditorPrefs.GetBool(ERROR_PAUSE_PREF_KEY, false);
            CountSeverities();

            Konsole.OnEntryLogged += Konsole_OnEntryLogged;
            EditorApplication.update += EditorApplication_OnUpdate;
            EditorApplication.playModeStateChanged += EditorApplication_OnPlayModeStateChanged;
            CompilationPipeline.compilationStarted += CompilationPipeline_OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += CompilationPipeline_OnAssemblyCompilationFinished;
        }

        private void OnDisable()
        {
            Konsole.OnEntryLogged -= Konsole_OnEntryLogged;
            EditorApplication.update -= EditorApplication_OnUpdate;
            EditorApplication.playModeStateChanged -= EditorApplication_OnPlayModeStateChanged;
            CompilationPipeline.compilationStarted -= CompilationPipeline_OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= CompilationPipeline_OnAssemblyCompilationFinished;

            // A script reload drops the static queue, so the waiting entries move to the serialized list first.
            AddPendingEntries(out _);
        }

        public void Clear()
        {
            m_Entries.Clear();
            Array.Clear(m_SeverityCounts, 0, m_SeverityCounts.Length);
            OnChanged?.Invoke();
        }

        public int GetSeverityCount(KonsoleSeverity severity)
        {
            return m_SeverityCounts[(int)severity];
        }

        public void SetErrorPause(bool isEnabled)
        {
            IsErrorPauseEnabled = isEnabled;
            EditorPrefs.SetBool(ERROR_PAUSE_PREF_KEY, isEnabled);
        }

        // A log can come from a background thread, so the entry waits here until the editor update.
        private void Konsole_OnEntryLogged(KonsoleEntry entry)
        {
            if (IsCompilerEcho(entry))
            {
                return;
            }

            PENDING_ENTRIES.Enqueue(entry);
        }

        private void EditorApplication_OnUpdate()
        {
            if (PENDING_ENTRIES.IsEmpty)
            {
                return;
            }

            AddPendingEntries(out bool hasError);
            TrimOldEntries();
            PauseOnError(hasError);
            OnChanged?.Invoke();
        }

        private void EditorApplication_OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            Clear();
        }

        private void CompilationPipeline_OnCompilationStarted(object context)
        {
            if (m_Entries.RemoveAll(entry => entry.IsCompilerMessage) == 0)
            {
                return;
            }

            CountSeverities();
            OnChanged?.Invoke();
        }

        private void CompilationPipeline_OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            for (int i = 0; i < messages.Length; i++)
            {
                CompilerMessage message = messages[i];
                if (message.type == CompilerMessageType.Info)
                {
                    continue;
                }

                KonsoleSeverity severity = message.type == CompilerMessageType.Error ? KonsoleSeverity.Error : KonsoleSeverity.Warning;
                PENDING_ENTRIES.Enqueue(KonsoleEntry.CreateCompilerMessage(severity, Konsole.UNITY_CHANNEL, message.message, message.file, message.line));
            }
        }

        [InitializeOnLoadMethod]
        private static void PrepareInstance()
        {
            _ = instance;
        }

        // Unity also sends each compiler message through the log callback, with no stack trace. The compile event already gives that row.
        private static bool IsCompilerEcho(KonsoleEntry entry)
        {
            if (entry.Channel != Konsole.UNITY_CHANNEL || string.IsNullOrEmpty(entry.StackTrace) == false)
            {
                return false;
            }

            return entry.Message.Contains(COMPILER_ERROR_MARKER) || entry.Message.Contains(COMPILER_WARNING_MARKER);
        }

        private void AddPendingEntries(out bool hasError)
        {
            hasError = false;
            while (PENDING_ENTRIES.TryDequeue(out KonsoleEntry entry))
            {
                m_Entries.Add(entry);
                m_SeverityCounts[(int)entry.Severity]++;
                hasError |= entry.Severity == KonsoleSeverity.Error;
            }
        }

        private void TrimOldEntries()
        {
            if (m_Entries.Count <= MAX_ENTRY_COUNT)
            {
                return;
            }

            m_Entries.RemoveRange(0, m_Entries.Count - MAX_ENTRY_COUNT + TRIM_ENTRY_COUNT);
            CountSeverities();
        }

        private void CountSeverities()
        {
            Array.Clear(m_SeverityCounts, 0, m_SeverityCounts.Length);
            for (int i = 0; i < m_Entries.Count; i++)
            {
                m_SeverityCounts[(int)m_Entries[i].Severity]++;
            }
        }

        private void PauseOnError(bool hasError)
        {
            if (hasError == false || IsErrorPauseEnabled == false || EditorApplication.isPlaying == false)
            {
                return;
            }

            EditorApplication.isPaused = true;
        }
    }
}
