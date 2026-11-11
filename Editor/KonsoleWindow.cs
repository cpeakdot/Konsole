using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Cpeakdot.Logging
{
    internal sealed class KonsoleWindow : EditorWindow
    {
        private const float CLEAR_BUTTON_WIDTH = 50f;
        private const float SAVE_BUTTON_WIDTH = 44f;
        private const float ERROR_PAUSE_BUTTON_WIDTH = 80f;
        private const float COLLAPSE_BUTTON_WIDTH = 64f;
        private const float CHANNEL_BUTTON_WIDTH = 76f;
        private const float SEVERITY_BUTTON_MIN_WIDTH = 44f;
        private const float FONT_SIZE_SLIDER_WIDTH = 150f;
        private const float DEFAULT_DETAIL_HEIGHT = 150f;
        private const float ACCENT_LINE_HEIGHT = 3f;
        private const int NO_TITLE_ERROR_COUNT = -1;

        private const string FONT_SIZE_PREF_KEY = "Cpeakdot.Konsole.FontSize";
        private const int DEFAULT_FONT_SIZE = 17;
        private const int MIN_FONT_SIZE = 9;
        private const int MAX_FONT_SIZE = 20;

        private const string WINDOW_TITLE = "Konsole";
        private const string NO_CHANNEL_LABEL = "(none)";
        private const string EXPORT_FILE_NAME = "konsole.txt";
        private const string EXPORT_FILE_EXTENSION = "txt";
        private const string EXPORT_STACK_INDENT = "    ";

        private static readonly GUIContent CHANNEL_BUTTON_CONTENT = new GUIContent("Channels");

        [SerializeField] private KonsoleFilter m_Filter = new KonsoleFilter();
        [SerializeField] private float m_DetailHeight = DEFAULT_DETAIL_HEIGHT;

        private readonly GUIContent[] m_SeverityContents = new GUIContent[KonsoleStore.SEVERITY_COUNT];
        private KonsoleStore m_Store;
        private KonsoleStyles m_Styles;
        private KonsoleRowList m_RowList;
        private KonsoleDetailPanel m_DetailPanel;
        private string m_ProjectRoot;
        private int m_TitleErrorCount;

        private void OnEnable()
        {
            m_Store = KonsoleStore.instance;
            m_ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar;

            m_Styles = new KonsoleStyles(EditorPrefs.GetInt(FONT_SIZE_PREF_KEY, DEFAULT_FONT_SIZE));
            m_RowList = new KonsoleRowList(m_Store, m_Filter, m_Styles);
            m_DetailPanel = new KonsoleDetailPanel(m_Styles, m_ProjectRoot, m_DetailHeight);

            m_Store.OnChanged += KonsoleStore_OnChanged;
            m_RowList.OnRowsUpdated += RowList_OnRowsUpdated;
            m_RowList.OnSelectionChanged += RowList_OnSelectionChanged;
            m_RowList.OnEntryOpened += RowList_OnEntryOpened;
            m_RowList.OnEntryContextClicked += RowList_OnEntryContextClicked;
            m_DetailPanel.OnClosed += DetailPanel_OnClosed;
            m_DetailPanel.OnFrameClicked += DetailPanel_OnFrameClicked;

            m_TitleErrorCount = NO_TITLE_ERROR_COUNT;
            UpdateTitle();
        }

        private void OnDisable()
        {
            m_Store.OnChanged -= KonsoleStore_OnChanged;
            m_RowList.OnRowsUpdated -= RowList_OnRowsUpdated;
            m_RowList.OnSelectionChanged -= RowList_OnSelectionChanged;
            m_RowList.OnEntryOpened -= RowList_OnEntryOpened;
            m_RowList.OnEntryContextClicked -= RowList_OnEntryContextClicked;
            m_DetailPanel.OnClosed -= DetailPanel_OnClosed;
            m_DetailPanel.OnFrameClicked -= DetailPanel_OnFrameClicked;

            m_Styles.Release();
        }

        private void OnGUI()
        {
            m_Styles.Prepare();
            PrepareSeverityContents();

            DrawAccentLine();
            DrawToolbar();
            m_RowList.Draw();
            m_DetailPanel.Draw(position.height);

            m_DetailHeight = m_DetailPanel.Height;
        }

        private void KonsoleStore_OnChanged()
        {
            m_RowList.MarkDirty();
            UpdateTitle();
            Repaint();
        }

        private void RowList_OnRowsUpdated()
        {
            for (int i = 0; i < KonsoleStore.SEVERITY_COUNT; i++)
            {
                m_SeverityContents[i].text = m_Store.GetSeverityCount((KonsoleSeverity)i).ToString();
            }

            UpdateDetailPanel();
        }

        private void RowList_OnSelectionChanged()
        {
            UpdateDetailPanel();
            Repaint();
        }

        private void RowList_OnEntryOpened(KonsoleEntry entry)
        {
            if (string.IsNullOrEmpty(entry.StackTrace))
            {
                return;
            }

            string[] frameLines = entry.StackTrace.Split('\n');
            for (int i = 0; i < frameLines.Length; i++)
            {
                if (TryOpenSourceLine(frameLines[i]))
                {
                    return;
                }
            }
        }

        private void RowList_OnEntryContextClicked(KonsoleEntry entry)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy message"), false, CopyText, entry.Message);
            menu.AddItem(new GUIContent("Copy message and stack trace"), false, CopyText, entry.Message + "\n" + entry.StackTrace);
            menu.ShowAsContext();
        }

        private void DetailPanel_OnClosed()
        {
            m_RowList.ClearSelection();
        }

        private void DetailPanel_OnFrameClicked(string frameLine)
        {
            TryOpenSourceLine(frameLine);
        }

        [MenuItem("Tools/Konsole")]
        private static void Open()
        {
            GetWindow<KonsoleWindow>();
        }

        private void UpdateTitle()
        {
            int errorCount = m_Store.GetSeverityCount(KonsoleSeverity.Error);
            if (errorCount == m_TitleErrorCount)
            {
                return;
            }

            m_TitleErrorCount = errorCount;
            string title = errorCount > 0 ? WINDOW_TITLE + " (" + errorCount + ")" : WINDOW_TITLE;
            titleContent = new GUIContent(title, m_Styles.TabIcon);
        }

        private static void DrawAccentLine()
        {
            Rect lineRect = GUILayoutUtility.GetRect(0f, ACCENT_LINE_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, KonsoleStyles.ACCENT_COLOR);
        }

        private void PrepareSeverityContents()
        {
            if (m_SeverityContents[0] != null)
            {
                return;
            }

            for (int i = 0; i < KonsoleStore.SEVERITY_COUNT; i++)
            {
                KonsoleSeverity severity = (KonsoleSeverity)i;
                m_SeverityContents[i] = new GUIContent(m_Store.GetSeverityCount(severity).ToString(), m_Styles.GetSeverityIcon(severity));
            }
        }

        private void UpdateDetailPanel()
        {
            if (m_RowList.TryGetSelectedEntry(out KonsoleEntry entry))
            {
                m_DetailPanel.ShowEntry(entry);
                return;
            }

            m_DetailPanel.Hide();
        }

        // ─── Toolbar ───

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawActionButtons();
            DrawFilters();
            DrawFontSizeSlider();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(CLEAR_BUTTON_WIDTH)))
            {
                m_Store.Clear();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(SAVE_BUTTON_WIDTH)))
            {
                SaveVisibleRows();
            }

            bool isErrorPauseEnabled = GUILayout.Toggle(m_Store.IsErrorPauseEnabled, "Error Pause", EditorStyles.toolbarButton, GUILayout.Width(ERROR_PAUSE_BUTTON_WIDTH));
            if (isErrorPauseEnabled != m_Store.IsErrorPauseEnabled)
            {
                m_Store.SetErrorPause(isErrorPauseEnabled);
            }
        }

        private void DrawFilters()
        {
            EditorGUI.BeginChangeCheck();

            m_Filter.IsCollapsed = GUILayout.Toggle(m_Filter.IsCollapsed, "Collapse", EditorStyles.toolbarButton, GUILayout.Width(COLLAPSE_BUTTON_WIDTH));
            m_Filter.SearchText = EditorGUILayout.TextField(m_Filter.SearchText, EditorStyles.toolbarSearchField);

            if (EditorGUILayout.DropdownButton(CHANNEL_BUTTON_CONTENT, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(CHANNEL_BUTTON_WIDTH)))
            {
                ShowChannelMenu();
            }

            for (int i = 0; i < KonsoleStore.SEVERITY_COUNT; i++)
            {
                KonsoleSeverity severity = (KonsoleSeverity)i;
                bool isVisible = GUILayout.Toggle(m_Filter.IsSeverityVisible(severity), m_SeverityContents[i], EditorStyles.toolbarButton, GUILayout.MinWidth(SEVERITY_BUTTON_MIN_WIDTH), GUILayout.ExpandWidth(false));
                m_Filter.SetSeverityVisible(severity, isVisible);
            }

            if (EditorGUI.EndChangeCheck())
            {
                m_RowList.MarkDirty();
            }
        }

        private void DrawFontSizeSlider()
        {
            EditorGUI.BeginChangeCheck();
            int fontSize = EditorGUILayout.IntSlider(m_Styles.FontSize, MIN_FONT_SIZE, MAX_FONT_SIZE, GUILayout.Width(FONT_SIZE_SLIDER_WIDTH));
            if (EditorGUI.EndChangeCheck() == false)
            {
                return;
            }

            EditorPrefs.SetInt(FONT_SIZE_PREF_KEY, fontSize);
            m_Styles.SetFontSize(fontSize);
        }

        // ─── Channels ───

        private void ShowChannelMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show all"), false, ShowAllChannels);
            menu.AddSeparator(string.Empty);

            foreach (KeyValuePair<string, int> channelCount in CountChannels())
            {
                string channel = channelCount.Key;
                string name = channel.Length == 0 ? NO_CHANNEL_LABEL : channel;
                menu.AddItem(new GUIContent(name + "  (" + channelCount.Value + ")"), m_Filter.IsChannelVisible(channel), ToggleChannel, channel);
            }

            menu.ShowAsContext();
        }

        private SortedDictionary<string, int> CountChannels()
        {
            SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<KonsoleEntry> entries = m_Store.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                counts.TryGetValue(entries[i].Channel, out int count);
                counts[entries[i].Channel] = count + 1;
            }

            return counts;
        }

        private void ShowAllChannels()
        {
            m_Filter.ShowAllChannels();
            m_RowList.MarkDirty();
            Repaint();
        }

        private void ToggleChannel(object channel)
        {
            m_Filter.ToggleChannel((string)channel);
            m_RowList.MarkDirty();
            Repaint();
        }

        // ─── Export, copy and source lines ───

        private void SaveVisibleRows()
        {
            string path = EditorUtility.SaveFilePanel("Save Konsole", string.Empty, EXPORT_FILE_NAME, EXPORT_FILE_EXTENSION);
            if (string.IsNullOrEmpty(path) == false)
            {
                File.WriteAllText(path, CreateExportText());
            }

            // A modal dialog inside OnGUI breaks the layout state of the current event.
            GUIUtility.ExitGUI();
        }

        private string CreateExportText()
        {
            StringBuilder builder = new StringBuilder();
            IReadOnlyList<KonsoleEntry> entries = m_Store.Entries;
            IReadOnlyList<int> visibleIndices = m_RowList.VisibleIndices;

            for (int row = 0; row < visibleIndices.Count; row++)
            {
                AppendExportEntry(builder, entries[visibleIndices[row]]);
            }

            return builder.ToString();
        }

        private static void AppendExportEntry(StringBuilder builder, KonsoleEntry entry)
        {
            builder.Append(entry.TimeText).Append(" [").Append(entry.Severity.ToString()).Append("] ");
            if (entry.Channel.Length > 0)
            {
                builder.Append('[').Append(entry.Channel).Append("] ");
            }

            builder.AppendLine(entry.Message);

            if (string.IsNullOrEmpty(entry.StackTrace) == false)
            {
                builder.Append(EXPORT_STACK_INDENT).AppendLine(entry.StackTrace.Replace("\n", "\n" + EXPORT_STACK_INDENT));
            }
        }

        private static void CopyText(object text)
        {
            EditorGUIUtility.systemCopyBuffer = (string)text;
        }

        private bool TryOpenSourceLine(string frameLine)
        {
            if (KonsoleEntry.TryParseFrame(frameLine, out string filePath, out int line) == false)
            {
                return false;
            }

            if (Path.IsPathRooted(filePath) == false)
            {
                filePath = Path.Combine(m_ProjectRoot, filePath);
            }

            return InternalEditorUtility.OpenFileAtLineExternal(filePath, line, 0);
        }
    }
}
