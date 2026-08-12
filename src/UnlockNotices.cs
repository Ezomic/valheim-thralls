using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The cards the game shows when something new becomes available to you.
    ///
    /// Valheim raises one whenever you learn a piece or a recipe, and a player reads those
    /// as the game telling them their options have changed. Nothing in this mod raised any,
    /// so the altar and its upgrades simply appeared in the hammer with no announcement,
    /// and the breeds each upgrade opens were never mentioned at all - you had to open the
    /// panel and notice a card had stopped being greyed out.
    /// </summary>
    [HarmonyPatch]
    internal static class UnlockNotices
    {
        /// <summary>
        /// Building an upgrade opens the next breed, so say so at the moment it is built.
        ///
        /// The check runs after the piece is placed rather than predicting from the piece's
        /// own level, because the chain has to be unbroken: laying the third upgrade with
        /// the first two missing grants nothing, and announcing berserkers there would be a
        /// lie. LevelNear is asked what the altar actually reaches now.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static void AnnounceBreed(Piece piece, Vector3 pos)
        {
            if (piece == null || Player.m_localPlayer == null) return;
            if (piece.GetComponent<AltarUpgrade>() == null) return;

            // Instantiate runs OnEnable synchronously, so the new upgrade has already
            // registered itself by the time this runs and LevelNear counts it.
            var level = AltarUpgrade.LevelNear(pos, Mathf.Max(2f, ThrallConfig.SlotSearchRange.Value));
            if (level <= 0) return;

            var tier = level + 1;
            if (tier > ThrallBreed.Count) return;

            var breed = ThrallBreed.NameFor(tier);

            // The upgrade is only half the gate - the biome's boss still has to be down.
            // Saying "you can now bind golems" while Bonemass stands would be worse than
            // saying nothing, so a blocked breed gets told what is still in the way.
            var blocker = ThrallBreed.Blocker(tier);
            if (!string.IsNullOrEmpty(blocker))
            {
                ThrallsPlugin.Say(breed + " thralls: " + blocker);
                return;
            }

            if (MessageHud.instance != null)
                MessageHud.instance.QueueUnlockMsg(piece.m_icon, "New thralls",
                    breed + " can now be bound at the altar");
            else
                ThrallsPlugin.Say(breed + " can now be bound at the altar");

            ThrallsPlugin.Log.LogInfo("Announced breed unlock: " + breed + " (upgrade " + level + ")");
        }
    }
}
