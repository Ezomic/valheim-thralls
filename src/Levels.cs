using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Experience is the single source of truth for a thrall's level - there is no separate
    /// stored rank to drift out of step with it.
    /// </summary>
    internal static class Levels
    {
        private static List<float> _thresholds;

        public static void Invalidate() { _thresholds = null; }

        /// <summary>Experience needed to reach level 2, 3, 4 ... in order.</summary>
        public static List<float> Thresholds
        {
            get
            {
                if (_thresholds != null) return _thresholds;

                _thresholds = new List<float>();
                var raw = ThrallConfig.LevelThresholds.Value ?? "";

                foreach (var part in raw.Split(','))
                {
                    float value;
                    if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0f)
                        _thresholds.Add(value);
                }

                _thresholds.Sort();
                return _thresholds;
            }
        }

        public static int MaxLevel
        {
            get { return Mathf.Min(Thresholds.Count + 1, Mathf.Max(1, ThrallConfig.MaxLevel.Value)); }
        }

        public static int LevelFor(float xp)
        {
            var level = 1;
            for (int i = 0; i < Thresholds.Count; i++)
                if (xp >= Thresholds[i]) level = i + 2;

            return Mathf.Clamp(level, 1, MaxLevel);
        }

        /// <summary>Experience at which a level begins, used to seed a raised thrall.</summary>
        public static float XpFor(int level)
        {
            if (level <= 1 || Thresholds.Count == 0) return 0f;
            var index = Mathf.Clamp(level - 2, 0, Thresholds.Count - 1);
            return Thresholds[index];
        }

        /// <summary>Experience needed for the next level, or 0 when there is nothing left to earn.</summary>
        public static float NextAt(float xp)
        {
            var level = LevelFor(xp);
            if (level >= MaxLevel) return 0f;
            return XpFor(level + 1);
        }

        /// <summary>
        /// How far through the current level, from 0 to 1, for drawing a bar. Measured
        /// from the floor of this level rather than from zero, or every bar past level one
        /// would start almost full.
        /// </summary>
        public static float Fraction(float xp)
        {
            var next = NextAt(xp);
            if (next <= 0f) return 1f;

            var floor = XpFor(LevelFor(xp));
            var span = next - floor;
            if (span <= 0f) return 1f;

            return Mathf.Clamp01((xp - floor) / span);
        }

        /// <summary>"140/450" style progress, or "max" when done.</summary>
        public static string Progress(float xp)
        {
            var next = NextAt(xp);
            if (next <= 0f) return "max";

            var floor = XpFor(LevelFor(xp));
            return Mathf.FloorToInt(xp - floor) + "/" + Mathf.FloorToInt(next - floor);
        }
    }
}
