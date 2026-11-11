using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cpeakdot.Logging
{
    [Serializable]
    internal sealed class KonsoleFilter
    {
        [field: SerializeField] public string SearchText { get; set; } = string.Empty;
        [field: SerializeField] public bool IsCollapsed { get; set; }

        // One flag for each KonsoleSeverity value, with (int)KonsoleSeverity as the index.
        [SerializeField] private bool[] m_IsSeverityVisible = { true, true, true };
        [SerializeField] private List<string> m_HiddenChannels = new List<string> { Konsole.UNITY_CHANNEL };

        public bool IsSeverityVisible(KonsoleSeverity severity)
        {
            return m_IsSeverityVisible[(int)severity];
        }

        public void SetSeverityVisible(KonsoleSeverity severity, bool isVisible)
        {
            m_IsSeverityVisible[(int)severity] = isVisible;
        }

        public bool IsChannelVisible(string channel)
        {
            return m_HiddenChannels.Contains(channel) == false;
        }

        public void ToggleChannel(string channel)
        {
            if (m_HiddenChannels.Remove(channel) == false)
            {
                m_HiddenChannels.Add(channel);
            }
        }

        public void ShowAllChannels()
        {
            m_HiddenChannels.Clear();
        }

        public bool IsMatch(KonsoleEntry entry)
        {
            if (IsSeverityVisible(entry.Severity) == false || IsChannelVisible(entry.Channel) == false)
            {
                return false;
            }

            if (string.IsNullOrEmpty(SearchText))
            {
                return true;
            }

            return entry.Message.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
