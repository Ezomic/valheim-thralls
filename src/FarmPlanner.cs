using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>A seed the thralls know how to sow, and the plant it turns into.</summary>
    internal class PlantRecipe
    {
        public GameObject PlantPrefab;
        public Plant Plant;
        public string SeedPrefabName;
        public string SeedSharedName;
        public int SeedAmount = 1;

        public string DisplayName
        {
            get { return Plant != null ? Plant.m_name : PlantPrefab.name; }
        }
    }

    /// <summary>
    /// Works out what can be sown and where. The catalogue is read straight off the
    /// cultivator's piece table, so anything a mod adds to the cultivator is picked up too.
    /// </summary>
    internal static class FarmPlanner
    {
        private static List<PlantRecipe> _catalog;
        private static int _roofMask;
        private static int _spaceMask;
        private static int _groundMask;

        public static List<PlantRecipe> Catalog
        {
            get
            {
                if (_catalog == null) Build();
                return _catalog;
            }
        }

        public static void Invalidate() { _catalog = null; }

        private static void Build()
        {
            _catalog = new List<PlantRecipe>();
            if (ObjectDB.instance == null) return;

            var cultivator = ObjectDB.instance.GetItemPrefab("Cultivator");
            if (cultivator == null)
            {
                ThrallsPlugin.Log.LogWarning("No Cultivator in ObjectDB; thralls cannot sow anything.");
                return;
            }

            var drop = cultivator.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            if (table == null || table.m_pieces == null) return;

            foreach (var pieceGo in table.m_pieces)
            {
                if (pieceGo == null) continue;

                var plant = pieceGo.GetComponentInChildren<Plant>();
                if (plant == null) continue;

                var piece = pieceGo.GetComponent<Piece>();
                if (piece == null || piece.m_resources == null || piece.m_resources.Length == 0) continue;

                var req = piece.m_resources[0];
                if (req == null || req.m_resItem == null || req.m_resItem.m_itemData == null) continue;

                _catalog.Add(new PlantRecipe
                {
                    PlantPrefab = pieceGo,
                    Plant = plant,
                    SeedPrefabName = req.m_resItem.gameObject.name,
                    SeedSharedName = req.m_resItem.m_itemData.m_shared.m_name,
                    SeedAmount = Mathf.Max(1, req.m_amount)
                });
            }

            ThrallsPlugin.Log.LogInfo("Thralls can sow " + _catalog.Count + " kinds of plant.");
        }

        public static bool IsSeed(ItemDrop.ItemData item)
        {
            return RecipeFor(item) != null;
        }

        public static PlantRecipe RecipeFor(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;
            var list = Catalog;
            for (int i = 0; i < list.Count; i++)
                if (list[i].SeedSharedName == item.m_shared.m_name) return list[i];
            return null;
        }

        // ---------------------------------------------------------------- placement

        /// <summary>
        /// Mirrors the checks Plant runs on itself, so a thrall only sows where the crop
        /// will actually take: cultivated soil, right biome, open sky, no crowding.
        /// </summary>
        public static bool CanPlantAt(PlantRecipe recipe, Vector3 pos)
        {
            if (recipe == null || recipe.Plant == null) return false;

            var heightmap = Heightmap.FindHeightmap(pos);
            if (heightmap == null) return false;

            if (recipe.Plant.m_needCultivatedGround && !heightmap.IsCultivated(pos)) return false;

            var biome = heightmap.GetBiome(pos);
            if ((biome & recipe.Plant.m_biome) == 0) return false;

            if (_roofMask == 0) _roofMask = LayerMask.GetMask("Default", "static_solid", "piece");
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.up, 100f, _roofMask)) return false;

            if (_spaceMask == 0)
                _spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");

            var crowd = Physics.OverlapSphere(pos, recipe.Plant.m_growRadius, _spaceMask);
            if (crowd.Length > 0) return false;

            return true;
        }

        /// <summary>Nearest free patch of soil to <paramref name="anchor"/>, searched ring by ring.</summary>
        public static bool FindSpot(PlantRecipe recipe, Vector3 anchor, float radius, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (recipe == null || recipe.Plant == null) return false;

            var step = Mathf.Max(0.6f, recipe.Plant.m_growRadius * 2f + 0.1f);
            var rings = Mathf.Clamp(Mathf.CeilToInt(radius / step), 1, 24);
            var tested = 0;

            for (int ring = 0; ring <= rings; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int z = -ring; z <= ring; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                        if (++tested > 400) return false;

                        var candidate = anchor + new Vector3(x * step, 0f, z * step);
                        if (Vector3.Distance(anchor, candidate) > radius) continue;

                        Vector3 grounded;
                        if (!GroundAt(candidate, out grounded)) continue;
                        if (!CanPlantAt(recipe, grounded)) continue;

                        spot = grounded;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool GroundAt(Vector3 near, out Vector3 grounded)
        {
            if (_groundMask == 0) _groundMask = LayerMask.GetMask("terrain", "static_solid", "Default");

            RaycastHit hit;
            if (Physics.Raycast(near + Vector3.up * 5f, Vector3.down, out hit, 12f, _groundMask))
            {
                grounded = hit.point;
                return true;
            }
            grounded = near;
            return false;
        }

        public static GameObject Sow(PlantRecipe recipe, Vector3 pos, long creatorId)
        {
            if (recipe == null || recipe.PlantPrefab == null) return null;

            var rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            var go = UnityEngine.Object.Instantiate(recipe.PlantPrefab, pos, rotation);

            var piece = go.GetComponent<Piece>();
            if (piece != null) piece.SetCreator(creatorId);

            return go;
        }
    }
}
