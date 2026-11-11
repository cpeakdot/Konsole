using UnityEditor;
using UnityEngine;

namespace Cpeakdot.Logging
{
    internal sealed class KonsoleStyles
    {
        public static readonly Color ACCENT_COLOR = new Color(0.61f, 0.48f, 1f, 1f);

        private const float MIN_ROW_HEIGHT = 24f;
        private const float ROW_TEXT_PADDING = 8f;
        private const float TIME_GAP = 12f;
        private const int DETAIL_PADDING = 8;

        private static readonly string[] FONT_NAMES = { "Consolas", "Menlo", "Courier New" };
        private static readonly GUIContent TIME_SAMPLE = new GUIContent("00:00:00.000 #000000");

        // The three arrays below use (int)KonsoleSeverity as the index.
        private static readonly string[] SEVERITY_ICON_NAMES = { "console.infoicon.sml", "console.warnicon.sml", "console.erroricon.sml" };
        private static readonly Color[] SEVERITY_STRIPE_COLORS = { new Color(0.45f, 0.6f, 0.85f, 1f), new Color(1f, 0.8f, 0.2f, 1f), new Color(1f, 0.27f, 0.27f, 1f) };
        private static readonly Color[] SEVERITY_FILL_COLORS = { Color.clear, new Color(1f, 0.82f, 0.25f, 0.26f), new Color(1f, 0.35f, 0.35f, 0.34f) };

        private static readonly Color TIME_TEXT_COLOR = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color FRAME_TEXT_COLOR = new Color(0.6f, 0.78f, 1f, 1f);
        private static readonly Color TAB_ICON_GLYPH_COLOR = new Color(0.12f, 0.12f, 0.12f, 1f);

        private const char TAB_ICON_FILL = 'v';
        private const char TAB_ICON_GLYPH = 'k';
        private static readonly string[] TAB_ICON_ROWS =
        {
            "..vvvvvvvvvvvv..",
            ".vvvvvvvvvvvvvv.",
            "vvvvvvvvvvvvvvvv",
            "vvvvkkvvvvkkvvvv",
            "vvvvkkvvvkkvvvvv",
            "vvvvkkvvkkvvvvvv",
            "vvvvkkvkkvvvvvvv",
            "vvvvkkkkvvvvvvvv",
            "vvvvkkkkvvvvvvvv",
            "vvvvkkvkkvvvvvvv",
            "vvvvkkvvkkvvvvvv",
            "vvvvkkvvvkkvvvvv",
            "vvvvkkvvvvkkvvvv",
            "vvvvvvvvvvvvvvvv",
            ".vvvvvvvvvvvvvv.",
            "..vvvvvvvvvvvv.."
        };

        public Texture2D TabIcon { get; private set; }

        public GUIStyle MessageStyle { get; private set; }
        public GUIStyle TimeStyle { get; private set; }
        public GUIStyle CountStyle { get; private set; }
        public GUIStyle DetailMessageStyle { get; private set; }
        public GUIStyle FrameStyle { get; private set; }

        public int FontSize { get; private set; }
        public float RowHeight { get; private set; }
        public float TextHeight { get; private set; }
        public float TimeWidth { get; private set; }
        public float CharWidth { get; private set; }

        private readonly Texture[] m_SeverityIcons = new Texture[KonsoleStore.SEVERITY_COUNT];
        private Font m_Font;

        public KonsoleStyles(int fontSize)
        {
            FontSize = fontSize;
            TabIcon = CreateTabIcon();
        }

        public void Prepare()
        {
            if (MessageStyle != null)
            {
                return;
            }

            m_Font = Font.CreateDynamicFontFromOSFont(FONT_NAMES, FontSize);
            m_Font.hideFlags = HideFlags.HideAndDontSave;

            CreateRowStyles();
            CreateDetailStyles();

            for (int i = 0; i < KonsoleStore.SEVERITY_COUNT; i++)
            {
                m_SeverityIcons[i] = EditorGUIUtility.IconContent(SEVERITY_ICON_NAMES[i]).image;
            }

            UpdateMetrics();
        }

        public void Release()
        {
            MessageStyle = null;

            Object.DestroyImmediate(TabIcon);
            TabIcon = null;

            if (m_Font == null)
            {
                return;
            }

            Object.DestroyImmediate(m_Font);
            m_Font = null;
        }

        public void SetFontSize(int fontSize)
        {
            FontSize = fontSize;

            if (MessageStyle == null)
            {
                return;
            }

            UpdateMetrics();
        }

        public Texture GetSeverityIcon(KonsoleSeverity severity)
        {
            return m_SeverityIcons[(int)severity];
        }

        public Color GetStripeColor(KonsoleSeverity severity)
        {
            return SEVERITY_STRIPE_COLORS[(int)severity];
        }

        public Color GetFillColor(KonsoleSeverity severity)
        {
            return SEVERITY_FILL_COLORS[(int)severity];
        }

        private static Texture2D CreateTabIcon()
        {
            int size = TAB_ICON_ROWS.Length;
            Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };

            for (int y = 0; y < size; y++)
            {
                string row = TAB_ICON_ROWS[size - 1 - y];
                for (int x = 0; x < size; x++)
                {
                    icon.SetPixel(x, y, GetTabIconColor(row[x]));
                }
            }

            icon.Apply();
            return icon;
        }

        private static Color GetTabIconColor(char cell)
        {
            switch (cell)
            {
                case TAB_ICON_FILL: return ACCENT_COLOR;
                case TAB_ICON_GLYPH: return TAB_ICON_GLYPH_COLOR;
                default: return Color.clear;
            }
        }

        private void CreateRowStyles()
        {
            MessageStyle = new GUIStyle(EditorStyles.label)
            {
                font = m_Font,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset()
            };

            TimeStyle = new GUIStyle(MessageStyle) { richText = false };
            TimeStyle.normal.textColor = TIME_TEXT_COLOR;

            CountStyle = new GUIStyle(MessageStyle) { richText = false, alignment = TextAnchor.MiddleCenter };
        }

        private void CreateDetailStyles()
        {
            DetailMessageStyle = new GUIStyle(MessageStyle)
            {
                wordWrap = true,
                padding = new RectOffset(DETAIL_PADDING, DETAIL_PADDING, DETAIL_PADDING, DETAIL_PADDING)
            };

            FrameStyle = new GUIStyle(MessageStyle)
            {
                richText = false,
                padding = new RectOffset(DETAIL_PADDING, DETAIL_PADDING, 1, 1)
            };
            FrameStyle.normal.textColor = FRAME_TEXT_COLOR;
            FrameStyle.hover.textColor = Color.white;
        }

        private void UpdateMetrics()
        {
            MessageStyle.fontSize = FontSize;
            TimeStyle.fontSize = FontSize;
            CountStyle.fontSize = FontSize;
            DetailMessageStyle.fontSize = FontSize;
            FrameStyle.fontSize = FontSize;

            Vector2 timeSize = TimeStyle.CalcSize(TIME_SAMPLE);
            TimeWidth = timeSize.x + TIME_GAP;
            CharWidth = timeSize.x / TIME_SAMPLE.text.Length;
            TextHeight = timeSize.y;
            RowHeight = Mathf.Max(MIN_ROW_HEIGHT, TextHeight + ROW_TEXT_PADDING);
        }
    }
}
