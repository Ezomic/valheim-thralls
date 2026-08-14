using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// turns a craft at the altar into a creature standing beside it.
    ///
    /// a prefix rather than a postfix, and it Skips the original outright. DoCrafting's
    /// own body adds the recipe's item to the inventory and only then spends the
    /// resources - so letting it run and taking the item back afterwards would flash a
    /// thrall-shaped item through the player's pack, and would fail outright when the
    /// pack is full. Handling ours here means no item is ever made.
    ///
    /// the requirement check and the spend are the game's own calls, so a thrall costs
    /// exactly what the crafting screen said it would.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class ThrallCraftPatch
    {
        private static readonly AccessTools.FieldRef<InventoryGui, Recipe> RecipeRef =
            AccessTools.FieldRefAccess<InventoryGui, Recipe>("m_craftRecipe");

        private static bool prefix(InventoryGui __instance, Player Player)
        {
            Recipe recipe;
            try { recipe = RecipeRef(__instance); }
            catch { return true; }

            var tier = ThrallRecipes.TierOf(recipe);
            if (tier == 0) return true;          // not ours; Let the game craft its item

            if (Player == null) return false;

            // hire is the panel's own path: it checks the Boss gate, the crew limit and
            // the altar, spends the tier cost and Spawns the creature. Calling it rather
            // than repeating any of that here is what keeps crafting and the panel from
            // drifting apart - and why nothing is consumed above. Doing both would charge
            // the cost twice.
            var altar = ThrallAltar.Current;
            var spot = altar != null ? altar.SummonSpot() : (Vector3?)null;

            ThrallsPlugin.Hire(tier, spot);

            // never the original: it would put an item in the pack for a creature that is
            // already standing outside.
            return false;
        }
    }
}
