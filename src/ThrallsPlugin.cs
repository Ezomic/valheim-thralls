using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ezomic.valheim.core", BepInDependency.DependencyFlags.HardDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // Thralls are creatures whose AI and ZDOs the server owns once nobody is nearby, and the
    // prefabs must resolve there or ZNetScene discards them.
    public class ThrallsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.thralls";
        public const string PluginName = "Thralls";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Robbin Thijssen";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private float _scanTimer;

        private void Awake()
        {
            Log = Logger;
            ThrallConfig.Bind(Config);
            // Everyone, not HostOnly. Both ends have to agree about this mod, and the
            // disagreement is silent when they do not: a client that cannot resolve a prefab
            // hash discards the ZDO rather than erroring - destroying what is already standing
            // in the world - and item data that differs desyncs inventories.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config);

            _harmony = new Harmony(PluginGuid);
            // Named one class at a time on purpose. PatchAll(Type) applies only the class
            // it is handed, so every patch class has to be listed here - which is easy to
            // forget, and a patch that is simply never applied fails silently.
            _harmony.PatchAll(typeof(HoverPatches));
            _harmony.PatchAll(typeof(DepotHoverPatch));
            _harmony.PatchAll(typeof(UnlockNotices));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");

            if (ThrallConfig.TestMode.Value)
                Log.LogWarning("TEST MODE: the altar and every upgrade cost one wood. "
                               + "Turn TestMode off in the config before playing for real.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void OnGUI()
        {
            AltarUI.Draw();
            ThrallTalk.Draw();
        }

        private void Update()
        {
            // Kept as a safety net: the real registration happens on ZNetScene.Awake, well
            // before any saved altar is rebuilt from its ZDO.
            AltarPrefab.Register();

            if (Player.m_localPlayer == null || ZNetScene.instance == null) return;

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 2f)
            {
                _scanTimer = 0f;
                ThrallRegistry.AttachToExisting();
            }

            AltarPrefab.Teach();

            SiteTools.KeepAlive();
            AltarUI.Tick();
            ThrallTalk.Tick();
            Fallen.Tick();
            Resting.Tick();

            if (InputBlocked()) return;

            // Both panels swallow every key while they are open. Only the site tools are
            // left on keys, and none of them should fire at a window.
            if (AltarUI.IsOpen || ThrallTalk.IsOpen) return;

            if (Hotkey.Down(ThrallConfig.KeyTimeOfDay)) SiteTools.CycleTimeOfDay();
            else if (Hotkey.Down(ThrallConfig.KeyFlatten)) FlattenHere();
            else if (Hotkey.Down(ThrallConfig.KeyGodMode)) SiteTools.ToggleGodMode();
            else if (Hotkey.Down(ThrallConfig.KeyAltarEffects)) AltarDebug.Cycle();

            // Outside the key dispatch: the markers have to keep up with the thralls
            // whether or not anything was pressed this frame.
            ThrallMap.Update();
        }

        private static bool InputBlocked()
        {
            if (global::Console.IsVisible()) return true;
            if (Chat.instance != null && Chat.instance.HasFocus()) return true;
            if (TextInput.IsVisible()) return true;
            if (InventoryGui.IsVisible()) return true;
            if (StoreGui.IsVisible()) return true;
            if (Menu.IsVisible()) return true;
            if (Minimap.IsOpen()) return true;
            return false;
        }

        // ------------------------------------------------------------------ commands

        /// <summary>
        /// Binds one thrall of the given tier. Heads must match that tier or be better,
        /// so a golem cannot be bought with greydwarf skulls.
        /// </summary>
        internal static bool Hire(int tier, Vector3? at)
        {
            var player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null) return false;

            tier = ThrallBreed.Clamp(tier);

            if (!ThrallBreed.Unlocked(tier))
            {
                Say(ThrallBreed.NameFor(tier) + " thralls will not answer until "
                    + ThrallBreed.BossName(tier) + " has fallen.");
                return false;
            }

            if (ThrallAltar.Within(ThrallConfig.AltarRange.Value) == null)
            {
                Say("You must be at a " + ThrallConfig.AltarName.Value.ToLowerInvariant() + " to bind a thrall.");
                return false;
            }

            if (ThrallRegistry.Count() >= ThrallConfig.MaxThralls.Value)
            {
                Say("You already command " + ThrallRegistry.Count() + " thralls.");
                return false;
            }

            var prefabName = ThrallBreed.PrefabFor(tier);
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Say("Unknown creature for tier " + tier + ": " + prefabName);
                Log.LogError("Prefab '" + prefabName + "' is not in ZNetScene.");
                return false;
            }

            var inventory = player.GetInventory();
            var raise = ThrallBreed.RaiseCost(tier);
            var extra = ThrallConfig.RecruitCost.Value;

            // Checked before anything is taken, so being one bloodbag short cannot spend
            // the rest of the price first.
            if (!string.IsNullOrEmpty(raise) && !ItemCost.CanPay(inventory, raise))
            {
                Say("You are missing " + ItemCost.Missing(inventory, raise) + ".");
                return false;
            }

            if (!string.IsNullOrEmpty(extra) && !ItemCost.CanPay(inventory, extra))
            {
                Say("You also need " + ItemCost.Missing(inventory, extra) + ".");
                return false;
            }

            if (!string.IsNullOrEmpty(raise) && !ItemCost.Pay(inventory, raise))
            {
                Say("The offering was refused.");
                return false;
            }

            if (!PayExtra()) return false;

            Vector3 spot;
            if (at.HasValue) spot = at.Value;
            else if (!LookAtPoint(20f, out spot))
                spot = player.transform.position + player.transform.forward * 2f;

            Spawn(prefab, spot, tier, 1, null);
            Say(ThrallBreed.NameFor(tier) + " enters your service. Walk up to it and press use "
                + "to tell it what to do.");
            return true;
        }

        /// <summary>Brings a fallen thrall back at the rank it died with, for biome goods.</summary>
        internal static bool Resurrect(FallenThrall entry, Vector3 at)
        {
            var player = Player.m_localPlayer;
            if (player == null || entry == null || ZNetScene.instance == null) return false;

            var cost = ThrallBreed.ResurrectCost(entry.Tier);
            var inventory = player.GetInventory();

            if (!ItemCost.CanPay(inventory, cost))
            {
                Say("Still needs " + ItemCost.Missing(inventory, cost) + ".");
                return false;
            }

            var prefab = ZNetScene.instance.GetPrefab(ThrallBreed.PrefabFor(entry.Tier));
            if (prefab == null)
            {
                Say("Cannot find a body for " + entry.Name + ".");
                return false;
            }

            if (!ItemCost.Pay(inventory, cost)) return false;

            Spawn(prefab, at, entry.Tier, entry.Level, entry.Name);
            Fallen.Remove(entry);
            Say(entry.Name + " walks again.");
            return true;
        }

        /// <summary>Calls a resting thrall back into the world exactly as it was.</summary>
        internal static bool Recall(RestingThrall entry, Vector3 at)
        {
            var player = Player.m_localPlayer;
            if (player == null || entry == null || ZNetScene.instance == null) return false;

            if (ThrallRegistry.Count() >= ThrallConfig.MaxThralls.Value)
            {
                Say("You already command " + ThrallRegistry.Count() + " thralls.");
                return false;
            }

            var prefab = ZNetScene.instance.GetPrefab(ThrallBreed.PrefabFor(entry.Tier));
            if (prefab == null) return false;

            // Paid before the body exists, and only then removed from the roll: a failure
            // between those two is a thrall that is neither resting nor standing up.
            var cost = ThrallBreed.RecallCost(entry.Tier);
            var inventory = player.GetInventory();

            if (!string.IsNullOrEmpty(cost))
            {
                if (!ItemCost.CanPay(inventory, cost))
                {
                    Say("Waking " + entry.Name + " needs "
                        + ItemCost.Missing(inventory, cost) + ".");
                    return false;
                }

                if (!ItemCost.Pay(inventory, cost))
                {
                    Say("The offering was refused.");
                    return false;
                }
            }

            var go = SpawnAt(prefab, at, entry.Tier, entry.Level, entry.Name);

            // Its experience and its tool come back with it, not just its name and breed.
            var thrall = go != null ? go.GetComponent<Thrall>() : null;
            if (thrall != null) thrall.Restore(entry.Xp, entry.Tool);

            Resting.Remove(entry);
            Say(entry.Name + " answers the altar again.");
            return true;
        }

        private static void Spawn(GameObject prefab, Vector3 spot, int tier, int level, string name)
        {
            SpawnAt(prefab, spot, tier, level, name);
        }

        private static GameObject SpawnAt(GameObject prefab, Vector3 spot, int tier, int level, string name)
        {
            var player = Player.m_localPlayer;
            var go = Instantiate(prefab, spot + Vector3.up * 0.2f,
                Quaternion.LookRotation(-player.transform.forward));

            var ai = go.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.MakeTame();
                ai.SetDespawnInDay(false);
                ai.SetEventCreature(false);
            }
            var character = go.GetComponent<Character>();
            if (character != null) character.SetTamed(true);

            Thrall.Imprint(go, player.GetPlayerID(), player.GetPlayerName(), tier, level, name);
            if (go.GetComponent<Thrall>() == null) go.AddComponent<Thrall>();

            return go;
        }

        private void FlattenHere()
        {
            Vector3 spot;
            if (!LookAtPoint(40f, out spot))
            {
                Say("Look at the ground you want levelled.");
                return;
            }

            SiteTools.Flatten(spot, Mathf.Clamp(ThrallConfig.FlattenRadius.Value, 1f, 24f));
        }

        // Assign, follow, dismiss and open-the-altar all used to be keys here.
        //
        // Every one of them is now a line in a menu: the first three in the panel you get
        // by pressing use on the thrall itself, and the altar's ledger by pressing use on
        // the altar. A key that duplicates a menu entry is a second thing to keep in step
        // and a second thing to explain, and these four were the whole reason the mod
        // needed a keybind reference card.
        //
        // What went with them is the job inference - pointing at a tree meant chop, at a
        // vein meant mine - because it only ever existed to make one keypress carry two
        // decisions. The panel asks for the job outright.

        // ------------------------------------------------------------------ helpers

        /// <summary>Optional extra cost on top of the heads, from the RecruitCost setting.</summary>
        private static bool PayExtra()
        {
            var spec = ThrallConfig.RecruitCost.Value;
            if (string.IsNullOrEmpty(spec)) return true;

            var inventory = Player.m_localPlayer.GetInventory();
            if (ItemCost.CanPay(inventory, spec)) return ItemCost.Pay(inventory, spec);

            Say("You also need " + ItemCost.Missing(inventory, spec) + ".");
            return false;
        }

        /// <summary>
        /// The camera sits behind the player in third person, so a naive ray hits the player's
        /// own body first. Walk the hits in order and skip anything that is us.
        /// </summary>
        private static bool LookAtCollider(float range, out RaycastHit hit)
        {
            hit = default(RaycastHit);

            var cam = GameCamera.instance != null ? GameCamera.instance.transform
                : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return false;

            var hits = Physics.RaycastAll(cam.position, cam.forward, range, Physics.DefaultRaycastLayers);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            var player = Player.m_localPlayer;
            for (int i = 0; i < hits.Length; i++)
            {
                var candidate = hits[i];
                if (candidate.collider == null) continue;

                if (player != null)
                {
                    var body = candidate.collider.attachedRigidbody;
                    if (body != null && body.gameObject == player.gameObject) continue;
                    if (candidate.collider.transform.IsChildOf(player.transform)) continue;
                }

                hit = candidate;
                return true;
            }
            return false;
        }

        private static bool LookAtPoint(float range, out Vector3 point)
        {
            RaycastHit hit;
            if (LookAtCollider(range, out hit)) { point = hit.point; return true; }
            point = Vector3.zero;
            return false;
        }

        internal static void Say(string message)
        {
            if (Player.m_localPlayer == null) return;
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, message, 0, null);
        }
    }
}


