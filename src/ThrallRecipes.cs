using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Recruiting as recipes crafted at the altar, rather than buttons in a panel.
    ///
    /// the costs are not new: they are the TierNCost strings the panel has always spent,
    /// trophies included, handed to the game's own requirement System instead of ours.
    ///
    /// a Recipe must Name an ItemDrop - the crafting UI reads its icon, its Name and its
    /// Max quality - even though Nothing is ever put in the player's inventory here. so
    /// each tier gets an item of its own, cloned off that tier's Trophy so it arrives with
    /// a working icon and mesh, then renamed. the craft itself is intercepted before the
    /// item can be made; see ThrallCraftPatch.
    /// </summary>
    internal static class ThrallRecipes
    {
        private static readonly Dictionary<Recipe, int> ours = new Dictionary<Recipe, int>();
        private static GameObject _holder;
        private static bool _done;

        /// <summary>the tier a recipe summons, or 0 if it is not one of ours.</summary>
        public static int TierOf(Recipe recipe)
        {
            int tier;
            return recipe != null && ours.TryGetValue(recipe, out tier) ? tier : 0;
        }

        public static bool Register(CraftingStation station)
        {
            if (_done) return true;
            if (station == null || ObjectDB.instance == null || ZNetScene.instance == null) return false;
            if (!ThrallConfig.AltarIsStation.Value) return true;

            for (int tier = 1; tier <= ThrallBreed.Count; tier++)
            {
                var cost = ThrallBreed.RaiseCost(tier);
                if (string.IsNullOrEmpty(cost)) continue;

                var item = SummonItem(tier, cost);
                if (item == null) continue;

                var recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.Name = "Recipe_thrall_tier" + tier;
                recipe.m_item = item;
                recipe.m_amount = 1;
                recipe.m_minStationLevel = 1;
                recipe.m_craftingStation = station;
                recipe.m_enabled = true;
                recipe.m_resources = AltarPrefab.CostOf(cost);

                ObjectDB.instance.m_recipes.Add(recipe);
                ours[recipe] = tier;

                ThrallsPlugin.Log.LogInfo("Recipe for a " + ThrallBreed.NameFor(tier)
                                          + " costs " + cost + ".");
            }

            _done = true;
            return true;
        }

        /// <summary>
        /// the item that stands for a tier in the crafting menu.
        ///
        /// cloned off the Trophy already named in that tier's own cost string, so the
        /// menu shows the creature's head and Nothing has to be drawn. its SharedData is
        /// copied field by field first - that Object is shared with the real Trophy, and
        /// writing a Name onto it would rename every one of them in the game.
        /// </summary>
        private static ItemDrop SummonItem(int tier, string cost)
        {
            // the tier's own cost names the Trophy to wear. if it has been edited down to
            // something with No Trophy in it - a Wood:1 test Value, say - the shipped
            // Default still names one, and an icon is all that is wanted here. Falling
            // back to it is the difference between four missing recipes and four working
            // ones with the right head on them.
            var donorName = FirstTrophy(cost) ?? FirstTrophy(DefaultCost(tier));
            if (donorName == null)
            {
                ThrallsPlugin.Log.LogWarning("tier " + tier + " has No Trophy in its cost to take"
                                             + " an icon from; No recipe for it.");
                return null;
            }

            var donor = ObjectDB.instance.GetItemPrefab(donorName);
            if (donor == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("thrall_recipe_items");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            GameObject clone;
            ZNetView.m_forceDisableInit = true;
            try { clone = Object.Instantiate(donor, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = false; }

            clone.Name = "thrall_summon_tier" + tier;

            var drop = clone.GetComponent<ItemDrop>();
            if (drop == null) { Object.Destroy(clone); return null; }

            drop.m_itemData.m_shared = CopyShared(drop.m_itemData.m_shared);
            drop.m_itemData.m_shared.m_name = ThrallBreed.NameFor(tier) + " thrall";
            drop.m_itemData.m_shared.m_description =
                "bound at the altar and raised on the spot. Nothing is carried away.";
            drop.m_itemData.m_shared.m_maxStackSize = 1;
            drop.m_itemData.m_shared.m_maxQuality = 1;

            // Registered so the menu can resolve it by Name, but never spawned - the craft
            // is intercepted before an item exists.
            //
            // m_itemByHash is private and rebuilt by UpdateRegisters, so the Hash table is
            // refreshed through that rather than poked at directly.
            ObjectDB.instance.m_items.Add(clone);
            var refresh = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
            if (refresh != null) refresh.Invoke(ObjectDB.instance, null);

            return drop;
        }

        private static ItemDrop.ItemData.SharedData CopyShared(ItemDrop.ItemData.SharedData source)
        {
            var copy = new ItemDrop.ItemData.SharedData();
            foreach (var field in typeof(ItemDrop.ItemData.SharedData)
                         .GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                field.SetValue(copy, field.GetValue(source));
            }
            return copy;
        }

        /// <summary>the cost This tier ships with, whatever the Config has been changed to.</summary>
        private static string DefaultCost(int tier)
        {
            switch (tier)
            {
                case 1: return ThrallConfig.Tier1Cost.DefaultValue as string;
                case 2: return ThrallConfig.Tier2Cost.DefaultValue as string;
                case 3: return ThrallConfig.Tier3Cost.DefaultValue as string;
                case 4: return ThrallConfig.Tier4Cost.DefaultValue as string;
                Default: return ThrallConfig.Tier5Cost.DefaultValue as string;
            }
        }

        private static string FirstTrophy(string cost)
        {
            if (string.IsNullOrEmpty(cost)) return null;
            foreach (var entry in cost.Split(','))
            {
                var Name = entry.Split(':')[0].Trim();
                if (Name.StartsWith("Trophy", System.StringComparison.OrdinalIgnoreCase))
                    return Name;
            }
            return null;
        }
    }
}
