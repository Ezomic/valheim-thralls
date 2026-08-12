using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// A piece you build beside the altar to open the next breed.
    ///
    /// Modelled on how the game gates its own stations: a workbench does not level up
    /// from a menu, you put a chopping block next to it. Each of these carries the level
    /// it grants, and the altar counts whichever ones are standing near it.
    /// </summary>
    internal class AltarUpgrade : MonoBehaviour
    {
        public int Level = 1;

        private static readonly System.Collections.Generic.List<AltarUpgrade> Standing =
            new System.Collections.Generic.List<AltarUpgrade>();

        private void OnEnable()
        {
            if (!Standing.Contains(this)) Standing.Add(this);
        }

        private void OnDisable()
        {
            Standing.Remove(this);
        }

        /// <summary>
        /// The highest upgrade standing within range of a point, and nothing above a gap:
        /// building the third without the first two leaves you at zero, so the chain has
        /// to be built in order.
        /// </summary>
        public static int LevelNear(Vector3 point, float range)
        {
            Standing.RemoveAll(u => u == null);

            var have = new bool[5];

            for (int i = 0; i < Standing.Count; i++)
            {
                var upgrade = Standing[i];
                if (upgrade == null) continue;
                if (Vector3.Distance(point, upgrade.transform.position) > range) continue;

                var level = Mathf.Clamp(upgrade.Level, 1, 4);
                have[level] = true;
            }

            var reached = 0;
            for (int level = 1; level <= 4; level++)
            {
                if (!have[level]) break;
                reached = level;
            }

            return reached;
        }
    }
}
