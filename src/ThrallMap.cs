using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Shows the crew on the map, and keeps showing them as they move.
    ///
    /// These are live markers, not dropped markers: one pin per thrall, created once and
    /// then dragged along behind it every frame. They are added with save:false, so they
    /// never end up written into the map data and there is nothing left behind to clean
    /// up if the mod goes away. It is the same thing Valheim does for other players.
    /// </summary>
    internal static class ThrallMap
    {
        private static readonly Dictionary<Thrall, Minimap.PinData> Pins =
            new Dictionary<Thrall, Minimap.PinData>();

        private static readonly List<Thrall> Stale = new List<Thrall>();

        private static bool _shown;

        /// <summary>
        /// The map only redraws its pins when this is set. Moving a pin without it leaves
        /// the marker painted where it was, which is exactly the stuck pin we do not want.
        /// </summary>
        private static readonly AccessTools.FieldRef<Minimap, bool> DirtyRef =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");

        public static bool Shown { get { return _shown; } }

        public static void SetShown(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            if (!_shown) Clear();
        }

        public static void Update()
        {
            var map = Minimap.instance;
            if (map == null) return;

            if (!_shown)
            {
                if (Pins.Count > 0) Clear();
                return;
            }

            var moved = false;

            // Thralls that have died, been dismissed or unloaded lose their pin. Unity
            // objects compare equal to null once destroyed, which is what catches them.
            Stale.Clear();
            foreach (var entry in Pins)
                if (entry.Key == null) Stale.Add(entry.Key);

            for (int i = 0; i < Stale.Count; i++)
            {
                map.RemovePin(Pins[Stale[i]]);
                Pins.Remove(Stale[i]);
                moved = true;
            }

            var type = PinType();

            foreach (var thrall in ThrallRegistry.All)
            {
                if (thrall == null) continue;

                Minimap.PinData pin;
                if (!Pins.TryGetValue(thrall, out pin))
                {
                    pin = map.AddPin(thrall.transform.position, type, Label(thrall),
                        false, false, 0L);
                    Pins[thrall] = pin;
                    moved = true;
                }

                var here = thrall.transform.position;
                if (pin.m_pos != here)
                {
                    pin.m_pos = here;
                    moved = true;
                }

                var label = Label(thrall);
                if (pin.m_name != label)
                {
                    pin.m_name = label;
                    moved = true;
                }
            }

            if (moved) MarkDirty(map);
        }

        /// <summary>What the marker says, if anything. Names on a busy map are clutter.</summary>
        private static string Label(Thrall thrall)
        {
            if (!ThrallConfig.MapPinLabels.Value) return "";

            return thrall.ThrallName + " (" + WorkNode.JobName(thrall.Job) + ")";
        }

        private static Minimap.PinType PinType()
        {
            try
            {
                return (Minimap.PinType)System.Enum.Parse(
                    typeof(Minimap.PinType), ThrallConfig.MapPinType.Value.Trim(), true);
            }
            catch
            {
                return Minimap.PinType.Icon3;
            }
        }

        private static void MarkDirty(Minimap map)
        {
            try { DirtyRef(map) = true; }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not ask the map to redraw: " + e.Message);
            }
        }

        public static void Clear()
        {
            var map = Minimap.instance;

            foreach (var entry in Pins)
            {
                if (map != null && entry.Value != null) map.RemovePin(entry.Value);
            }
            Pins.Clear();

            if (map != null) MarkDirty(map);
        }
    }
}
