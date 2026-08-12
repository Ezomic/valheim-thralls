using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// A small corner crib sheet of the mod's keys. Reads the live config, so it stays
    /// correct if the keys are rebound.
    /// </summary>
    internal static class KeybindOverlay
    {
        private static GUIStyle _title;
        private static GUIStyle _key;
        private static GUIStyle _what;

        public static void Draw()
        {
            if (!ThrallConfig.ShowKeybinds.Value) return;
            if (Player.m_localPlayer == null) return;
            if (AltarUI.IsOpen) return;
            if (Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;

            EnsureStyles();

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.05f, 0.04f, 0.03f, 0.75f);

            GUILayout.BeginArea(new Rect(12f, 150f, 268f, 268f), GUI.skin.box);
            GUILayout.Space(4f);
            GUILayout.Label("THRALLS", _title);

            Row(ThrallConfig.KeyRecruit.Value.ToString(), "recruit a thrall");
            Row(ThrallConfig.KeyAssign.Value.ToString(), "assign job at crosshair");
            Row(ThrallConfig.KeyDeposit.Value.ToString(), "set drop-off chest");
            Row(ThrallConfig.KeyFollow.Value.ToString(), "follow / stay");
            Row(ThrallConfig.KeyDismiss.Value.ToString(), "dismiss thrall");
            Row(ThrallConfig.KeySteward.Value.ToString(), "open altar panel");
            Row(ThrallConfig.KeyPlan.Value.ToString(), "mark build order");
            Row(ThrallConfig.KeyTimeOfDay.Value.ToString(), "time of day");
            Row(ThrallConfig.KeyFlatten.Value.ToString(), "level the ground");

            GUILayout.Space(4f);
            GUILayout.Label(ThrallRegistry.Count() + " thralls   -   "
                            + BuildPlans.Count + " build orders", _what);

            GUILayout.EndArea();
            GUI.backgroundColor = previous;
        }

        private static void Row(string key, string what)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, _key, GUILayout.Width(96f));
            GUILayout.Label(what, _what);
            GUILayout.EndHorizontal();
        }

        private static void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.85f, 0.65f) }
            };
            _key = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.98f, 0.95f) }
            };
            _what = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.75f, 0.73f, 0.68f) }
            };
        }
    }
}

