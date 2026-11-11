using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cpeakdot.Logging
{
    internal sealed class KonsoleDetailPanel
    {
        private const float SPLITTER_HEIGHT = 18f;
        private const float CLOSE_BUTTON_WIDTH = 24f;
        private const float MIN_HEIGHT = 60f;
        private const float MAX_HEIGHT_RATIO = 0.8f;

        private static readonly Color SPLITTER_COLOR = new Color(0f, 0f, 0f, 0.45f);

        public float Height { get; private set; }

        private readonly KonsoleStyles m_Styles;
        private readonly string m_ProjectRoot;
        private readonly List<string> m_FrameLines = new List<string>();

        private KonsoleEntry m_Entry;
        private KonsoleEntry m_PendingEntry;
        private bool m_HasPendingEntry;
        private bool m_IsDirty;
        private bool m_IsVisible;

        private Vector2 m_ScrollPosition;
        private bool m_IsSplitterDragged;

        public event Action OnClosed;
        public event Action<string> OnFrameClicked;

        public KonsoleDetailPanel(KonsoleStyles styles, string projectRoot, float height)
        {
            m_Styles = styles;
            m_ProjectRoot = projectRoot;
            Height = height;
        }

        public void ShowEntry(KonsoleEntry entry)
        {
            m_PendingEntry = entry;
            m_HasPendingEntry = true;
            m_IsDirty = true;
        }

        public void Hide()
        {
            m_HasPendingEntry = false;
            m_IsDirty = true;
        }

        public void Draw(float windowHeight)
        {
            UpdateEntry();

            if (m_IsVisible == false)
            {
                return;
            }

            DrawSplitter(windowHeight);
            DrawContent();
        }

        // IMGUI needs the same layout controls in the Layout event and in the events after it, so the panel state changes only in Layout.
        private void UpdateEntry()
        {
            if (Event.current.type != EventType.Layout || m_IsDirty == false)
            {
                return;
            }

            m_IsDirty = false;
            m_FrameLines.Clear();
            m_IsVisible = m_HasPendingEntry;
            if (m_IsVisible == false)
            {
                return;
            }

            m_Entry = m_PendingEntry;
            if (string.IsNullOrEmpty(m_Entry.StackTrace))
            {
                return;
            }

            m_FrameLines.AddRange(m_Entry.StackTrace.Replace(m_ProjectRoot, string.Empty).Split('\n'));
        }

        private void DrawContent()
        {
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(Height));
            GUILayout.Label(m_Entry.Message, m_Styles.DetailMessageStyle);

            for (int i = 0; i < m_FrameLines.Count; i++)
            {
                if (GUILayout.Button(m_FrameLines[i], m_Styles.FrameStyle))
                {
                    OnFrameClicked?.Invoke(m_FrameLines[i]);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSplitter(float windowHeight)
        {
            Rect splitterRect = GUILayoutUtility.GetRect(0f, SPLITTER_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(splitterRect, SPLITTER_COLOR);

            Rect closeRect = new Rect(splitterRect.xMax - CLOSE_BUTTON_WIDTH, splitterRect.y, CLOSE_BUTTON_WIDTH, splitterRect.height);
            if (GUI.Button(closeRect, "×", EditorStyles.toolbarButton))
            {
                OnClosed?.Invoke();
            }

            Rect dragRect = new Rect(splitterRect.x, splitterRect.y, splitterRect.width - CLOSE_BUTTON_WIDTH, splitterRect.height);
            GUI.Label(dragRect, "Stack trace", EditorStyles.miniLabel);
            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.ResizeVertical);
            HandleSplitterDrag(dragRect, windowHeight);
        }

        private void HandleSplitterDrag(Rect dragRect, float windowHeight)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && dragRect.Contains(current.mousePosition))
            {
                m_IsSplitterDragged = true;
                current.Use();
            }

            if (current.rawType == EventType.MouseUp)
            {
                m_IsSplitterDragged = false;
            }

            if (m_IsSplitterDragged == false || current.type != EventType.MouseDrag)
            {
                return;
            }

            Height = Mathf.Clamp(Height - current.delta.y, MIN_HEIGHT, windowHeight * MAX_HEIGHT_RATIO);
            current.Use();
        }
    }
}
