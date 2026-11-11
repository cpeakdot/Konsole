using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cpeakdot.Logging
{
    internal sealed class KonsoleRowList
    {
        private const float STRIPE_WIDTH = 3f;
        private const float ICON_SIZE = 16f;
        private const float ICON_LEFT = 6f;
        private const float TIME_LEFT = 27f;
        private const float COUNT_BADGE_WIDTH = 44f;
        private const float COUNT_BADGE_MARGIN = 4f;

        private const float CHANNEL_TAG_PADDING = 5f;
        private const float CHANNEL_TAG_MARGIN = 3f;
        private const float CHANNEL_TAG_GAP = 6f;
        private const float CHANNEL_TAG_SATURATION = 0.5f;
        private const float CHANNEL_TAG_BRIGHTNESS = 0.7f;
        private const float CHANNEL_TAG_ALPHA = 0.75f;
        private const int CHANNEL_HUE_COUNT = 360;

        private static readonly Color SELECTED_COLOR = new Color(0.24f, 0.49f, 0.9f, 0.5f);
        private static readonly Color BASE_ROW_COLOR = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color ALTERNATE_ROW_COLOR = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color ROW_TOP_EDGE_COLOR = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color ROW_BOTTOM_EDGE_COLOR = new Color(0f, 0f, 0f, 0.3f);
        private static readonly Color COUNT_BADGE_COLOR = new Color(0f, 0f, 0f, 0.35f);

        private readonly List<int> m_VisibleIndices = new List<int>();
        public IReadOnlyList<int> VisibleIndices => m_VisibleIndices;

        private readonly KonsoleStore m_Store;
        private readonly KonsoleFilter m_Filter;
        private readonly KonsoleStyles m_Styles;

        private readonly List<int> m_RowCounts = new List<int>();
        private readonly List<string> m_RowCountTexts = new List<string>();
        private readonly Dictionary<(KonsoleSeverity, string, string), int> m_RowByKey = new Dictionary<(KonsoleSeverity, string, string), int>();

        private Vector2 m_ScrollPosition;
        private float m_MaxScroll;
        private int m_SelectedIndex = -1;
        private bool m_IsDirty = true;
        private bool m_IsAtBottom = true;

        public event Action OnRowsUpdated;
        public event Action OnSelectionChanged;
        public event Action<KonsoleEntry> OnEntryOpened;
        public event Action<KonsoleEntry> OnEntryContextClicked;

        public KonsoleRowList(KonsoleStore store, KonsoleFilter filter, KonsoleStyles styles)
        {
            m_Store = store;
            m_Filter = filter;
            m_Styles = styles;
        }

        public void MarkDirty()
        {
            m_IsDirty = true;
        }

        public void ClearSelection()
        {
            m_SelectedIndex = -1;
            OnSelectionChanged?.Invoke();
        }

        public bool TryGetSelectedEntry(out KonsoleEntry entry)
        {
            entry = default;
            if (m_SelectedIndex < 0 || m_SelectedIndex >= m_Store.Entries.Count)
            {
                return false;
            }

            entry = m_Store.Entries[m_SelectedIndex];
            return true;
        }

        public void Draw()
        {
            UpdateRows();
            DrawRows();
        }

        // ─── Row data ───

        private void UpdateRows()
        {
            if (m_IsDirty == false)
            {
                return;
            }

            m_IsDirty = false;
            m_VisibleIndices.Clear();
            m_RowCounts.Clear();
            m_RowByKey.Clear();

            IReadOnlyList<KonsoleEntry> entries = m_Store.Entries;
            if (m_SelectedIndex >= entries.Count)
            {
                m_SelectedIndex = -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (m_Filter.IsMatch(entries[i]))
                {
                    AddVisibleRow(i, entries[i]);
                }
            }

            UpdateRowCountTexts();
            OnRowsUpdated?.Invoke();
        }

        private void AddVisibleRow(int entryIndex, KonsoleEntry entry)
        {
            if (m_Filter.IsCollapsed == false)
            {
                m_VisibleIndices.Add(entryIndex);
                return;
            }

            (KonsoleSeverity, string, string) key = (entry.Severity, entry.Channel, entry.Message);
            if (m_RowByKey.TryGetValue(key, out int row) == false)
            {
                m_RowByKey.Add(key, m_VisibleIndices.Count);
                m_VisibleIndices.Add(entryIndex);
                m_RowCounts.Add(1);
                return;
            }

            if (m_VisibleIndices[row] == m_SelectedIndex)
            {
                m_SelectedIndex = entryIndex;
            }

            m_VisibleIndices[row] = entryIndex;
            m_RowCounts[row]++;
        }

        private void UpdateRowCountTexts()
        {
            m_RowCountTexts.Clear();
            for (int i = 0; i < m_RowCounts.Count; i++)
            {
                m_RowCountTexts.Add(m_RowCounts[i].ToString());
            }
        }

        // ─── Row drawing ───

        private void DrawRows()
        {
            Rect viewRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // GetRect returns a placeholder rect during Layout, so the scroll limit comes from the last real event.
            // The scroll view still runs in Layout: IMGUI gives its control ID to the detail scroll view if it does not.
            float contentHeight = m_VisibleIndices.Count * m_Styles.RowHeight;
            if (Event.current.type != EventType.Layout)
            {
                m_MaxScroll = Mathf.Max(0f, contentHeight - viewRect.height);
            }

            if (m_IsAtBottom)
            {
                m_ScrollPosition.y = m_MaxScroll;
            }

            float scrollbarWidth = m_MaxScroll > 0f ? GUI.skin.verticalScrollbar.fixedWidth : 0f;
            Rect contentRect = new Rect(0f, 0f, viewRect.width - scrollbarWidth, contentHeight);
            m_ScrollPosition = GUI.BeginScrollView(viewRect, m_ScrollPosition, contentRect);
            DrawVisibleRows(viewRect.height, contentRect.width);
            GUI.EndScrollView();

            m_IsAtBottom = m_ScrollPosition.y >= m_MaxScroll - 1f;
        }

        private void DrawVisibleRows(float viewHeight, float rowWidth)
        {
            float rowHeight = m_Styles.RowHeight;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(m_ScrollPosition.y / rowHeight));
            int lastRow = Mathf.Min(m_VisibleIndices.Count - 1, firstRow + Mathf.CeilToInt(viewHeight / rowHeight));

            for (int row = firstRow; row <= lastRow; row++)
            {
                DrawRow(row, new Rect(0f, row * rowHeight, rowWidth, rowHeight));
            }
        }

        private void DrawRow(int row, Rect rect)
        {
            int entryIndex = m_VisibleIndices[row];
            KonsoleEntry entry = m_Store.Entries[entryIndex];

            HandleRowClick(entryIndex, entry, rect);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawRowBackground(row, entryIndex, entry.Severity, rect);
            DrawRowContent(row, entry, rect);
        }

        private void DrawRowBackground(int row, int entryIndex, KonsoleSeverity severity, Rect rect)
        {
            EditorGUI.DrawRect(rect, BASE_ROW_COLOR);

            if (row % 2 == 1)
            {
                EditorGUI.DrawRect(rect, ALTERNATE_ROW_COLOR);
            }

            EditorGUI.DrawRect(rect, m_Styles.GetFillColor(severity));

            if (entryIndex == m_SelectedIndex)
            {
                EditorGUI.DrawRect(rect, SELECTED_COLOR);
            }

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, STRIPE_WIDTH, rect.height), m_Styles.GetStripeColor(severity));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), ROW_TOP_EDGE_COLOR);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ROW_BOTTOM_EDGE_COLOR);
        }

        private void DrawRowContent(int row, KonsoleEntry entry, Rect rect)
        {
            float iconY = rect.y + (rect.height - ICON_SIZE) * 0.5f;
            GUI.DrawTexture(new Rect(rect.x + ICON_LEFT, iconY, ICON_SIZE, ICON_SIZE), m_Styles.GetSeverityIcon(entry.Severity));

            float badgeWidth = m_Filter.IsCollapsed ? COUNT_BADGE_WIDTH : 0f;
            float textY = rect.y + (rect.height - m_Styles.TextHeight) * 0.5f;
            Rect timeRect = new Rect(rect.x + TIME_LEFT, textY, m_Styles.TimeWidth, m_Styles.TextHeight);
            float messageX = DrawChannelTag(entry.Channel, timeRect.xMax, rect);
            Rect messageRect = new Rect(messageX, textY, rect.xMax - badgeWidth - messageX, m_Styles.TextHeight);
            GUI.Label(timeRect, entry.TimeText, m_Styles.TimeStyle);
            GUI.Label(messageRect, entry.Message, m_Styles.MessageStyle);

            if (m_Filter.IsCollapsed)
            {
                DrawCountBadge(row, rect);
            }
        }

        private float DrawChannelTag(string channel, float x, Rect rowRect)
        {
            if (channel.Length == 0)
            {
                return x;
            }

            float width = channel.Length * m_Styles.CharWidth + CHANNEL_TAG_PADDING * 2f;
            Rect tagRect = new Rect(x, rowRect.y + CHANNEL_TAG_MARGIN, width, rowRect.height - CHANNEL_TAG_MARGIN * 2f);
            EditorGUI.DrawRect(tagRect, GetChannelColor(channel));
            GUI.Label(tagRect, channel, m_Styles.CountStyle);
            return tagRect.xMax + CHANNEL_TAG_GAP;
        }

        private static Color GetChannelColor(string channel)
        {
            float hue = (channel.GetHashCode() & int.MaxValue) % CHANNEL_HUE_COUNT / (float)CHANNEL_HUE_COUNT;
            Color color = Color.HSVToRGB(hue, CHANNEL_TAG_SATURATION, CHANNEL_TAG_BRIGHTNESS);
            color.a = CHANNEL_TAG_ALPHA;
            return color;
        }

        private void DrawCountBadge(int row, Rect rect)
        {
            float width = COUNT_BADGE_WIDTH - COUNT_BADGE_MARGIN * 2f;
            float height = rect.height - COUNT_BADGE_MARGIN * 2f;
            Rect badgeRect = new Rect(rect.xMax - COUNT_BADGE_WIDTH + COUNT_BADGE_MARGIN, rect.y + COUNT_BADGE_MARGIN, width, height);

            EditorGUI.DrawRect(badgeRect, COUNT_BADGE_COLOR);
            GUI.Label(badgeRect, m_RowCountTexts[row], m_Styles.CountStyle);
        }

        // ─── Row clicks ───

        private void HandleRowClick(int entryIndex, KonsoleEntry entry, Rect rect)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || rect.Contains(current.mousePosition) == false)
            {
                return;
            }

            m_SelectedIndex = entryIndex;
            OnSelectionChanged?.Invoke();

            if (current.button == 1)
            {
                OnEntryContextClicked?.Invoke(entry);
            }
            else if (current.button == 0 && current.clickCount == 2)
            {
                OnEntryOpened?.Invoke(entry);
            }
            else if (current.button == 0)
            {
                SelectContextObject(entry);
            }

            current.Use();
        }

        private static void SelectContextObject(KonsoleEntry entry)
        {
            if (entry.ContextId == KonsoleEntry.NO_CONTEXT)
            {
                return;
            }

            UnityEngine.Object context = EditorUtility.InstanceIDToObject(entry.ContextId);
            if (context == null)
            {
                return;
            }

            Selection.activeObject = context;
            EditorGUIUtility.PingObject(context);
        }
    }
}
