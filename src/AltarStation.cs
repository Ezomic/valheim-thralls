using System.Reflection;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// turns the altar into a real CraftingStation, and its upgrades into real
    /// StationExtensions.
    ///
    /// binding a creature is a kind of crafting, so the altar may as well be the thing
    /// the game already has for that. Everything the upgrades needed hand-writing for -
    /// the connection line, the "Requires" line in the Build menu, the range circle while
    /// placing, counting how many are attached - Falls out of the game's own code once
    /// the altar is a station and they are extensions of it.
    ///
    /// the component is copied off a vanilla station rather than added Blank. several of
    /// CraftingStation's fields are dereferenced without a null check - m_roofCheckPoint
    /// among them - so a hand-configured one throws the first time somebody stands near
    /// it. Copying a working station's Values and cloning the child objects it points at
    /// leaves Nothing unset.
    /// </summary>
    internal static class AltarStation
    {
        private static CraftingStation _shared;

        /// <summary>the station every upgrade attaches to. null until an altar is built.</summary>
        public static CraftingStation shared { get { return _shared; } }

        /// <summary>child objects a station points at, which have to be cloned rather than shared.</summary>
        private static readonly string[] OwnedFields =
        {
            "m_areaMarker", "m_roofCheckPoint", "m_connectionEffectPoint", "m_haveFireObject"
        };

        public static CraftingStation attach(GameObject prefab, string DisplayName)
        {
            if (!ThrallConfig.AltarIsStation.Value) return null;

            var donorName = (ThrallConfig.AltarStationCopyFrom.Value ?? "").Trim();
            var donor = donorName.Length > 0 ? ZNetScene.instance.GetPrefab(donorName) : null;
            var source = donor != null ? donor.GetComponent<CraftingStation>() : null;

            if (source == null)
            {
                ThrallsPlugin.Log.LogWarning("No station to copy from ('" + donorName
                                             + "'); the altar will not be a crafting station.");
                return null;
            }

            var station = prefab.GetComponent<CraftingStation>();
            if (station == null) station = prefab.AddComponent<CraftingStation>();

            // Every public field, then the ones that must not be shared.
            foreach (var field in typeof(CraftingStation).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                field.SetValue(station, field.GetValue(source));
            }

            foreach (var Name in OwnedFields) Reparent(station, source, Name, prefab.transform);

            station.m_name = DisplayName;
            station.m_discoverRange = ThrallConfig.AltarStationRange.Value;
            station.m_rangeBuild = ThrallConfig.AltarStationRange.Value;

            // binding a thrall is not smithing practice, and a station that quietly trains
            // a skill every time you use it is a surprise nobody asked for.
            station.m_craftingSkill = Skills.SkillType.None;

            // the altar stands outdoors by design - it is a shrine, not a workshop.
            station.m_craftRequireRoof = false;
            station.m_craftRequireFire = false;

            _shared = station;
            ThrallsPlugin.Log.LogInfo("altar registered as crafting station '" + DisplayName
                                      + "' with range " + station.m_discoverRange + ".");
            return station;
        }

        /// <summary>
        /// Clones whatever child Object a station field points at onto our own prefab.
        ///
        /// Copying the field alone would leave every altar in the world pointing at the
        /// donor workbench's marker and roof probe - one shared Object, positioned at the
        /// workbench, driving all of them.
        /// </summary>
        private static void Reparent(CraftingStation station, CraftingStation source,
                                     string fieldName, Transform parent)
        {
            var field = typeof(CraftingStation).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            var Value = field.GetValue(source);
            if (Value == null) { field.SetValue(station, null); return; }

            var donorObject = Value as GameObject;
            var donorTransform = Value as Transform;
            var original = donorObject != null ? donorObject
                         : donorTransform != null ? donorTransform.gameObject : null;
            if (original == null) return;

            var copy = Object.Instantiate(original, parent);
            copy.Name = original.Name;
            copy.transform.localPosition = original.transform.localPosition;
            copy.transform.localRotation = original.transform.localRotation;

            field.SetValue(station, donorTransform != null ? (Object)copy.transform : copy);
        }

        /// <summary>
        /// Hangs an upgrade off the altar the way a chopping block Hangs off a workbench.
        /// </summary>
        public static bool AttachExtension(GameObject prefab, CraftingStation station)
        {
            if (station == null) return false;

            var extension = prefab.GetComponent<StationExtension>();
            if (extension == null) extension = prefab.AddComponent<StationExtension>();

            extension.m_craftingStation = station;
            extension.m_maxStationDistance = ThrallConfig.AltarStationRange.Value;
            extension.m_continousConnection = true;
            extension.m_connectionOffset = new Vector3(0f, ThrallConfig.UpgradeLinkHeight.Value, 0f);
            extension.m_connectionPrefab = ConnectionEffect();
            extension.m_stack = false;
            return true;
        }

        private static GameObject ConnectionEffect()
        {
            foreach (var Name in (ThrallConfig.UpgradeLinkFrom.Value ?? "").Split(','))
            {
                var trimmed = Name.Trim();
                if (trimmed.Length == 0) continue;

                var found = ZNetScene.instance.GetPrefab(trimmed);
                if (found != null) return found;
            }

            return null;
        }
    }
}
