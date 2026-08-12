using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Heads are the currency. Which head decides how much it is worth: a greydwarf skull
    /// buys labour, a seeker's buys a better class of labourer.
    /// </summary>
    internal static class Trophies
    {
        private static Dictionary<string, int> _tiers;

        public static void Invalidate() { _tiers = null; }

        private static Dictionary<string, int> Tiers
        {
            get
            {
                if (_tiers == null) Build();
                return _tiers;
            }
        }

        private static void Build()
        {
            _tiers = new Dictionary<string, int>();
            Add(ThrallConfig.Tier2Trophies.Value, 2);
            Add(ThrallConfig.Tier3Trophies.Value, 3);
            Add(ThrallConfig.Tier4Trophies.Value, 4);
        }

        private static void Add(string csv, int tier)
        {
            if (string.IsNullOrEmpty(csv)) return;
            foreach (var part in csv.Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) _tiers[name.ToLowerInvariant()] = tier;
            }
        }

        public static bool IsTrophy(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null
                   && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }

        /// <summary>Tier of a head, or 0 if it is not a head at all. Anything unlisted is tier 1.</summary>
        public static int TierOf(ItemDrop.ItemData item)
        {
            if (!IsTrophy(item)) return 0;

            var prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            if (prefab != null)
            {
                int tier;
                if (Tiers.TryGetValue(prefab.ToLowerInvariant(), out tier)) return tier;
            }
            return 1;
        }

        public static int Count(Inventory inventory, int minTier)
        {
            if (inventory == null) return 0;

            var total = 0;
            var items = inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var tier = TierOf(items[i]);
                if (tier >= minTier && tier > 0) total += items[i].m_stack;
            }
            return total;
        }

        /// <summary>Takes the cheapest acceptable heads first, so good trophies are not wasted.</summary>
        public static bool Consume(Inventory inventory, int minTier, int amount)
        {
            if (inventory == null || amount <= 0) return false;
            if (Count(inventory, minTier) < amount) return false;

            var remaining = amount;
            for (int tier = minTier; tier <= 4 && remaining > 0; tier++)
            {
                var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
                foreach (var item in items)
                {
                    if (remaining <= 0) break;
                    if (TierOf(item) != tier) continue;

                    var take = Mathf.Min(remaining, item.m_stack);
                    inventory.RemoveItem(item, take);
                    remaining -= take;
                }
            }
            return remaining == 0;
        }
    }
}
