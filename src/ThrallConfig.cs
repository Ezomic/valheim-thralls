using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Thralls
{
    /// <summary>All tunables. Everything the player is likely to want to change lives here.</summary>
    internal static class ThrallConfig
    {
        // --- keys ---
        public static ConfigEntry<KeyboardShortcut> KeyTimeOfDay;
        public static ConfigEntry<KeyboardShortcut> KeyFlatten;
        public static ConfigEntry<float> FlattenRadius;
        public static ConfigEntry<KeyboardShortcut> KeyGodMode;
        public static ConfigEntry<KeyboardShortcut> KeyAltarEffects;
        public static ConfigEntry<bool> ShowThrallsOnMap;
        public static ConfigEntry<float> MapToggleOffset;
        public static ConfigEntry<string> MapPinType;
        public static ConfigEntry<bool> MapPinLabels;
        public static ConfigEntry<bool> GodMode;

        // --- thralls ---
        public static ConfigEntry<int> MaxThralls;
        public static ConfigEntry<int> Breeds;
        public static ConfigEntry<string> RecruitCost;
        public static ConfigEntry<string> LevelThresholds;
        public static ConfigEntry<float> XpPerSwing;
        public static ConfigEntry<float> XpPerHarvest;
        public static ConfigEntry<float> XpPerPlant;
        public static ConfigEntry<float> XpPerRepair;
        // Named for the key it binds. It was MaxTier, which is the one thing it is not:
        // this caps the rank a thrall trains to, and tier is the breed you hired.
        public static ConfigEntry<int> MaxLevel;
        public static ConfigEntry<float> TierDamageStep;
        public static ConfigEntry<float> TierSpeedStep;
        public static ConfigEntry<float> LevelDamageStep;
        public static ConfigEntry<float> LevelSpeedStep;
        public static ConfigEntry<string> SmashTiers;
        public static ConfigEntry<float> SmashYield;

        public static ConfigEntry<string> Tier1Prefab;
        public static ConfigEntry<string> Tier2Prefab;
        public static ConfigEntry<string> Tier3Prefab;
        public static ConfigEntry<string> Tier4Prefab;
        public static ConfigEntry<string> Tier5Prefab;

        public static ConfigEntry<string> Tier1Key;
        public static ConfigEntry<string> Tier2Key;
        public static ConfigEntry<string> Tier3Key;
        public static ConfigEntry<string> Tier4Key;
        public static ConfigEntry<string> Tier5Key;

        public static ConfigEntry<bool> UpgradesGateTiers;
        public static ConfigEntry<string> Upgrade1Parts;
        public static ConfigEntry<string> Upgrade2Parts;
        public static ConfigEntry<string> Upgrade3Parts;
        public static ConfigEntry<string> Upgrade4Parts;
        public static ConfigEntry<string> Upgrade1Cost;
        public static ConfigEntry<string> Upgrade2Cost;
        public static ConfigEntry<string> Upgrade3Cost;
        public static ConfigEntry<string> Upgrade4Cost;
        public static ConfigEntry<string> Upgrade1Model;
        public static ConfigEntry<string> Upgrade2Model;
        public static ConfigEntry<string> Upgrade3Model;
        public static ConfigEntry<string> Upgrade4Model;
        public static ConfigEntry<string> Tier1Cost;
        public static ConfigEntry<string> Tier2Cost;
        public static ConfigEntry<string> Tier3Cost;
        public static ConfigEntry<string> Tier4Cost;
        public static ConfigEntry<string> Tier5Cost;
        public static ConfigEntry<string> Tier1Revive;
        public static ConfigEntry<string> Tier2Revive;
        public static ConfigEntry<string> Tier3Revive;
        public static ConfigEntry<string> Tier4Revive;
        public static ConfigEntry<string> Tier5Revive;
        public static ConfigEntry<string> Tier1Recall;
        public static ConfigEntry<string> Tier2Recall;
        public static ConfigEntry<string> Tier3Recall;
        public static ConfigEntry<string> Tier4Recall;
        public static ConfigEntry<string> Tier5Recall;
        public static ConfigEntry<int> PackBaseSlots;
        public static ConfigEntry<int> PackPerTier;
        public static ConfigEntry<int> PackLevelsPerSlot;

        // --- work ---
        public static ConfigEntry<float> WorkRadius;
        public static ConfigEntry<float> HarvestRange;
        public static ConfigEntry<float> SwingInterval;
        public static ConfigEntry<float> ChopDamage;
        public static ConfigEntry<float> PickaxeDamage;
        public static ConfigEntry<int> ToolTier;
        public static ConfigEntry<float> PickupRadius;
        public static ConfigEntry<float> DepositRange;

        // --- the depot ---
        public static ConfigEntry<string> DepotName;
        public static ConfigEntry<string> DepotCost;
        public static ConfigEntry<string> DepotBasePrefab;
        public static ConfigEntry<string> DepotModel;
        public static ConfigEntry<float> DepotScale;
        public static ConfigEntry<float> DepotRange;
        public static ConfigEntry<int> DepotWidth;
        public static ConfigEntry<int> DepotHeight;

        // --- talking to a thrall ---
        public static ConfigEntry<bool> TalkOnUse;
        public static ConfigEntry<float> TalkWalkAway;
        public static ConfigEntry<bool> WorkAtNight;
        public static ConfigEntry<string> MineablePrefabs;
        public static ConfigEntry<int> SeedsPerTrip;
        public static ConfigEntry<string> SwingAnimation;

        // --- misc ---
        public static ConfigEntry<string> AltarName;
        public static ConfigEntry<string> AltarCost;
        public static ConfigEntry<string> AltarBasePrefab;
        public static ConfigEntry<float> AltarRange;
        public static ConfigEntry<float> AltarScale;
        public static ConfigEntry<string> AltarParts;
        public static ConfigEntry<string> PropsPlinth;
        public static ConfigEntry<string> PropsDolmen;
        public static ConfigEntry<string> PropsCairn;
        public static ConfigEntry<string> PropsCircle;
        public static ConfigEntry<string> PropsBarrow;
        public static ConfigEntry<string> PropsWorktable;
        public static ConfigEntry<string> PropsBindstone;
        public static ConfigEntry<string> PropsNoEffects;
        public static ConfigEntry<string> PropsShrine;
        public static ConfigEntry<bool> AltarEffects;
        public static ConfigEntry<float> AltarEffectStrength;
        public static ConfigEntry<string> AltarEffectFrom;
        public static ConfigEntry<bool> AltarLight;
        public static ConfigEntry<float> AltarLightRange;
        public static ConfigEntry<float> AltarLightStrength;
        public static ConfigEntry<int> OneStarRank;
        public static ConfigEntry<int> TwoStarRank;
        public static ConfigEntry<bool> AltarFlattenNormals;
        public static ConfigEntry<string> AltarVanillaGroups;
        public static ConfigEntry<string> AltarVanillaWoodGroups;
        public static ConfigEntry<string> AltarWoodMaterialFrom;
        public static ConfigEntry<string> AltarShapes;
        public static ConfigEntry<string> AltarModel;
        public static ConfigEntry<string> AltarMaterialFrom;
        public static ConfigEntry<bool> AltarBakedShading;
        public static ConfigEntry<bool> AltarTexturePoint;
        public static ConfigEntry<bool> AltarDiagnostics;
        public static ConfigEntry<bool> AltarScreenshot;
        public static ConfigEntry<float> AltarUvScale;
        public static ConfigEntry<string> AltarUvRegion;
        public static ConfigEntry<int> BaseWorkSlots;
        public static ConfigEntry<int> MaxWorkSlots;
        public static ConfigEntry<float> SlotSearchRange;

        public static ConfigEntry<bool> RequireTools;
        public static ConfigEntry<string> ToolsChop;
        public static ConfigEntry<string> ToolsMine;
        public static ConfigEntry<string> ToolsFarm;
        public static ConfigEntry<bool> ShowTools;
        public static ConfigEntry<string> ToolChop;
        public static ConfigEntry<string> ToolMine;
        public static ConfigEntry<string> ToolFarm;
        public static ConfigEntry<string> ToolBuild;



        public static ConfigEntry<bool> Verbose;
        public static ConfigEntry<bool> TestMode;

        /// <summary>What each upgrade piece is called on the hammer.</summary>
        public static string UpgradeName(int level)
        {
            switch (level)
            {
                // Named for what each one opens, so the hammer menu reads as a ladder.
                //
                // No numerals on the end: the game does not number its own station
                // upgrades - chopping block, tanning rack, adze, forge bellows - and each
                // of these says in its description what it opens, so a "(II)" was only
                // ever repeating what the menu already showed.
                case 1: return "Bog stone";
                case 2: return "Mountain cairn";
                case 3: return "War totem";
                default: return "Rift stone";
            }
        }

        /// <summary>
        /// Names an upgrade has gone by in an earlier version of this mod.
        ///
        /// Player.m_knownRecipes is keyed by the display name and there is no id beside
        /// it, so renaming a piece un-learns it for everyone who had it and drops it out
        /// of the hammer with no message at all. Anything renamed from here on has to
        /// leave its old name behind in this list. A stale entry costs nothing; a missing
        /// one costs the player the piece.
        /// </summary>
        public static string[] UpgradeLegacyNames(int level)
        {
            switch (level)
            {
                case 1: return new[] { "Bog stone (I)" };
                case 2: return new[] { "Mountain cairn (II)" };
                case 3: return new[] { "War camp arch (III)", "War totem (III)" };
                default: return new[] { "Rift stone (IV)" };
            }
        }

        /// <summary>
        /// What everything costs while testing: one wood.
        ///
        /// This exists because the altar tiers are gated behind four biomes, and checking
        /// that a mountain cairn renders correctly should not require a mountain. The
        /// costs were all hand-edited to Wood:1 once already while the shapes were being
        /// modelled; this makes that a switch instead of five edits you have to remember
        /// to undo.
        /// </summary>
        private const string TestCost = "Wood:1";

        /// <summary>What the bindstone costs, honouring TestMode.</summary>
        public static string AltarCostNow()
        {
            return TestMode.Value ? TestCost : AltarCost.Value;
        }

        /// <summary>What the depot costs, honouring TestMode for the same reason.</summary>
        public static string DepotCostNow()
        {
            return TestMode.Value ? TestCost : DepotCost.Value;
        }

        /// <summary>What the numbered bindstone upgrade costs.</summary>
        public static string UpgradeCost(int level)
        {
            if (TestMode.Value) return TestCost;

            switch (level)
            {
                case 1: return Upgrade1Cost.Value;
                case 2: return Upgrade2Cost.Value;
                case 3: return Upgrade3Cost.Value;
                default: return Upgrade4Cost.Value;
            }
        }

        /// <summary>
        /// The model file an upgrade wears. Empty, or a name with no file beside the
        /// plugin, falls the piece back to its assembly of vanilla prefabs.
        /// </summary>
        public static string UpgradeModel(int level)
        {
            switch (level)
            {
                case 1: return Upgrade1Model.Value;
                case 2: return Upgrade2Model.Value;
                case 3: return Upgrade3Model.Value;
                default: return Upgrade4Model.Value;
            }
        }

        public static void Bind(ConfigFile cfg)
        {
            // Recruit, Assign, FollowToggle, Dismiss and OpenAltar were bound here.
            //
            // All five are menu entries now - the first four in the panel you get by
            // pressing use on a thrall, and the altar's ledger by pressing use on the
            // altar - and a key that shadows a menu entry is a second thing to keep in
            // step. What is left on keys is only what no menu can reach: the site tools.
            // The stale lines in an existing cfg do nothing.
            KeyTimeOfDay = cfg.Bind("1 - Keys", "TimeOfDay", new KeyboardShortcut(KeyCode.Keypad7),
                "Step the time of day through dawn, midday, dusk and night, then back to normal.");
            KeyFlatten = cfg.Bind("1 - Keys", "FlattenGround", new KeyboardShortcut(KeyCode.Keypad8),
                "Level a wide circle of ground at the spot you are looking at.");
            ShowThrallsOnMap = cfg.Bind("2 - Thralls", "ShowThrallsOnMap", false,
                "Whether thralls are marked on the map. Set by the 'Show thralls' checkbox on the map itself, under the one that shares your position.");
            MapToggleOffset = cfg.Bind("2 - Thralls", "MapToggleOffset", 26f,
                "How far below the map's public-position checkbox the 'Show thralls' one sits, in pixels. Only worth touching if another mod has put something in that space.");

            MapPinType = cfg.Bind("2 - Thralls", "MapPinType", "Player",
                "Which map marker a thrall gets. One of Icon0, Icon1, Icon2, Icon3, Icon4, Death, Bed, Shout, Boss, Player, Ping, Hildir1, Hildir2, Hildir3.");
            MapPinLabels = cfg.Bind("2 - Thralls", "MapPinLabels", true,
                "Write each thrall's name and job beside its marker. Turn off if a large crew makes the map unreadable.");

            KeyAltarEffects = cfg.Bind("1 - Keys", "AltarEffects",
                new KeyboardShortcut(KeyCode.KeypadMinus),
                "Cycles the bindstone's light and drifting motes on and off: both, no light, no motes, neither. For working out which of them is behind something you can see.");

            KeyGodMode = cfg.Bind("1 - Keys", "GodMode", new KeyboardShortcut(KeyCode.Keypad9),
                "Toggle unlimited health and stamina. A building aid, not a feature.");
            GodMode = cfg.Bind("4 - Misc", "GodMode", false,
                "Hold health and stamina full and switch on the game's own god mode. For building and testing.");
            FlattenRadius = cfg.Bind("3 - Work", "FlattenRadius", 5f,
                "Radius of the ground levelling tool, so 5 gives a 10m circle.");

            Breeds = cfg.Bind("2 - Thralls", "Breeds", 1,
                "How many of the five kinds of thrall are offered, counting up from the greydwarf brute. One ships the brute alone: a thrall is a whole thing to learn rather than the bottom of a ladder, and one kind done properly is a release where five half-tested ones are a wishlist. The other four are written and their code stays in - raise this to 2, 3, 4 or 5 to turn the draugr, golem, berserker and seeker back on. This also decides how many bindstone upgrades exist, since an upgrade is what unlocks the breed above it: at 1 there are none.");
            MaxThralls = cfg.Bind("2 - Thralls", "MaxThralls", 20,
                "How many thralls you may keep in total. Only a few of them can be working at once - see WorkSlots.");
            RecruitCost = cfg.Bind("2 - Thralls", "RecruitCost", "",
                "Cost per thrall, as PrefabName:Amount, comma separated. Empty means free. Set to something like Coins:50 if you want recruiting to bite.");
            // HeadsPerWorker and the Tier2/3/4Trophies lists that classified heads by
            // tier were removed here. They were a whole second currency - any head of the
            // tier or better - charged alongside the breed prices, which already name the
            // head they want. One price, and it is the named one.

            MaxLevel = cfg.Bind("2 - Thralls", "MaxLevel", 20,
                "Highest rank a thrall can train to. This is rank, not tier: it does not change which breed you can hire or what tools they can use, only how far the one you hired can be pushed.");

            LevelThresholds = cfg.Bind("2 - Thralls", "LevelThresholds", "150,400,750,1200,1800,2500,3400,4500,5800,7300,9000,11000,13200,15700,18500,21600,25000,28800,33000",
                "Experience needed to reach level 2, 3, 4 and so on. Thralls earn it by working; there is nothing to buy.");
            XpPerSwing = cfg.Bind("2 - Thralls", "XpPerSwing", 1f,
                "Experience for each swing of the axe or pick.");
            XpPerHarvest = cfg.Bind("2 - Thralls", "XpPerHarvest", 10f,
                "Experience for finishing something off - a tree felled, a vein broken, a crop picked.");
            XpPerPlant = cfg.Bind("2 - Thralls", "XpPerPlant", 3f, "Experience for sowing a seed.");
            XpPerRepair = cfg.Bind("2 - Thralls", "XpPerRepair", 3f, "Experience for a repair.");

            Tier1Prefab = cfg.Bind("2 - Thralls", "Tier1Creature", "Greydwarf_Elite",
                "Creature hired as a tier 1 thrall (greydwarf brute). Must be melee.");
            Tier2Prefab = cfg.Bind("2 - Thralls", "Tier2Creature", "Draugr_Elite",
                "Creature hired as a tier 2 thrall (swamp elite). Must be melee.");
            Tier3Prefab = cfg.Bind("2 - Thralls", "Tier3Creature", "StoneGolem",
                "Creature hired as a tier 3 thrall (mountain golem). Must be melee.");
            Tier4Prefab = cfg.Bind("2 - Thralls", "Tier4Creature", "GoblinBrute",
                "Creature hired as a tier 4 thrall (fuling berserker). Must be melee.");
            Tier5Prefab = cfg.Bind("2 - Thralls", "Tier5Creature", "SeekerBrute",
                "Creature hired as a tier 5 thrall (mistlands seeker). Must be melee - the plain Seeker throws, the brute closes.");

            // Each breed answers once the boss of ITS OWN biome is down, not the boss
            // before it. A greydwarf is a black forest creature, so it waits for the
            // Elder; a draugr is of the swamp, so it waits for Bonemass, and so on. The
            // ladder used to sit one boss earlier all the way up, which handed you black
            // forest labour for killing a stag in the meadows. Tier 5 was already on this
            // footing - the seeker waited for the Queen - so this is the other four
            // catching up with it rather than a new rule.
            //
            // Key names are the game's own, read out of the GlobalKeys enum in
            // assembly_valheim rather than remembered: defeated_eikthyr, defeated_gdking,
            // defeated_bonemass, defeated_dragon, defeated_goblinking.
            Tier1Key = cfg.Bind("2 - Thralls", "Tier1RequiresBoss", "defeated_gdking",
                "The Elder must fall before black forest thralls will answer. Empty means no gate.");
            Tier2Key = cfg.Bind("2 - Thralls", "Tier2RequiresBoss", "defeated_bonemass",
                "Bonemass must fall before swamp thralls will answer.");
            Tier3Key = cfg.Bind("2 - Thralls", "Tier3RequiresBoss", "defeated_dragon",
                "Moder must fall before golems will answer.");
            Tier4Key = cfg.Bind("2 - Thralls", "Tier4RequiresBoss", "defeated_goblinking",
                "Yagluth must fall before berserkers will answer.");
            Tier5Key = cfg.Bind("2 - Thralls", "Tier5RequiresBoss", "defeated_queen",
                "The Queen must fall before seekers will answer.");

            UpgradesGateTiers = cfg.Bind("2 - Thralls", "UpgradesGateTiers", true,
                "Each bindstone upgrade opens the next breed. Turn off to let the boss keys alone decide.");
            Upgrade1Parts = cfg.Bind("2 - Thralls", "Upgrade1Parts",
                "stone_floor_2x2:0,0,0:0.34:0"
                + ";stone_pillar:0,0.04,0:0.30:0"
                + ";piece_groundtorch_green:0,0.70,0:0.85:0",
                "Fallback for the first bindstone upgrade, used only if Upgrade1Model is missing from disk. Each part is prefab:x,y,z:scale:yaw. Green fire over a sunken slab, for the swamp.");
            Upgrade2Parts = cfg.Bind("2 - Thralls", "Upgrade2Parts",
                "stone_floor_2x2:0,0,0:0.46:0"
                + ";stone_floor_2x2:0,0.18,0:0.32:45"
                + ";stone_pillar:0,0.30,0:0.42:0"
                + ";guard_stone:0,0.95,0:0.60:0",
                "Fallback for the second bindstone upgrade, used only if Upgrade2Model is missing. Stacked rock with a rune stone crowning it, for the mountain.");
            Upgrade4Parts = cfg.Bind("2 - Thralls", "Upgrade4Parts",
                "blackmarble_column_1:0,0,0:0.5:0"
                + ";blackmarble_head_big01:0,0.9,0:0.4:0",
                "What the fourth bindstone upgrade is built out of, if its model is missing. Black marble, for the mistlands.");
            Upgrade3Parts = cfg.Bind("2 - Thralls", "Upgrade3Parts",
                "stone_floor_2x2:0,0,0:0.44:0"
                + ";stone_arch:0,0.10,0:0.52:0"
                + ";fire_pit:0,0.14,0:0.55:0"
                + ";piece_groundtorch:-0.62,0.16,0:0.78:0"
                + ";piece_groundtorch:0.62,0.16,0:0.78:0",
                "Fallback for the third bindstone upgrade, used only if Upgrade3Model is missing. An arch over open fire, for the war camps of the plains.");

            // Each one is paid for in the goods of the biome whose dead it opens, so the
            // rung cannot be reached before you have been where its thralls come from.
            // These were all Wood:1 while the shapes were being built - a test value that
            // let every breed be unlocked in a minute, and one that should not ship.
            Upgrade1Cost = cfg.Bind("2 - Thralls", "Upgrade1Cost",
                "Iron:10,Guck:10,WitheredBone:5,Stone:20,ElderBark:15",
                "What the bog stone costs. It is the first bindstone upgrade and it opens swamp elites.");
            Upgrade2Cost = cfg.Bind("2 - Thralls", "Upgrade2Cost",
                "Obsidian:20,FineWood:20,DeerHide:10,Silver:6,Crystal:25,FreezeGland:10",
                "What the mountain cairn costs. It is the second bindstone upgrade and it opens mountain golems.");
            Upgrade3Cost = cfg.Bind("2 - Thralls", "Upgrade3Cost",
                "BlackMetal:20,Needle:15,LinenThread:25,GoblinTotem:2",
                "What the war totem costs. It is the third bindstone upgrade and it opens fuling berserkers.");
            Upgrade4Cost = cfg.Bind("2 - Thralls", "Upgrade4Cost",
                "BlackMarble:30,Eitr:20,YggdrasilWood:30,Iron:30",
                "What the rift stone costs. It is the fourth bindstone upgrade and it opens mistlands seekers.");

            // Each upgrade wears its own hand-modelled mesh, sitting next to the plugin
            // alongside the altar's. The UpgradeNParts assemblies above are still the
            // fallback for anyone who deletes the files, which is why they stay.
            //
            // Each carries the breed it unlocks rather than only its biome: a draugr
            // rising out of the guck, a golem under the cairn, a fuling war totem.
            Upgrade1Model = cfg.Bind("2 - Thralls", "Upgrade1Model",
                "thrall_altar_upgrade1.obj",
                "Model file for the first bindstone upgrade, sitting next to the plugin dll. Empty falls back to Upgrade1Parts.");
            Upgrade2Model = cfg.Bind("2 - Thralls", "Upgrade2Model",
                "thrall_altar_upgrade2.obj",
                "Model file for the second bindstone upgrade. Empty falls back to Upgrade2Parts.");
            Upgrade3Model = cfg.Bind("2 - Thralls", "Upgrade3Model",
                "thrall_altar_upgrade3.obj",
                "Model file for the third bindstone upgrade. Empty falls back to Upgrade3Parts.");
            Upgrade4Model = cfg.Bind("2 - Thralls", "Upgrade4Model",
                "thrall_altar_upgrade4.obj",
                "Model file for the fourth bindstone upgrade. Empty falls back to Upgrade4Parts.");

            Tier1Cost = cfg.Bind("2 - Thralls", "Tier1Cost",
                "Bronze:5,Resin:20,GreydwarfEye:10,RoundLog:25,Stone:25,TrophyGreydwarfBrute:1",
                "What it costs to raise a greydwarf brute, as PrefabName:Amount separated by commas.");
            Tier2Cost = cfg.Bind("2 - Thralls", "Tier2Cost",
                "Iron:5,Entrails:20,Bloodbag:10,ElderBark:25,Flint:25,TrophyDraugrElite:1",
                "What it costs to raise a swamp elite.");
            Tier3Cost = cfg.Bind("2 - Thralls", "Tier3Cost",
                "Silver:5,Crystal:10,RoundLog:15,ElderBark:15,Stone:15,Flint:15,TrophySGolem:1",
                "What it costs to raise a mountain golem.");
            Tier5Cost = cfg.Bind("2 - Thralls", "Tier5Cost",
                "BlackMarble:20,Eitr:15,Softtissue:20,Carapace:15,TrophySeekerBrute:1",
                "What it costs to raise a mistlands seeker.");
            Tier4Cost = cfg.Bind("2 - Thralls", "Tier4Cost",
                "BlackMetal:10,Coins:50,FineWood:25,Obsidian:25,TrophyGoblinBrute:1",
                "What it costs to raise a fuling berserker.");

            Tier1Revive = cfg.Bind("2 - Thralls", "Tier1ReviveCost", "GreydwarfEye:20,Wood:20",
                "Black forest goods to raise a fallen tier 1 thrall.");
            Tier2Revive = cfg.Bind("2 - Thralls", "Tier2ReviveCost", "Bloodbag:15,IronScrap:5",
                "Swamp goods to raise a fallen tier 2 thrall.");
            Tier3Revive = cfg.Bind("2 - Thralls", "Tier3ReviveCost", "Crystal:10,WolfPelt:5",
                "Mountain goods to raise a fallen tier 3 thrall.");
            Tier4Revive = cfg.Bind("2 - Thralls", "Tier4ReviveCost", "BlackMetalScrap:15,Needle:10",
                "Plains goods to raise a fallen tier 4 thrall.");
            Tier5Revive = cfg.Bind("2 - Thralls", "Tier5ReviveCost", "Eitr:10,BlackCore:2",
                "Mistlands goods to raise a fallen tier 5 thrall.");

            // Calling a resting thrall back costs about half what raising a dead one does.
            //
            // Sending one to rest is free and always will be - it is you deciding you have
            // too many mouths at the treeline, and charging for tidying up is the sort of
            // rule that makes people leave thralls standing in a field instead. Waking one
            // is the other half of that bargain, and it wants a price or the roll becomes
            // free storage: bind five, rest four, and swap whichever you need for nothing.
            // Cheaper than a raise, because the thrall did not die - you put it away.
            Tier1Recall = cfg.Bind("2 - Thralls", "Tier1RecallCost", "GreydwarfEye:10,Wood:10",
                "Black forest goods to call a resting tier 1 thrall back. Empty means free.");
            Tier2Recall = cfg.Bind("2 - Thralls", "Tier2RecallCost", "Bloodbag:8,IronScrap:2",
                "Swamp goods to call a resting tier 2 thrall back. Empty means free.");
            Tier3Recall = cfg.Bind("2 - Thralls", "Tier3RecallCost", "Crystal:5,WolfPelt:2",
                "Mountain goods to call a resting tier 3 thrall back. Empty means free.");
            Tier4Recall = cfg.Bind("2 - Thralls", "Tier4RecallCost", "BlackMetalScrap:8,Needle:5",
                "Plains goods to call a resting tier 4 thrall back. Empty means free.");
            Tier5Recall = cfg.Bind("2 - Thralls", "Tier5RecallCost", "Eitr:5,BlackCore:1",
                "Mistlands goods to call a resting tier 5 thrall back. Empty means free.");

            TierDamageStep = cfg.Bind("2 - Thralls", "TierDamageStep", 0.5f,
                "Extra work damage per tier above the first, as a fraction.");
            TierSpeedStep = cfg.Bind("2 - Thralls", "TierSpeedStep", 0.12f,
                "How much faster each tier swings, as a fraction of the base interval.");
            LevelDamageStep = cfg.Bind("2 - Thralls", "LevelDamageStep", 0.08f,
                "Extra work damage per level above the first, as a fraction.");
            LevelSpeedStep = cfg.Bind("2 - Thralls", "LevelSpeedStep", 0.03f,
                "How much faster each level swings, as a fraction of the base interval.");

            SmashTiers = cfg.Bind("2 - Thralls", "SmashTiers", "3",
                "Tiers that fell trees by hand instead of with an axe, comma separated. Does nothing at the shipped Breeds of 2 - the golem is tier 3 - so this waits for the release that turns the golem on. A "
                + "breed listed here needs no axe to be set to chopping and will not be "
                + "offered one, and in exchange it keeps only SmashYield of what the tree "
                + "drops. The golem is the one that fits: it is a walking boulder, so it "
                + "clears a treeline faster than anything else you can bind and leaves you "
                + "splinters for it. Empty means every breed has to be handed an axe.");
            SmashYield = cfg.Bind("2 - Thralls", "SmashYield", 0.2f,
                "What share of a smashed tree survives, from 0 to 1. At the default a "
                + "thrall keeps one log in five and the rest is wasted. Set to 1 to remove "
                + "the penalty and leave only the no-axe part, or to 0 for a breed that "
                + "clears ground and brings back nothing at all.");

            PackBaseSlots = cfg.Bind("2 - Thralls", "PackBaseSlots", 1,
                "Pack slots a tier one thrall carries. A greydwarf brute is not a pack mule.");
            PackPerTier = cfg.Bind("2 - Thralls", "PackPerTier", 1,
                "Extra pack slots for each tier above the first, so a berserker out-carries a brute.");
            PackLevelsPerSlot = cfg.Bind("2 - Thralls", "PackLevelsPerSlot", 5,
                "Levels between each extra pack slot. Note this only ever fires if MaxLevel is at least this high.");


            WorkRadius = cfg.Bind("3 - Work", "WorkRadius", 25f,
                "How far from its assigned spot a thrall looks for more of the same resource.");
            HarvestRange = cfg.Bind("3 - Work", "HarvestRange", 4.5f,
                "How close a thrall must be to swing at its target.");
            SwingInterval = cfg.Bind("3 - Work", "SwingInterval", 1.6f,
                "Seconds between swings. Higher = slower workers.");
            ChopDamage = cfg.Bind("3 - Work", "ChopDamage", 40f, "Chop damage per swing (trees).");
            PickaxeDamage = cfg.Bind("3 - Work", "PickaxeDamage", 40f, "Pickaxe damage per swing (rock and ore).");
            ToolTier = cfg.Bind("3 - Work", "ToolTier", 1,
                "Tool tier of a tier 1 thrall. Each breed above the first adds one, so a berserker works as tier 4 and a seeker as tier 5. Levels do NOT raise this - they only make a thrall hit harder and faster, so a rank 20 brute still cannot touch what a draugr can. 0 = flint/antler, 2 = bronze/iron, 4 = black metal.");
            PickupRadius = cfg.Bind("3 - Work", "PickupRadius", 10f, "How far a thrall reaches to pick up what it knocked loose.");
            DepositRange = cfg.Bind("3 - Work", "DepositRange", 4f, "How close a thrall must be to the depot to unload into it.");

            // Where the chest settings used to be.
            //
            // A thrall unloads into a depot and nowhere else now. Pointing at a chest and
            // pressing a key was a chore repeated once per thrall, and the auto-adopt that
            // was added to soften it made the opposite problem - a thrall would quietly
            // claim whatever box happened to be nearest the altar, including one you were
            // keeping something else in. Building the store is a clearer instruction than
            // either. AutoDropOff, AutoDropOffRange and the SetDropOff key are gone; the
            // stale lines left behind in an existing cfg do nothing.

            DepotName = cfg.Bind("7 - Depot", "DepotName", "Thrall depot",
                "What the depot is called in the build menu and when you look at it.");
            DepotCost = cfg.Bind("7 - Depot", "DepotCost", "Wood:20,RoundLog:6,LeatherScraps:4,Iron:2",
                "What it takes to build a depot. Iron puts it at roughly the bindstone's own tier, so a crew that can be bound can be given somewhere to unload.");
            DepotBasePrefab = cfg.Bind("7 - Depot", "DepotBasePrefab", "piece_chest_wood",
                "Existing piece the depot is cloned from, for its components - the container, the network view, the wear-and-tear. Its own model is switched off and replaced, so this does NOT decide how the depot looks. It MUST be something carrying a Container or there is nowhere for the goods to go.");
            DepotModel = cfg.Bind("7 - Depot", "DepotModel", "thrall_depot.obj",
                "Model file sitting next to the plugin dll. Delete or rename it and the depot falls back to the donor chest's own model, which works but looks like a chest.");
            DepotScale = cfg.Bind("7 - Depot", "DepotScale", 1f,
                "Overall size of the depot. The mast stands 2.7m as modelled, which is deliberate - it is the landmark for its own radius.");
            DepotRange = cfg.Bind("7 - Depot", "DepotRange", 60f,
                "How far a depot reaches. A thrall whose work base is inside this hauls to it. Generous on purpose: the point of the depot is that a crew scattered across a treeline all bring their loads to the same place, and a radius smaller than the walk is just a thrall standing still with a full pack.");
            DepotWidth = cfg.Bind("7 - Depot", "DepotWidth", 6,
                "Columns of storage in the depot.");
            DepotHeight = cfg.Bind("7 - Depot", "DepotHeight", 4,
                "Rows of storage in the depot. Six by four is a large chest and a half: a crew of five fills a normal chest in an afternoon, and a full depot stops them working.");

            TalkOnUse = cfg.Bind("7 - Depot", "TalkOnUse", true,
                "Pressing use on a thrall opens its orders panel. Turn off to leave the key doing whatever the creature normally does with it.");
            TalkWalkAway = cfg.Bind("7 - Depot", "TalkWalkAway", 8f,
                "How far you can walk from a thrall before its orders panel closes itself. Matches the bindstone panel.");

            WorkAtNight = cfg.Bind("3 - Work", "WorkAtNight", true, "If false, thralls idle between dusk and dawn.");
            MineablePrefabs = cfg.Bind("3 - Work", "ExtraMineableNames", "rock,stone,copper,tin,silver,obsidian,flametal,mudpile,ore",
                "Extra name fragments treated as mineable, on top of proper ore veins. Comma separated, case insensitive.");
            SeedsPerTrip = cfg.Bind("3 - Work", "SeedsPerTrip", 20,
                "How much seed a farming thrall draws from the depot per visit. 0 stops them restocking, so they only sow what you hand them directly.");

            SwingAnimation = cfg.Bind("3 - Work", "SwingAnimation", "",
                "Animator trigger played when a thrall works. Empty picks the first one the creature has. Set to 'none' for no animation at all if your chosen creature does something silly.");

            AltarName = cfg.Bind("2 - Thralls", "AltarName", "Bindstone",
                "What the bindstone is called in the build menu and when you look at it.");
            AltarCost = cfg.Bind("2 - Thralls", "AltarCost", "HardAntler:3,Stone:30,Wood:20,GreydwarfEye:5,Bronze:5",
                "What it takes to build the bindstone. ElderBark is ancient bark, so the bindstone now sits behind the swamp rather than behind Eikthyr - hard antler and greydwarf eyes no longer set the gate, they are just flavour on top of it.");
            AltarBasePrefab = cfg.Bind("2 - Thralls", "AltarBasePrefab", "guard_stone",
                "Existing piece the bindstone is cloned from, for its components - the network view, the wear-and-tear and the piece itself. Its own model is switched off and replaced, so this does NOT decide how the altar looks; AltarMaterialFrom and the model files do. Change only if the clone is misbehaving.");
            AltarRange = cfg.Bind("2 - Thralls", "AltarRange", 20f,
                "How close to a bindstone you must be to bind thralls or open its panel.");
            AltarScale = cfg.Bind("2 - Thralls", "AltarScale", 1f,
                "Overall size of the bindstone.");
            // The bench is gone: the bindstone replaced it and it has been archived.
            //
            // Note this list, not the files on disk, is what decides whether a shape's
            // prefab exists - Shapes() never checks the disk. A name dropped from here
            // destroys every altar of that shape already standing, because ZNetScene
            // discards ZDOs whose prefab it cannot resolve. A name kept here with no model
            // beside the plugin is harmless: it falls back to the AltarParts assembly.
            AltarShapes = cfg.Bind("2 - Thralls", "AltarShapes",
                "bindstone",
                "Which bindstone shapes appear on the hammer, each as its own buildable piece. They all work identically. Names match the model files sitting next to the plugin dll; the shelved ones are in assets/archive. CAREFUL: this list alone decides which prefabs exist - nothing here checks the disk. Removing a name destroys every altar of that shape already standing in a world, because ZNetScene discards ZDOs whose prefab it cannot resolve. Leaving a name here with no model beside the plugin is harmless: it falls back to the AltarParts assembly. Empty falls back to the single AltarModel.");
            AltarVanillaGroups = cfg.Bind("2 - Thralls", "AltarVanillaGroups", "darkstone",
                "Material groups that wear Valheim's own stone material instead of a texture of ours, comma separated. Ours are flattened to remove the normal map, which is where the game's stone gets most of its contrast, so a large plain mass reads flatter and darker than the rock around it. Empty means every group uses our own sheets.");
            OneStarRank = cfg.Bind("2 - Thralls", "OneStarRank", 10,
                "Rank at which a thrall gains its first star. Stars are the game's own creature levels, so a starred thrall is tougher as well as marked.");
            TwoStarRank = cfg.Bind("2 - Thralls", "TwoStarRank", 20,
                "Rank at which a thrall gains its second star.");
            AltarFlattenNormals = cfg.Bind("2 - Thralls", "AltarFlattenNormals", false,
                "Replace the borrowed material's normal map with a flat one on our own textures. Off keeps it, which is where the game's surfaces get their highlights: with it on, every custom sheet reads flat and dead next to real wood and stone however bright the texture is. On is the old behaviour, worth returning to if the donor's bumps land in obviously wrong places on our geometry.");
            AltarVanillaWoodGroups = cfg.Bind("2 - Thralls", "AltarVanillaWoodGroups", "timber,wood",
                "Material groups that wear Valheim's own wood material instead of a texture of ours, comma separated. Same reasoning as AltarVanillaGroups: ours are flattened of their normal map, so a pole beside a real workbench reads black and grainless. Empty means every wood group uses our own sheets.");
            AltarWoodMaterialFrom = cfg.Bind("2 - Thralls", "AltarWoodMaterialFrom",
                "wood_wall,wood_beam,wood_pole,wood_floor,piece_workbench,wood_door",
                "Pieces to lift a wood material from, first one that resolves wins. The wooden counterpart of AltarMaterialFrom.");
            AltarModel = cfg.Bind("2 - Thralls", "AltarModel", "thrall_altar_bindstone.obj",
                "Model file sitting next to the plugin dll, used only when AltarShapes is empty. Delete or rename it to fall back to the altar assembled from existing pieces.");
            AltarDiagnostics = cfg.Bind("6 - Altar props", "Diagnostics", false,
                "Cycle the bindstone through a series of test states on load, photographing each. For working out which part of the altar is responsible for something you can see. Costs a few seconds and six images every time a world loads.");
            AltarScreenshot = cfg.Bind("6 - Altar props", "Screenshot", false,
                "Photograph the bindstone from four sides shortly after it loads, next to the plugin. Only bindstones standing at load time are caught. Do NOT judge colour or brightness from these: the routine forces daylight before shooting and renders through a camera of its own, which carries none of the game's post-processing, so every object in them is lighter and flatter than on screen. Good for shape and placement, misleading for anything else.");

            AltarTexturePoint = cfg.Bind("2 - Thralls", "AltarTexturePoint", true,
                "Sample the bindstone textures without smoothing, so texels stay square. This is most of what gives Valheim props their crisp blocky look up close; turn it off for a soft, filtered surface.");

            AltarBakedShading = cfg.Bind("2 - Thralls", "AltarBakedShading", false,
                "Feed the bindstone's baked ambient occlusion to the shader through vertex colours. Only correct if Valheim's piece shader treats vertex colour as a plain tint; if it uses those channels for blending or wear instead, this paints black facets across the model. Off sends plain white.");

            AltarUvScale = cfg.Bind("2 - Thralls", "AltarUvScale", 0.5f,
                "How many times the texture repeats per metre. Lower means larger, coarser grain. Must match UV_SCALE in tools/altar_model.py, or the preview renders lie about how the altar will look.");
            AltarUvRegion = cfg.Bind("2 - Thralls", "AltarUvRegion", "",
                "Patch of the shared stone atlas to sample, as x,y,width,height. Empty reads it off the donor piece, which is almost always right.");

            AltarMaterialFrom = cfg.Bind("2 - Thralls", "AltarMaterialFrom",
                "stone_wall_2x1,stone_floor_2x2,stone_pillar,stone_arch,stone_wall_4x2,guard_stone,blackmarble_column_1",
                "Pieces to lift a stone material from, first one that resolves wins. This is what makes the bindstone light and weather like the rest of the world.");

            // Dressed and ceremonial: torches lighting the steps.
            PropsPlinth = cfg.Bind("6 - Altar props", "Plinth",
                "piece_groundtorch:-1.05,0,-1.95:0.9:0"
                + ";piece_groundtorch:1.05,0,-1.95:0.9:0",
                "Props added to the plinth bindstone, as prefab:x,y,z:scale:yaw separated by semicolons. Missing prefabs are skipped.");

            // Ancient and cold: dwarven lamps under the capstone.
            PropsDolmen = cfg.Bind("6 - Altar props", "Dolmen",
                "piece_dvergr_lantern:0,0,-1.55:0.85:0"
                + ";piece_groundtorch_blue:-1.95,0,1.05:0.8:0"
                + ";piece_groundtorch_blue:1.95,0,1.05:0.8:0",
                "Props added to the dolmen bindstone.");

            // Rough and lived-in: a fire burning in the crown of the pile.
            PropsCairn = cfg.Bind("6 - Altar props", "Cairn",
                "fire_pit:0,1.32,0:0.75:0"
                + ";piece_groundtorch_wood:-2.05,0,-1.25:0.85:0",
                "Props added to the cairn bindstone.");

            // Ritual and overgrown: green fire in the ring.
            PropsCircle = cfg.Bind("6 - Altar props", "Circle",
                "fire_pit:0,0.36,0:0.7:0"
                + ";piece_groundtorch_green:-2.15,0,1.25:0.85:0"
                + ";piece_groundtorch_green:2.15,0,1.25:0.85:0"
                + ";piece_groundtorch_green:0,0,-2.35:0.85:0",
                "Props added to the stone circle bindstone.");

            PropsNoEffects = cfg.Bind("6 - Altar props", "PropsWithoutEffects", "Trophy",
                "Prefab name prefixes whose props lose their particles and lights when mounted on a bindstone. Item prefabs sparkle so you can find them dropped in grass, which on an altar just looks like loot lying there. Torches and candles are not listed, because their flame is the point.");

            AltarEffects = cfg.Bind("6 - Altar props", "Motes", false,
                "Drifting motes above each bindstone, coloured to match it. Off by default: the bindstones now wear the game's own stone and wood, and the motes were doing work that the material was not doing before. Turn on for the coloured drift.");
            AltarEffectStrength = cfg.Bind("6 - Altar props", "MoteStrength", 1f,
                "Multiplier on how many motes each bindstone gives off.");
            AltarLight = cfg.Bind("6 - Altar props", "Light", false,
                "A light on the bindstone itself, coloured to match its motes. Vanilla torches cannot be used as props for this - they are stripped of the component that lights them - and candles alone are too dim to work by.");
            AltarLightRange = cfg.Bind("6 - Altar props", "LightRange", 18f,
                "How far the bindstone's light reaches, in metres.");
            AltarLightStrength = cfg.Bind("6 - Altar props", "LightStrength", 2f,
                "How bright the bindstone's light is, as a multiple of one of Valheim's own fires. 1 is about a campfire. 0 is the same as turning it off.");

            AltarEffectFrom = cfg.Bind("6 - Altar props", "MoteMaterialFrom",
                "fire_pit,bonfire,piece_groundtorch,piece_groundtorch_green",
                "Pieces to borrow a particle material from, so the motes blend and fog like the game's own effects.");

            // Bench sized and meant for indoors, so only what fits on the stone itself.
            PropsShrine = cfg.Bind("6 - Altar props", "Shrine",
                "Candle_resin:-0.62,1.06,-0.18:1:0"
                + ";Candle_resin:0.62,1.06,-0.18:1:0",
                "Props added to the bench-sized shrine.");

            // Candle flames on the boards, and nothing else. The torches that used to
            // flank it never lit: a prop has every component stripped off it before it
            // goes on, and a torch without its Fireplace is an unlit post. The altar's
            // own light does the work those were meant to do.
            // Scaled well down: a vanilla candle is sized for a floor, and at full size
            // three of them stood on the bench like barrels next to the skull.
            PropsWorktable = cfg.Bind("6 - Altar props", "Worktable",
                "Candle_resin:-0.60,1.16,-0.30:0.45:0"
                + ";Candle_resin:-0.46,1.16,-0.38:0.38:0"
                + ";Candle_resin:0.62,1.16,-0.30:0.42:0",
                "Props for the summoning bench, which has been archived and replaced by the bindstone - kept so the shape still dresses correctly if it is ever put back in AltarShapes. Only the candles: a vanilla item prefab mounted as decoration keeps the sparkle it uses to be findable on the ground.");

            // The bindstone's shelf runs across the back of its crown, 1.20 up and 0.27
            // back. The skeleton trophy stands between the two candles, where the model
            // leaves a bare plinth for it - the skull is not modelled, because one built
            // out of spheres never stopped looking like a pile of spheres and the game
            // ships a hand-made one.
            PropsBindstone = cfg.Bind("6 - Altar props", "Bindstone",
                "Candle_resin:-0.17,0.52,-0.73:0.45:0"
                + ";Candle_resin:0.17,0.52,-0.73:0.42:0",
                "Props added to the bindstone, on the flat capstone across the front of its kerb.");

            // Necromancy: corpse-light, guttering candles and banners over the howe.
            PropsBarrow = cfg.Bind("6 - Altar props", "Barrow",
                "piece_groundtorch_green:-1.55,0,-1.75:0.9:0"
                + ";piece_groundtorch_green:1.55,0,-1.75:0.9:0"
                + ";Candle_resin:-0.55,0.95,-0.45:1:0"
                + ";Candle_resin:0.55,0.95,-0.45:1:0"
                + ";piece_banner07:-2.30,0,1.15:0.9:0"
                + ";piece_banner07:2.30,0,1.15:0.9:0"
                + ";itemstand:0,0.95,0.55:1:180",
                "Props added to the barrow bindstone.");

            AltarParts = cfg.Bind("2 - Thralls", "AltarParts",
                "stone_floor_2x2:0,0,0:0.62:0"
                + ";stone_floor_2x2:0,0.18,0:0.4:45"
                + ";guard_stone:0,0.3,0:1.05:0"
                + ";stone_pillar:-1.15,0,-0.15:0.7:0"
                + ";stone_pillar:1.15,0,-0.15:0.7:0"
                + ";piece_groundtorch:-1.15,1.35,-0.15:0.8:0"
                + ";piece_groundtorch:1.15,1.35,-0.15:0.8:0",
                "Fallback for the bindstone itself, used only if its model file is missing from disk. Each part is prefab:x,y,z:scale:yaw, separated by semicolons. Parts that do not exist are skipped. Empty leaves the altar wearing the plain borrowed model.");

            BaseWorkSlots = cfg.Bind("2 - Thralls", "BaseWorkSlots", 5,
                "How many thralls can be working at once. This release gives five flat and offers no way to raise it: with a single breed and no bindstone upgrades there is nothing to earn, and a crew that starts at one is a mod you cannot see working until you have built something else. Set below MaxWorkSlots to make station extensions matter again.");
            MaxWorkSlots = cfg.Bind("2 - Thralls", "MaxWorkSlots", 5,
                "Hard ceiling on thralls working at once. Equal to BaseWorkSlots by default, which is what makes the crew a flat five: the count is clamped between the two, so with both at five nothing built nearby can add to it.");
            SlotSearchRange = cfg.Bind("2 - Thralls", "SlotSearchRange", 40f,
                "How far from the bindstone a station extension counts towards your work slots. Dormant while BaseWorkSlots equals MaxWorkSlots, since the clamp discards whatever it finds.");

            RequireTools = cfg.Bind("5 - Tools", "RequireTools", true,
                "A thrall must be handed a tool before it will chop, mine or farm. Give it one from its page in the bindstone panel. Turn off to let them work bare handed as they used to.");
            ToolsChop = cfg.Bind("5 - Tools", "ChopTools",
                "AxeStone,AxeFlint,AxeBronze,AxeIron,AxeBlackMetal,AxeJotunBane,AxeBerzerkr",
                "Items accepted as a chopping tool, by prefab name.");
            ToolsMine = cfg.Bind("5 - Tools", "MineTools",
                "PickaxeAntler,PickaxeStone,PickaxeBronze,PickaxeIron,PickaxeBlackMetal",
                "Items accepted as a mining tool, by prefab name.");
            ToolsFarm = cfg.Bind("5 - Tools", "FarmTools", "Cultivator,Hoe",
                "Items accepted as a farming tool, by prefab name.");

            ShowTools = cfg.Bind("5 - Tools", "ShowTools", true,
                "Put the right tool in a thrall's hand for its job. Cosmetic only - it does not change what they hit for.");
            ToolChop = cfg.Bind("5 - Tools", "ChopTool", "AxeBronze",
                "Item prefab shown when chopping. AxeStone, AxeFlint, AxeBronze, AxeIron, AxeBlackMetal, AxeJotunBane.");
            ToolMine = cfg.Bind("5 - Tools", "MineTool", "PickaxeBronze",
                "Item prefab shown when mining. PickaxeAntler, PickaxeStone, PickaxeBronze, PickaxeIron, PickaxeBlackMetal.");
            ToolFarm = cfg.Bind("5 - Tools", "FarmTool", "Cultivator",
                "Item prefab shown when farming.");
            ToolBuild = cfg.Bind("5 - Tools", "BuildTool", "Hammer",
                "Item prefab shown when building or repairing.");

            // AltarIsStation, AltarStationCopyFrom, AltarStationRange, UpgradeLinks,
            // UpgradeLinkFrom and UpgradeLinkHeight were bound here.
            //
            // All six drove code that was never called. AltarStation would have made the
            // altar a real CraftingStation and hung the upgrades off it as extensions;
            // UpgradeLink was the earlier, hand-rolled version of the same connection line.
            // Neither was ever wired to anything, so both were settings for behaviour the
            // mod did not have - and the station route is the one to stay away from
            // regardless, because CraftingStation is an Interactable and would take the
            // altar's use key, which is now how you open its ledger.

            Verbose = cfg.Bind("4 - Misc", "VerboseLogging", false, "Chatty logs, for when something misbehaves.");

            TestMode = cfg.Bind("4 - Misc", "TestMode", false,
                "Makes the bindstone and every upgrade cost one wood, so all four tiers can be "
                + "built and looked at without four biomes of progression behind them. "
                + "Turn it off before playing for real - it is announced in the log on "
                + "startup so it is hard to leave on by accident.");
        }

        /// <summary>Parses RecruitCost into prefab/amount pairs. Malformed entries are skipped, not fatal.</summary>
        public static List<KeyValuePair<string, int>> ParseCost()
        {
            var result = new List<KeyValuePair<string, int>>();
            var raw = RecruitCost.Value;
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var part in raw.Split(','))
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

        public static string[] ExtraMineableNames()
        {
            var raw = MineablePrefabs.Value ?? "";
            var parts = raw.Split(',');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim().ToLowerInvariant();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }
    }
}
