using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The four kinds of thrall you can bind. These are separate careers, not a ladder -
    /// a brute never becomes a golem. Each is hired with heads from its own biome and then
    /// levelled up on its own.
    /// </summary>
    internal static class ThrallBreed
    {
        // Five careers, not four rungs of one: a brute never becomes a seeker.
        public const int Count = 5;

        public static string PrefabFor(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return ThrallConfig.Tier1Prefab.Value;
                case 2: return ThrallConfig.Tier2Prefab.Value;
                case 3: return ThrallConfig.Tier3Prefab.Value;
                case 4: return ThrallConfig.Tier4Prefab.Value;
                default: return ThrallConfig.Tier5Prefab.Value;
            }
        }

        public static string NameFor(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return "Brute";
                case 2: return "Draugr";
                case 3: return "Golem";
                case 4: return "Berserker";
                default: return "Seeker";
            }
        }

        /// <summary>World key gating this tier - each kind answers only once its biome's boss is down.</summary>
        public static string RequiredKey(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return ThrallConfig.Tier1Key.Value;
                case 2: return ThrallConfig.Tier2Key.Value;
                case 3: return ThrallConfig.Tier3Key.Value;
                case 4: return ThrallConfig.Tier4Key.Value;
                default: return ThrallConfig.Tier5Key.Value;
            }
        }

        public static bool Unlocked(int tier)
        {
            // The altar has to have been built up far enough. Tier one needs nothing;
            // each tier above it wants one more upgrade on the altar.
            if (ThrallConfig.UpgradesGateTiers.Value
                && ThrallAltar.Level < Clamp(tier) - 1) return false;

            var key = RequiredKey(tier);
            if (string.IsNullOrEmpty(key)) return true;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);
        }

        /// <summary>What is standing between you and this breed, for the card to say.</summary>
        public static string Blocker(int tier)
        {
            tier = Clamp(tier);

            if (ThrallConfig.UpgradesGateTiers.Value && ThrallAltar.Level < tier - 1)
                return "needs altar upgrade " + (tier - 1);

            var key = RequiredKey(tier);
            if (!string.IsNullOrEmpty(key)
                && (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(key)))
                return BossName(tier) + " must fall";

            return "";
        }

        /// <summary>
        /// The boss standing in the way of this breed - the one that rules its own biome.
        ///
        /// Kept in step with the TierNRequiresBoss defaults by hand. If those are changed
        /// in the config this goes on naming the default boss, which is the lesser of two
        /// evils: the alternative is mapping arbitrary world keys back to display names and
        /// getting "defeated_gdking must fall" on screen when somebody types one in.
        /// </summary>
        public static string BossName(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return "The Elder";
                case 2: return "Bonemass";
                case 3: return "Moder";
                case 4: return "Yagluth";
                default: return "The Queen";
            }
        }

        /// <summary>What it costs to raise this one from the dead, in biome goods.</summary>
        /// <summary>What raising one of this breed costs, as PrefabName:Amount pairs.</summary>
        public static string RaiseCost(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return ThrallConfig.Tier1Cost.Value;
                case 2: return ThrallConfig.Tier2Cost.Value;
                case 3: return ThrallConfig.Tier3Cost.Value;
                case 4: return ThrallConfig.Tier4Cost.Value;
                default: return ThrallConfig.Tier5Cost.Value;
            }
        }

        public static string ResurrectCost(int tier)
        {
            switch (Clamp(tier))
            {
                case 1: return ThrallConfig.Tier1Revive.Value;
                case 2: return ThrallConfig.Tier2Revive.Value;
                case 3: return ThrallConfig.Tier3Revive.Value;
                case 4: return ThrallConfig.Tier4Revive.Value;
                default: return ThrallConfig.Tier5Revive.Value;
            }
        }

        public static int Clamp(int tier)
        {
            return Mathf.Clamp(tier, 1, Count);
        }

        /// <summary>Bigger bodies need more room to swing, or they stall out of reach.</summary>
        /// <summary>What the altar has to say about each breed, which is not much.</summary>
        public static string Lore(int tier)
        {
            switch (Clamp(tier))
            {
                case 1:
                    return "Bound from the biggest greydwarf anyone could catch. Works "
                           + "without complaint, mostly because it has never learned to "
                           + "complain. Carries about as much as its own hands can hold "
                           + "and will put a tree through a wall if the wall is closer.";

                case 2:
                    return "A swamp elite, raised damp and kept that way. Tireless, "
                           + "joyless, and faintly unpleasant to stand downwind of. "
                           + "Sturdier than the brute and considerably better at "
                           + "remembering where the chest is.";

                case 3:
                    return "A mountain golem, woken up and given a job. Slow to start "
                           + "and slow to stop, but rock does not tire and does not "
                           + "argue. Needs no axe: it walks at a tree and the tree "
                           + "stops being there. Very little of it is wood afterwards.";

                case 4:
                    return "A fuling berserker, still holding both clubs. Nobody has "
                           + "explained the work to it; it simply follows and hits what "
                           + "it is pointed at until that thing is gone. Bring more "
                           + "trees.";

                default:
                    return "A seeker brute out of the mist, bound while it was still "
                           + "twitching. Does not tire, does not sleep and does not "
                           + "appear to need the light. Handles a full load without "
                           + "noticing it, and puts everyone else off their work.";
            }
        }

        /// <summary>
        /// How many pack slots a thrall of this tier and level carries.
        ///
        /// A greydwarf brute hauls one slot and every tier above it carries one more, so a
        /// berserker manages four. Levels add to that on top, which is what makes an old
        /// thrall worth keeping rather than replacing with a fresh one of a higher tier.
        /// </summary>
        public static int PackSlots(int tier, int level)
        {
            var slots = Mathf.Max(1, ThrallConfig.PackBaseSlots.Value)
                        + (Clamp(tier) - 1) * Mathf.Max(0, ThrallConfig.PackPerTier.Value);

            var per = Mathf.Max(1, ThrallConfig.PackLevelsPerSlot.Value);
            slots += Mathf.Max(1, level) / per;

            return Mathf.Clamp(slots, 1, 64);
        }

        public static float ReachBonus(int tier)
        {
            return (Clamp(tier) - 1) * 0.75f;
        }

        /// <summary>
        /// Whether this breed knocks trees down rather than cutting them.
        ///
        /// A stone golem holding a flint axe was always a slightly silly picture, and
        /// making it carry one to be useful was worse: the thing is two metres of rock and
        /// the tree is in its way. A smasher needs no axe and fells anything its tool tier
        /// covers - and gets almost nothing back for it, because what it leaves is
        /// splinters. That is the trade: it is a way to clear ground quickly, not a way to
        /// get wood, and the two would be the same feature without the loss.
        /// </summary>
        public static bool Smashes(int tier)
        {
            var wanted = Clamp(tier);

            foreach (var entry in (ThrallConfig.SmashTiers.Value ?? "").Split(','))
            {
                int listed;
                if (int.TryParse(entry.Trim(), out listed) && listed == wanted) return true;
            }
            return false;
        }

        /// <summary>
        /// The share of a smashed tree that survives being smashed, 0 to 1.
        ///
        /// Applied to what the thrall picks up rather than to the tree's own drop table:
        /// the table belongs to the game and is shared with every other way a tree can
        /// fall, so reaching into it would quietly change what a player gets for their own
        /// axe swing too.
        /// </summary>
        public static float SmashYield
        {
            get { return Mathf.Clamp01(ThrallConfig.SmashYield.Value); }
        }
    }
}
