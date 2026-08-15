using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>A thrall you sent away, kept whole so it can be called back as itself.</summary>
    internal class RestingThrall
    {
        public string Name;
        public int Tier;
        public float Xp;
        public string Tool;

        public string TierName { get { return ThrallBreed.NameFor(Tier); } }
        public int Level { get { return Levels.LevelFor(Xp); } }
    }

    /// <summary>
    /// The thralls that are not currently standing in the world.
    ///
    /// Dismissing used to mean releasing: the creature was gone and everything it had
    /// learned went with it. This keeps the name, the breed, the experience and the tool
    /// on the altar's ZDO, so sending one away is putting it down rather than throwing it
    /// out - which is what makes a work slot limit bearable when you own twenty of them.
    /// </summary>
    internal static class Resting
    {
        public const string ZKey = "thrallResting";

        private static readonly List<RestingThrall> Roll = new List<RestingThrall>();
        private static ThrallAltar _boundTo;

        public static IList<RestingThrall> All { get { return Roll; } }
        public static int Count { get { return Roll.Count; } }

        private static ZNetView Ledger()
        {
            var altar = ThrallAltar.Current;
            return altar != null && altar.Usable ? altar.View : null;
        }

        /// <summary>
        /// Whether there is an altar to keep the roll on.
        ///
        /// Asked before sending a thrall to rest, because without one there is nowhere to
        /// write it down and "rest" quietly becomes "gone". The panel says which of the two
        /// it is rather than reporting the same cheerful line either way.
        /// </summary>
        public static bool HasLedger { get { return Ledger() != null; } }

        public static void Tick()
        {
            if (ThrallAltar.Current == _boundTo) return;
            _boundTo = ThrallAltar.Current;
            Load();
        }

        private static void Load()
        {
            Roll.Clear();

            var nview = Ledger();
            if (nview == null) return;

            var blob = nview.GetZDO().GetByteArray(ZKey, null);
            if (blob == null || blob.Length == 0) return;

            try
            {
                var pkg = new ZPackage();
                pkg = new ZPackage(blob);

                var count = pkg.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    Roll.Add(new RestingThrall
                    {
                        Name = pkg.ReadString(),
                        Tier = pkg.ReadInt(),
                        Xp = pkg.ReadSingle(),
                        Tool = pkg.ReadString()
                    });
                }
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not read the resting roll: " + e.Message);
                Roll.Clear();
            }
        }

        private static void Save()
        {
            var nview = Ledger();
            if (nview == null) return;

            nview.ClaimOwnership();

            var pkg = new ZPackage();
            pkg.Write(Roll.Count);

            for (int i = 0; i < Roll.Count; i++)
            {
                pkg.Write(Roll[i].Name ?? "Thrall");
                pkg.Write(Roll[i].Tier);
                pkg.Write(Roll[i].Xp);
                pkg.Write(Roll[i].Tool ?? "");
            }

            nview.GetZDO().Set(ZKey, pkg.GetArray());
        }

        /// <summary>Puts a thrall down. Returns false when there is no altar to keep it.</summary>
        public static bool Rest(string name, int tier, float xp, string tool)
        {
            if (Ledger() == null) return false;

            Roll.Add(new RestingThrall
            {
                Name = name ?? "Thrall",
                Tier = ThrallBreed.Clamp(tier),
                Xp = Mathf.Max(0f, xp),
                Tool = tool ?? ""
            });

            Save();
            return true;
        }

        public static void Remove(RestingThrall entry)
        {
            if (entry == null) return;

            Roll.Remove(entry);
            Save();
        }
    }
}
