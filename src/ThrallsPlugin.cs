using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public class ThrallsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "robbin.valheim.thralls";
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

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(HoverPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void OnGUI()
        {
            KeybindOverlay.Draw();
            AltarUI.Draw();
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
            BuildPlans.Tick(Time.deltaTime);
            Fallen.Tick();
            Resting.Tick();

            if (InputBlocked()) return;

            if (AltarUI.IsOpen)
            {
                // While the ledger is open the only key that matters is the one that shuts it.
                if (Hotkey.Down(ThrallConfig.KeySteward)) AltarUI.Close();
                return;
            }

            if (Hotkey.Down(ThrallConfig.KeySteward)) OpenAltar();
            else if (Hotkey.Down(ThrallConfig.KeyPlan)) MarkPlan();
            else if (Hotkey.Down(ThrallConfig.KeyRecruit)) Recruit();
            else if (Hotkey.Down(ThrallConfig.KeyAssign)) Assign();
            else if (Hotkey.Down(ThrallConfig.KeyDeposit)) SetDropOff();
            else if (Hotkey.Down(ThrallConfig.KeyFollow)) ToggleFollow();
            else if (Hotkey.Down(ThrallConfig.KeyDismiss)) Dismiss();
            else if (Hotkey.Down(ThrallConfig.KeyTimeOfDay)) SiteTools.CycleTimeOfDay();
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

        /// <summary>Hotkey hiring takes the best tier the player can currently pay for.</summary>
        private void Recruit()
        {
            var inventory = Player.m_localPlayer.GetInventory();
            var price = Mathf.Max(0, ThrallConfig.HeadsPerWorker.Value);

            for (int tier = ThrallBreed.Count; tier >= 1; tier--)
            {
                if (!ThrallBreed.Unlocked(tier)) continue;
                if (price == 0 || Trophies.Count(inventory, tier) >= price)
                {
                    Hire(tier, null);
                    return;
                }
            }

            Say("You need " + price + " trophies to bind a thrall. You have "
                + Trophies.Count(inventory, 1) + ".");
        }

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
            var price = Mathf.Max(0, ThrallConfig.HeadsPerWorker.Value);
            var have = Trophies.Count(inventory, tier);

            if (price > 0 && have < price)
            {
                Say(string.Format("Needs {0} tier {1} heads or better. You have {2}.", price, tier, have));
                return false;
            }

            if (price > 0 && !Trophies.Consume(inventory, tier, price))
            {
                Say("The sacrifice was refused.");
                return false;
            }

            // The breed's own price, on top of anything RecruitCost adds.
            var raise = ThrallBreed.RaiseCost(tier);
            if (!string.IsNullOrEmpty(raise))
            {
                if (!ItemCost.CanPay(inventory, raise))
                {
                    Say("You are missing " + ItemCost.Missing(inventory, raise) + ".");
                    return false;
                }

                if (!ItemCost.Pay(inventory, raise))
                {
                    Say("The offering was refused.");
                    return false;
                }
            }

            if (!PayExtra()) return false;

            Vector3 spot;
            if (at.HasValue) spot = at.Value;
            else if (!LookAtPoint(20f, out spot))
                spot = player.transform.position + player.transform.forward * 2f;

            Spawn(prefab, spot, tier, 1, null);
            Say(ThrallBreed.NameFor(tier) + " enters your service. Point at work and press "
                + ThrallConfig.KeyAssign.Value + ".");
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

        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly AccessTools.FieldRef<Player, Player.PlacementStatus> StatusRef =
            AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");

        /// <summary>
        /// Turns the hammer's current placement preview into a build order. Reading the
        /// vanilla ghost means the spot has already passed every check the game makes,
        /// so a thrall is never sent to build somewhere the game would refuse.
        /// </summary>
        private void MarkPlan()
        {
            var player = Player.m_localPlayer;

            if (!BuildPlans.HasLedger)
            {
                Say("Build a " + ThrallConfig.AltarName.Value.ToLowerInvariant() + " first - it keeps the build orders.");
                return;
            }

            GameObject ghost;
            Player.PlacementStatus status;
            try
            {
                ghost = GhostRef(player);
                status = StatusRef(player);
            }
            catch (Exception e)
            {
                Log.LogError("Cannot read the placement ghost: " + e.Message);
                return;
            }

            if (ghost == null || !ghost.activeInHierarchy)
            {
                Say("Take out a hammer and pick a piece first.");
                return;
            }

            if (status != Player.PlacementStatus.Valid)
            {
                Say("That is not a spot the piece can go.");
                return;
            }

            if (!BuildPlans.Add(ghost.name, ghost.transform.position, ghost.transform.rotation))
            {
                Say("The altar is not listening.");
                return;
            }

            Say("Build order noted (" + BuildPlans.Count + " pending).");
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

        /// <summary>Opens the nearest altar's panel without having to walk up and press use.</summary>
        private void OpenAltar()
        {
            var altar = ThrallAltar.Within(ThrallConfig.AltarRange.Value);
            if (altar == null)
            {
                Say("No " + ThrallConfig.AltarName.Value.ToLowerInvariant() + " within reach.");
                return;
            }
            AltarUI.Toggle(altar);
        }

        private void Assign()
        {
            RaycastHit hit;
            if (!LookAtCollider(60f, out hit))
            {
                Say("Look at a tree, a rock, a bush or the ground.");
                return;
            }

            var pointed = Hovered() != null
                ? Hovered().GetComponentInParent<Thrall>()
                : hit.collider.GetComponentInParent<Thrall>();
            if (pointed != null)
            {
                Say(pointed.StatusLine());
                return;
            }

            var thrall = ThrallRegistry.Nearest(Player.m_localPlayer.transform.position,
                ThrallConfig.CommandRadius.Value, true);
            if (thrall == null)
            {
                Say("No thrall close enough to hear you.");
                return;
            }

            var job = WorkNode.JobFor(hit.collider, hit.point, thrall.Power.ToolTier);

            if (ThrallRegistry.IsWork(job) && !ThrallRegistry.HasFreeSlot(thrall))
            {
                Say(string.Format("Only {0} thralls can work at once. Build more station upgrades near the altar.",
                    ThrallAltar.Slots));
                return;
            }

            thrall.AssignJob(job, hit.point);

            if (job == ThrallJob.None)
                Say(thrall.ThrallName + " will wait here.");
            else
                Say(thrall.ThrallName + " starts " + WorkNode.JobName(job) + ".");
        }

        private void SetDropOff()
        {
            var container = LookingAt<Container>(60f);
            if (container == null)
            {
                Say("Look at a chest.");
                return;
            }

            // The whole crew, not the nearest one. Setting a chest per thrall meant
            // walking round pointing at the same box once for each of them, and there is
            // almost never a reason for two thralls to unload into different chests.
            var crew = ThrallRegistry.All;
            var set = 0;

            for (int i = 0; i < crew.Count; i++)
            {
                if (crew[i] == null) continue;
                crew[i].SetDropOff(container.transform.position);
                set++;
            }

            if (set == 0)
            {
                Say("No thralls to send here.");
                return;
            }

            Say(set == 1 ? "Your thrall will unload here." : "All " + set + " thralls will unload here.");
        }

        private void ToggleFollow()
        {
            var player = Player.m_localPlayer;
            var thrall = ThrallRegistry.Nearest(player.transform.position, ThrallConfig.CommandRadius.Value, false);
            if (thrall == null)
            {
                Say("No thrall close enough to hear you.");
                return;
            }

            thrall.ToggleFollow(player.transform.position);
            Say(thrall.Job == ThrallJob.Follow
                ? thrall.ThrallName + " follows you."
                : thrall.ThrallName + " stays put.");
        }

        private void Dismiss()
        {
            var thrall = LookingAt<Thrall>(20f);
            if (thrall == null)
            {
                Say("Look at the thrall you want to dismiss.");
                return;
            }

            var name = thrall.ThrallName;
            thrall.Dismiss();
            Say(name + " is released from service.");
        }

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

        private static readonly AccessTools.FieldRef<Player, GameObject> HoverRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_hovering");

        /// <summary>
        /// Whatever the game itself says the crosshair is on. Using this means the mod agrees
        /// with the hover text the player can see, and inherits vanilla's own handling of
        /// interact range and awkward colliders.
        /// </summary>
        private static GameObject Hovered()
        {
            if (Player.m_localPlayer == null) return null;
            try { return HoverRef(Player.m_localPlayer); }
            catch { return null; }
        }

        /// <summary>Finds a component on whatever is under the crosshair, close range first.</summary>
        private static T LookingAt<T>(float range) where T : Component
        {
            var hovered = Hovered();
            if (hovered != null)
            {
                var found = hovered.GetComponentInParent<T>();
                if (found != null) return found;
            }

            RaycastHit hit;
            if (LookAtCollider(range, out hit)) return hit.collider.GetComponentInParent<T>();
            return null;
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


