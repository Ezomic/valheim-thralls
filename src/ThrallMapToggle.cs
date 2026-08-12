using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Thralls
{
    /// <summary>
    /// Puts a "Show thralls" checkbox on the map, right under the one that shares your
    /// position with other players.
    ///
    /// It is a clone of that checkbox rather than a box drawn to look like it, so it
    /// inherits the game's font, tick, spacing and hover behaviour exactly, and keeps
    /// inheriting them if the game restyles its UI.
    /// </summary>
    internal static class ThrallMapToggle
    {
        private static Toggle _toggle;

        public static void Build(Minimap map)
        {
            if (_toggle != null) return;

            var donor = map != null ? map.m_publicPosition : null;
            if (donor == null)
            {
                ThrallsPlugin.Log.LogWarning(
                    "No public-position checkbox on the map to copy; thralls can still be "
                    + "shown by setting ShowThrallsOnMap in the config.");
                return;
            }

            var copy = Object.Instantiate(donor.gameObject, donor.transform.parent);
            copy.name = "thralls_show_toggle";

            // Directly beneath the one it was copied from.
            var rect = copy.GetComponent<RectTransform>();
            var from = donor.GetComponent<RectTransform>();
            if (rect != null && from != null)
            {
                rect.anchorMin = from.anchorMin;
                rect.anchorMax = from.anchorMax;
                rect.pivot = from.pivot;
                rect.anchoredPosition = from.anchoredPosition
                                        + new Vector2(0f, -ThrallConfig.MapToggleOffset.Value);
            }

            Relabel(copy, "Show thralls");

            _toggle = copy.GetComponent<Toggle>();
            if (_toggle == null)
            {
                ThrallsPlugin.Log.LogWarning("The copied checkbox has no Toggle on it.");
                return;
            }

            // The original reports your position to the server. Anything the copy
            // inherited that would do the same has to go, or ticking "show thralls" would
            // quietly broadcast your location too.
            for (int i = 0; i < _toggle.onValueChanged.GetPersistentEventCount(); i++)
                _toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            _toggle.onValueChanged.RemoveAllListeners();

            _toggle.isOn = ThrallConfig.ShowThrallsOnMap.Value;
            _toggle.onValueChanged.AddListener(OnChanged);

            ThrallMap.SetShown(_toggle.isOn);
            ThrallsPlugin.Log.LogInfo("Added the 'Show thralls' checkbox to the map.");
        }

        private static void OnChanged(bool on)
        {
            ThrallConfig.ShowThrallsOnMap.Value = on;
            ThrallMap.SetShown(on);
        }

        /// <summary>
        /// Retitles the copy. Valheim's UI text is TextMeshPro, which lives in an assembly
        /// this one does not reference, so the label is set through whatever component
        /// exposes a text property rather than by naming the type.
        /// </summary>
        private static void Relabel(GameObject go, string label)
        {
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var type = component.GetType();
                if (type.Name.IndexOf("Text", System.StringComparison.Ordinal) < 0) continue;

                var property = type.GetProperty("text");
                if (property == null || !property.CanWrite) continue;

                try { property.SetValue(component, label, null); }
                catch { /* not every text-ish component takes a plain string */ }
            }
        }
    }
}
