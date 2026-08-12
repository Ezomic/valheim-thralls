using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>A thrall that died, remembered well enough to be brought back.</summary>
    internal class FallenThrall
    {
        public string Name;
        public int Tier;
        public int Level;
        public Vector3 Where;

        public string TierName { get { return ThrallBreed.NameFor(Tier); } }
    }

    /// <summary>
    /// The steward's roll of the dead. Kept on the steward's ZDO next to the build ledger,
    /// so it belongs to the world and cannot be lost with the body.
    /// </summary>
    internal static class Fallen
    {
        public const string ZKey = "thrallFallen";

        private static readonly List<FallenThrall> Roll = new List<FallenThrall>();
        private static ThrallAltar _boundTo;

        public static IList<FallenThrall> All { get { return Roll; } }
        public static int Count { get { return Roll.Count; } }

        private static ZNetView Ledger()
        {
            var altar = ThrallAltar.Current;
            return altar != null && altar.Usable ? altar.View : null;
        }

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
                var pkg = new ZPackage(blob);
                var count = pkg.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    Roll.Add(new FallenThrall
                    {
                        Name = pkg.ReadString(),
                        Tier = pkg.ReadInt(),
                        Level = pkg.ReadInt(),
                        Where = pkg.ReadVector3()
                    });
                }
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not read the roll of the dead: " + e.Message);
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
                pkg.Write(Roll[i].Level);
                pkg.Write(Roll[i].Where);
            }
            nview.GetZDO().Set(ZKey, pkg.GetArray());
        }

        public static void Record(string name, int tier, int level, Vector3 where)
        {
            // Without a steward there is nobody keeping the roll, so the death is simply final.
            if (Ledger() == null) return;

            Roll.Add(new FallenThrall
            {
                Name = name ?? "Thrall",
                Tier = ThrallBreed.Clamp(tier),
                Level = Mathf.Max(1, level),
                Where = where
            });
            Save();
        }

        public static void Remove(FallenThrall entry)
        {
            if (entry == null) return;
            Roll.Remove(entry);
            Save();
        }
    }
}
