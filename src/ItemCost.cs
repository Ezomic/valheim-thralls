using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Costs written as "PrefabName:Amount, PrefabName:Amount". Item lookups go through
    /// ObjectDB so the config can name prefabs while the inventory is matched on the
    /// shared name it actually stores.
    /// </summary>
    internal static class ItemCost
    {
        public static List<KeyValuePair<string, int>> Parse(string spec)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrEmpty(spec)) return result;

            foreach (var part in spec.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                var split = trimmed.Split(':');
                if (split.Length != 2) continue;

                int amount;
                if (!int.TryParse(split[1].Trim(), out amount) || amount <= 0) continue;

                result.Add(new KeyValuePair<string, int>(split[0].Trim(), amount));
            }
            return result;
        }

        public static string SharedName(string prefabName)
        {
            if (ObjectDB.instance == null) return null;
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab == null) return null;

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return null;
            return drop.m_itemData.m_shared.m_name;
        }

        /// <summary>Human readable "10x GreydwarfEye, 5x Wood" for whatever is still missing.</summary>
        public static string Missing(Inventory inventory, string spec)
        {
            var missing = new StringBuilder();
            foreach (var entry in Parse(spec))
            {
                var shared = SharedName(entry.Key);
                if (shared == null) continue;

                var have = inventory.CountItems(shared, -1, true);
                if (have >= entry.Value) continue;

                if (missing.Length > 0) missing.Append(", ");
                missing.Append((entry.Value - have) + "x " + entry.Key);
            }
            return missing.ToString();
        }

        public static bool CanPay(Inventory inventory, string spec)
        {
            return inventory != null && Missing(inventory, spec).Length == 0;
        }

        public static bool Pay(Inventory inventory, string spec)
        {
            if (!CanPay(inventory, spec)) return false;

            foreach (var entry in Parse(spec))
            {
                var shared = SharedName(entry.Key);
                if (shared == null) continue;
                inventory.RemoveItem(shared, entry.Value, -1, true);
            }
            return true;
        }

        /// <summary>Short label for a button, e.g. "20x GreydwarfEye".</summary>
        public static string Describe(string spec)
        {
            var sb = new StringBuilder();
            foreach (var entry in Parse(spec))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(entry.Value + "x " + entry.Key);
            }
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }
    }
}
