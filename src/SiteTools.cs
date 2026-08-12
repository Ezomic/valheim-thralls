using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Two conveniences for laying out a settlement: force the time of day, and flatten a
    /// wide patch of ground in one go rather than a hundred hoe taps.
    /// </summary>
    internal static class SiteTools
    {
        private static readonly string[] PhaseNames = { "dawn", "midday", "dusk", "night", "normal" };
        private static readonly float[] PhaseTimes = { 0.24f, 0.5f, 0.72f, 0.0f, -1f };
        private static int _phase = -1;

        /// <summary>
        /// Development crutch: keeps health and stamina topped up and turns on the game's
        /// own god mode, so building and testing is not interrupted by dying or resting.
        /// </summary>
        public static void KeepAlive()
        {
            if (!ThrallConfig.GodMode.Value) return;

            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) return;

            if (!player.InGodMode()) player.SetGodMode(true);

            player.SetHealth(player.GetMaxHealth());
            player.AddStamina(player.GetMaxStamina());
            player.AddEitr(100f);
        }

        public static void ToggleGodMode()
        {
            ThrallConfig.GodMode.Value = !ThrallConfig.GodMode.Value;

            var player = Player.m_localPlayer;
            if (player != null && !ThrallConfig.GodMode.Value && player.InGodMode())
                player.SetGodMode(false);

            ThrallsPlugin.Say(ThrallConfig.GodMode.Value
                ? "Unwearying: health and stamina held full."
                : "Mortal again.");
        }

        /// <summary>Steps through dawn, midday, dusk, night, then hands time back to the game.</summary>
        public static void CycleTimeOfDay()
        {
            var env = EnvMan.instance;
            if (env == null) return;

            _phase = (_phase + 1) % PhaseNames.Length;
            var time = PhaseTimes[_phase];

            if (time < 0f)
            {
                env.m_debugTimeOfDay = false;
                ThrallsPlugin.Say("Time runs on as normal.");
                return;
            }

            env.m_debugTimeOfDay = true;
            env.m_debugTime = time;
            ThrallsPlugin.Say("Time held at " + PhaseNames[_phase] + ".");
        }

        /// <summary>
        /// Levels a circle of ground to the height you are standing at - the aim point only
        /// chooses where the circle goes, never how high it ends up, so the result is always
        /// flush with your own feet.
        /// </summary>
        public static void Flatten(Vector3 centre, float radius)
        {
            if (TerrainOp.m_forceDisableTerrainOps)
            {
                ThrallsPlugin.Say("Terrain is locked right now.");
                return;
            }

            var settings = new TerrainOp.Settings
            {
                m_level = true,
                m_levelRadius = radius,
                m_levelOffset = 0f,
                m_square = false,
                m_raise = false,
                m_smooth = true,
                m_smoothRadius = radius + 2f,
                m_smoothPower = 3f,
                m_paintCleared = true,
                m_paintHeightCheck = false,
                m_paintType = TerrainModifier.PaintType.Dirt,
                m_paintRadius = radius
            };

            // The op levels to its own y, so take the height from the player's feet rather
            // than from wherever the crosshair happened to land.
            var player = Player.m_localPlayer;
            if (player != null) centre.y = player.transform.position.y;

            // Built inactive on purpose: TerrainOp does all the work in Awake, and Awake
            // fires the moment the component is added - before settings could be assigned.
            var holder = new GameObject("thrall_flatten");
            holder.SetActive(false);
            holder.transform.position = centre;

            var op = holder.AddComponent<TerrainOp>();
            op.m_settings = settings;

            // Awake finds every heightmap the circle touches and creates the terrain
            // compilers it needs, so a flatten across a zone seam is handled for us.
            holder.SetActive(true);

            Object.Destroy(holder);

            ThrallsPlugin.Say(string.Format("Levelled {0}m of ground to your height.",
                Mathf.RoundToInt(radius * 2f)));
        }
    }
}
