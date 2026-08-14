using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Attached at runtime to a tamed creature. Vanilla AI still does the walking and the
    /// fighting; this component decides where to walk and swings at resources on arrival.
    /// </summary>
    internal class Thrall : MonoBehaviour
    {
        public const string ZIs = "thrallIs";
        public const string ZJob = "thrallJob";
        public const string ZAnchor = "thrallAnchor";
        // thrallChest / thrallHasChest used to live here. A thrall no longer remembers a
        // drop-off at all: the depot is found from where it works, every time it needs one,
        // so there is no stored spot that can go stale when you move the store. Existing
        // saves still carry both keys; nothing reads them.
        public const string ZOwner = "thrallOwner";
        public const string ZOwnerName = "thrallOwnerName";
        public const string ZName = "thrallName";
        public const string ZTool = "thrallTool";
        public const string ZInv = "thrallInv";
        public const string ZRank = "thrallRank";
        public const string ZTier = "thrallTier";
        public const string ZXp = "thrallXp";

        private static readonly string[] Names =
        {
            "Bersi", "Hallr", "Kolli", "Ozur", "Steinn", "Thrand", "Ulfar", "Vali",
            "Asa", "Gyda", "Hild", "Ingrid", "Ranveig", "Signy", "Thora", "Yrsa"
        };

        private Character _character;
        private Humanoid _humanoid;
        private MonsterAI _ai;
        private ZNetView _nview;
        private ZSyncAnimation _anim;
        private GameObject _proxy;
        private Inventory _inventory;

        private ThrallJob _job;
        private Vector3 _anchor;
        private string _name;
        private string _ownerName;
        private string _tool = "";
        private float _xp;
        private int _tier = 1;
        private bool _deathRecorded;

        private WorkNode _target;
        private float _swingTimer;
        private float _searchTimer;
        private float _pickupTimer;
        private float _saveTimer;
        private float _stuckTimer;
        private float _lastDistance = float.MaxValue;
        private int _swingsOnTarget;
        private bool _hauling;
        private bool _warnedNoDepot;

        private PlantRecipe _sowing;
        private Vector3 _sowSpot;
        private bool _haveSowSpot;
        private float _sowTimer;
        private float _spotSearchTimer;
        private bool _restocking;
        private float _nextRestock;
        private int _lastRestockTake;
        private bool _warnedNoSeed;

        private string _currentTool;
        private WearNTear _repairTarget;
        private BuildPlan _plan;
        private bool _warnedNoMaterials;

        private readonly Dictionary<int, float> _giveUpList = new Dictionary<int, float>();

        public string ThrallName { get { return _name; } }

        /// <summary>
        /// Who summoned it. Empty for thralls bound before this was recorded, and for
        /// those the owner's name is looked up from whoever is connected instead.
        /// </summary>
        public string OwnerName
        {
            get
            {
                if (!string.IsNullOrEmpty(_ownerName)) return _ownerName;

                var id = OwnerId();
                if (id == 0L) return "";

                if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == id)
                    return Player.m_localPlayer.GetPlayerName();

                foreach (var other in Player.GetAllPlayers())
                    if (other != null && other.GetPlayerID() == id) return other.GetPlayerName();

                return "";
            }
        }
        public float Xp { get { return _xp; } }
        public string XpProgress { get { return Levels.Progress(_xp); } }

        /// <summary>Level is derived from experience, never stored separately.</summary>
        public int Rank { get { return Levels.LevelFor(_xp); } }

        public int Tier { get { return _tier; } }
        public string TierName { get { return ThrallBreed.NameFor(_tier); } }
        public WorkPower Power { get { return WorkPower.For(_tier, Rank); } }

        private float Reach
        {
            get { return ThrallConfig.HarvestRange.Value + ThrallBreed.ReachBonus(_tier); }
        }

        /// <summary>Higher ranks work faster, down to a floor so they never blur.</summary>
        public float SwingSeconds
        {
            get
            {
                var scale = 1f
                            - Mathf.Max(0, _tier - 1) * ThrallConfig.TierSpeedStep.Value
                            - Mathf.Max(0, Rank - 1) * ThrallConfig.LevelSpeedStep.Value;
                return Mathf.Max(0.25f, ThrallConfig.SwingInterval.Value * Mathf.Max(0.2f, scale));
            }
        }

        public ThrallJob Job { get { return _job; } }
        public Inventory Carrying { get { return _inventory; } }
        public bool Hauling { get { return _hauling; } }

        // ------------------------------------------------------------------ tools

        /// <summary>The prefab name of the tool it was handed, or empty.</summary>
        public string Tool { get { return _tool ?? ""; } }

        /// <summary>Which tools a job will accept, as prefab names.</summary>
        public static string ToolsFor(ThrallJob job)
        {
            switch (job)
            {
                case ThrallJob.Chop: return ThrallConfig.ToolsChop.Value;
                case ThrallJob.Mine: return ThrallConfig.ToolsMine.Value;
                case ThrallJob.Farm: return ThrallConfig.ToolsFarm.Value;
                default: return "";
            }
        }

        /// <summary>Whether a job needs a tool in hand at all. Guarding, following and
        /// hauling do not; swinging at a tree does.</summary>
        public static bool NeedsTool(ThrallJob job)
        {
            return !string.IsNullOrEmpty(ToolsFor(job));
        }

        public bool HasToolFor(ThrallJob job)
        {
            if (!ThrallConfig.RequireTools.Value) return true;
            if (!NeedsTool(job)) return true;
            if (string.IsNullOrEmpty(_tool)) return false;

            foreach (var name in ToolsFor(job).Split(','))
                if (string.Equals(name.Trim(), _tool, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        /// Takes a tool out of the player's hands and puts it in the thrall's.
        ///
        /// The item is genuinely removed from your inventory - a thrall holding a
        /// borrowed axe you also still own would make the whole requirement decorative.
        /// </summary>
        public bool GiveTool(ItemDrop.ItemData item, Inventory from)
        {
            if (item == null || from == null) return false;

            var prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (string.IsNullOrEmpty(prefab)) return false;

            // Whatever it was holding goes back, so nothing is quietly destroyed.
            ReturnTool(from);

            from.RemoveOneItem(item);

            _tool = prefab;
            _currentTool = null;
            UpdateTool();
            SaveState();

            Announce(_name + " takes the " + item.m_shared.m_name.TrimStart('$'));
            return true;
        }

        /// <summary>
        /// Puts back the experience and the tool a recalled thrall left with, so it comes
        /// back as the same worker rather than a fresh one wearing its name.
        /// </summary>
        public void Restore(float xp, string tool)
        {
            _xp = Mathf.Max(_xp, xp);
            _tool = tool ?? "";
            _currentTool = null;

            ResizeCarry();
            UpdateTool();
            SaveState();
        }

        /// <summary>Hands the tool back, if there is anywhere to put it.</summary>
        public bool ReturnTool(Inventory to)
        {
            if (string.IsNullOrEmpty(_tool)) return false;

            if (to != null && ObjectDB.instance != null)
            {
                var prefab = ObjectDB.instance.GetItemPrefab(_tool);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;

                if (drop != null && !to.AddItem(drop.m_itemData.Clone()))
                    return false;
            }

            _tool = "";
            _currentTool = null;
            UpdateTool();
            SaveState();
            return true;
        }

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            _character = GetComponent<Character>();
            _humanoid = GetComponent<Humanoid>();
            _ai = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            _anim = GetComponent<ZSyncAnimation>();
            // Opened at one slot; the real size is worked out from tier and level once
            // the ZDO has been read, and grows from there as the thrall levels.
            _inventory = new Inventory("Thrall", null, 1, 1);
        }

        private static readonly AccessTools.FieldRef<Inventory, int> PackWidth =
            AccessTools.FieldRefAccess<Inventory, int>("m_width");

        private static readonly AccessTools.FieldRef<Inventory, int> PackHeight =
            AccessTools.FieldRefAccess<Inventory, int>("m_height");

        public int PackSlots { get { return ThrallBreed.PackSlots(_tier, Rank); } }

        /// <summary>
        /// Sets the pack to the size this thrall has earned.
        ///
        /// Never shrinks below what is already in there: a thrall loaded carrying eight
        /// stacks from before this existed would otherwise have them stranded outside the
        /// grid. It gives the space back the next time it unloads.
        /// </summary>
        private void ResizeCarry()
        {
            if (_inventory == null) return;

            var slots = Mathf.Max(PackSlots, _inventory.NrOfItems());
            if (slots == _inventory.GetWidth() * _inventory.GetHeight()) return;

            try
            {
                PackWidth(_inventory) = slots;
                PackHeight(_inventory) = 1;
            }
            catch (Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not resize a thrall's pack: " + e.Message);
            }
        }

        private void Start()
        {
            if (!Valid()) return;

            _proxy = new GameObject("thrall_waypoint");
            _proxy.transform.position = transform.position;

            LoadState();

            if (_ai != null)
            {
                _ai.SetDespawnInDay(false);
                _ai.SetEventCreature(false);
            }
            if (_character != null) _character.SetTamed(true);

            UpdateTool();
            ThrallRegistry.Register(this);
        }

        private void OnDestroy()
        {
            ThrallRegistry.Unregister(this);
            if (_proxy != null) Destroy(_proxy);
        }

        private bool Valid()
        {
            return _nview != null && _nview.IsValid() && _character != null && _ai != null;
        }

        /// <summary>Marks a freshly spawned creature as a thrall. Called once, by the recruiter.</summary>
        public static void Imprint(GameObject go, long ownerId, string ownerName,
            int tier, int level, string name)
        {
            var nview = go.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            var zdo = nview.GetZDO();
            zdo.Set(ZIs, true);
            zdo.Set(ZOwner, ownerId);

            // The summoner's name is written down here rather than looked up later. An id
            // only resolves to a name while that player is connected, and a thrall should
            // still say who raised it when its owner is offline or on someone else's world.
            zdo.Set(ZOwnerName, string.IsNullOrEmpty(ownerName) ? "" : ownerName);
            zdo.Set(ZJob, (int)ThrallJob.None);
            zdo.Set(ZTier, ThrallBreed.Clamp(tier));
            zdo.Set(ZRank, Mathf.Max(1, level));
            zdo.Set(ZXp, Levels.XpFor(Mathf.Max(1, level)));
            zdo.Set(ZName, string.IsNullOrEmpty(name)
                ? Names[Mathf.Abs(zdo.m_uid.GetHashCode()) % Names.Length]
                : name);
        }

        public static bool IsThrall(ZNetView nview)
        {
            return nview != null && nview.IsValid() && nview.GetZDO().GetBool(ZIs, false);
        }

        // ------------------------------------------------------------------ state

        private void LoadState()
        {
            var zdo = _nview.GetZDO();
            _job = (ThrallJob)zdo.GetInt(ZJob, 0);
            _anchor = zdo.GetVec3(ZAnchor, transform.position);
            _name = zdo.GetString(ZName, "Thrall");
            _ownerName = zdo.GetString(ZOwnerName, "");
            _tool = zdo.GetString(ZTool, "");
            _tier = ThrallBreed.Clamp(zdo.GetInt(ZTier, 1));

            // Older thralls stored a level with no experience behind it; seed them at the
            // floor of that level so nothing is demoted by the change.
            _xp = zdo.GetFloat(ZXp, -1f);
            if (_xp < 0f) _xp = Levels.XpFor(Mathf.Max(1, zdo.GetInt(ZRank, 1)));

            var blob = zdo.GetByteArray(ZInv, null);
            if (blob != null && blob.Length > 0)
            {
                try { _inventory.Load(new ZPackage(blob)); }
                catch (Exception e) { ThrallsPlugin.Log.LogWarning("Could not restore thrall pack: " + e.Message); }
            }

            // After the tier, the level and whatever it was already carrying are known.
            ResizeCarry();
        }

        /// <summary>
        /// Stars, so a veteran looks like one.
        ///
        /// A level twenty thrall and a fresh one were identical to look at, and the only
        /// way to tell them apart was to open the panel. The game already has a vocabulary
        /// for "this one is dangerous" - the star over a creature's name - so a thrall
        /// borrows it rather than inventing a new cue.
        ///
        /// Character.SetLevel is not only cosmetic: it calls SetupMaxHealth and writes to
        /// the ZDO, so a starred thrall is genuinely tougher and stays that way. That is
        /// wanted here - a thrall that has worked for hours should be harder to lose - but
        /// it does mean rank is now a balance lever as well as a label.
        /// </summary>
        private void ShowRank()
        {
            if (_character == null) return;

            var rank = Rank;
            var stars = 0;
            if (rank >= Mathf.Max(1, ThrallConfig.TwoStarRank.Value)) stars = 2;
            else if (rank >= Mathf.Max(1, ThrallConfig.OneStarRank.Value)) stars = 1;

            // Valheim counts level 1 as no star, so the level is one above the star count.
            var want = stars + 1;
            if (_character.GetLevel() == want) return;

            _character.SetLevel(want);
            ThrallsPlugin.Log.LogInfo(string.Format("{0} reached rank {1}: {2} star(s).",
                _name ?? "Thrall", rank, stars));
        }

        private void SaveState()
        {
            if (!Valid() || !_nview.IsOwner()) return;
            var zdo = _nview.GetZDO();
            zdo.Set(ZJob, (int)_job);
            zdo.Set(ZAnchor, _anchor);
            zdo.Set(ZTool, _tool ?? "");
            zdo.Set(ZName, _name ?? "Thrall");
            zdo.Set(ZRank, Rank);
            zdo.Set(ZTier, _tier);
            zdo.Set(ZXp, _xp);

            ShowRank();

            var pkg = new ZPackage();
            _inventory.Save(pkg);
            zdo.Set(ZInv, pkg.GetArray());
        }

        // ------------------------------------------------------------------ orders

        public void AssignJob(ThrallJob job, Vector3 anchor)
        {
            _job = job;
            _anchor = anchor;
            _target = null;
            _hauling = false;
            _restocking = false;
            _warnedNoDepot = false;
            _sowing = null;
            _haveSowSpot = false;
            _nextRestock = 0f;
            _warnedNoSeed = false;
            _warnedNoMaterials = false;
            _repairTarget = null;
            _plan = null;
            _giveUpList.Clear();
            UpdateTool();
            SaveState();
        }

        /// <summary>Items in the pack that are not seed, i.e. Actually worth a trip to the chest.</summary>
        private int CarriedProduce()
        {
            var items = _inventory.GetAllItems();
            var count = 0;
            for (int i = 0; i < items.Count; i++)
                if (_job != ThrallJob.Farm || !FarmPlanner.IsSeed(items[i])) count += items[i].m_stack;
            return count;
        }

        /// <summary>
        /// Experience for a job done. Thralls learn their trade by working it - there is
        /// nothing to buy, so a thrall you actually use is the one that gets good.
        /// </summary>
        private void AddXp(float amount)
        {
            if (amount <= 0f) return;
            if (Rank >= Levels.MaxLevel) return;

            var before = Rank;
            _xp += amount;

            if (Rank == before) return;

            UpdateTool();

            // A level can bring another pack slot with it.
            var was = _inventory != null ? _inventory.GetWidth() * _inventory.GetHeight() : 0;
            ResizeCarry();
            var now = _inventory != null ? _inventory.GetWidth() * _inventory.GetHeight() : 0;

            SaveState();
            Announce(string.Format("{0} the {1} reaches level {2}.{3}",
                _name, TierName, Rank,
                now > was ? " Its pack grows to " + now + " slots." : ""));
        }

        public void Rename(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _name = name.Trim();
            SaveState();
        }

        /// <summary>Puts the thrall back to work where it already stands.</summary>
        public void ReassignHere(ThrallJob job)
        {
            AssignJob(job, transform.position);
        }

        /// <summary>Why it cannot take a job, or empty if it can.</summary>
        public string Refusal(ThrallJob job)
        {
            if (HasToolFor(job)) return "";

            return _name + " needs " + ToolWord(job) + " before it can do that.";
        }

        private static string ToolWord(ThrallJob job)
        {
            switch (job)
            {
                case ThrallJob.Chop: return "an axe";
                case ThrallJob.Mine: return "a pickaxe";
                case ThrallJob.Farm: return "a cultivator";
                default: return "a tool";
            }
        }

        public void SummonTo(Vector3 pos)
        {
            AssignJob(ThrallJob.None, pos);
        }

        /// <summary>
        /// The depot this thrall hauls to, or null if there is none in reach.
        ///
        /// Measured from the work base rather than from the thrall's feet, so a crew strung
        /// out along a treeline all agree on one store instead of each picking whatever is
        /// nearest to the tree it happens to be felling - and so the answer does not change
        /// while a thrall is walking towards it.
        ///
        /// Worked out fresh each time rather than remembered. A stored spot is a spot that
        /// goes stale the moment you take the depot down and rebuild it somewhere better,
        /// and the previous version of this - a chest position saved on the thrall - is
        /// exactly what made moving your storage a chore.
        /// </summary>
        public ThrallDepot DepotFor()
        {
            return ThrallDepot.Nearest(_anchor, Mathf.Max(1f, ThrallConfig.DepotRange.Value));
        }

        public bool HasDropOff { get { return DepotFor() != null; } }

        /// <summary>
        /// Moves where it works from, keeping the job it already has.
        ///
        /// AssignJob would do this too, but it also clears the target, the sowing plan and
        /// the give-up list - which is right when you are giving a new order and wrong when
        /// you are only shifting the pitch. What does have to go is the current target: it
        /// was chosen inside a radius that has now moved, and a thrall that keeps walking
        /// back to the old one has not really been moved at all.
        /// </summary>
        public void MoveBase(Vector3 pos)
        {
            _anchor = pos;
            _target = null;
            _repairTarget = null;
            _plan = null;
            _haveSowSpot = false;
            _searchTimer = 0f;
            _warnedNoDepot = false;
            _warnedNoSeed = false;
            _warnedNoMaterials = false;

            // A thrall told to work somewhere else stops following, or it would take the
            // order and then walk straight back to your heel.
            if (_job == ThrallJob.Follow) _job = ThrallJob.None;

            SaveState();
        }

        /// <summary>Where it works from, for the panel and the hover text.</summary>
        public Vector3 Base { get { return _anchor; } }

        /// <summary>
        /// Sends it to unload now, rather than when its pack happens to fill.
        ///
        /// Useful before a move: a thrall re-based across the valley with a half-full pack
        /// would otherwise carry an afternoon's ore to the far side and only then notice
        /// there is no depot in reach. Returns false, and does nothing, when there is
        /// nowhere to take it.
        /// </summary>
        public bool SendToDepot()
        {
            if (DepotFor() == null) return false;

            _hauling = true;
            _restocking = false;
            _target = null;
            return true;
        }

        public void ToggleFollow(Vector3 fallbackAnchor)
        {
            if (_job == ThrallJob.Follow)
            {
                _job = ThrallJob.None;
                _anchor = transform.position;
            }
            else
            {
                _job = ThrallJob.Follow;
                _anchor = fallbackAnchor;
            }
            _target = null;
            SaveState();
        }

        /// <summary>
        /// Sends it away for good. The pack is handed in first, and if there is an altar
        /// to keep the roll, the thrall itself is put on the resting list rather than
        /// thrown out - so calling it back gets the same one, name, level, tool and all.
        /// </summary>
        public void Dismiss(bool keep = true)
        {
            ReturnPack();

            if (keep && Resting.Rest(_name, _tier, _xp, _tool))
                Announce(_name + " steps back into the altar's keeping.");

            if (_nview != null && _nview.IsValid())
            {
                _nview.ClaimOwnership();
                _nview.Destroy();
            }
        }

        /// <summary>
        /// A dismissed thrall hands its work in at the depot rather than tipping it on the
        /// floor. Only what will not fit - or what has no depot to go to - gets dropped.
        /// </summary>
        private void ReturnPack()
        {
            if (_inventory.NrOfItems() == 0) return;

            var depot = DepotFor();
            if (depot != null && depot.Usable)
            {
                var nview = depot.GetComponent<ZNetView>();

                if (nview != null && nview.IsValid())
                {
                    nview.ClaimOwnership();
                    var store = depot.Store.GetInventory();
                    if (store != null)
                    {
                        var items = new List<ItemDrop.ItemData>(_inventory.GetAllItems());
                        foreach (var item in items)
                        {
                            if (!store.CanAddItem(item, item.m_stack)) continue;
                            if (!store.AddItem(item)) continue;
                            _inventory.RemoveItem(item);
                        }
                    }
                }
            }

            if (_inventory.NrOfItems() == 0)
            {
                Announce(_name + " left its load in the depot.");
                return;
            }

            DropEverything();
        }

        public string StatusLine()
        {
            var carried = _inventory.NrOfItems();
            var slots = _inventory.GetWidth() * _inventory.GetHeight();
            var what = _hauling ? "hauling to the chest" : WorkNode.JobName(_job);
            return string.Format("{0} the {1}, level {2} [{3} xp] - {4} ({5}/{6} slots)",
                _name, TierName, Rank, XpProgress, what, carried, slots);
        }

        // ------------------------------------------------------------------ think

        private void Update()
        {
            if (!Valid() || !_nview.IsOwner()) return;

            if (_character.IsDead())
            {
                // Note who fell so the steward can offer to raise them again.
                if (!_deathRecorded)
                {
                    _deathRecorded = true;
                    Fallen.Record(_name, _tier, Rank, transform.position);
                    Announce(_name + " has fallen.");
                }
                return;
            }

            var dt = Time.deltaTime;

            _saveTimer += dt;
            if (_saveTimer > 30f) { _saveTimer = 0f; SaveState(); }

            // Only a working thrall picks things up, so an idle one never pockets
            // something you dropped on purpose.
            if (_job == ThrallJob.Chop || _job == ThrallJob.Mine
                || _job == ThrallJob.Gather || _job == ThrallJob.Farm)
            {
                _pickupTimer += dt;
                if (_pickupTimer > 0.75f) { _pickupTimer = 0f; CollectDrops(); }
            }

            switch (_job)
            {
                case ThrallJob.Follow:
                    FollowOwner();
                    return;
                case ThrallJob.None:
                    WalkTo(_anchor);
                    return;
            }

            if (_hauling) { DoHaul(dt); return; }

            if (_inventory.GetEmptySlots() <= 0)
            {
                _hauling = true;
                return;
            }

            if (!ThrallConfig.WorkAtNight.Value && IsNight())
            {
                // Stands the night out at the depot if there is one, which puts the crew in
                // one place at dusk rather than scattered across the treeline.
                var shelter = DepotFor();
                WalkTo(shelter != null ? shelter.transform.position : _anchor);
                return;
            }

            if (_job == ThrallJob.Repair) { DoRepair(dt); return; }
            if (_job == ThrallJob.Build) { DoBuild(dt); return; }

            DoWork(dt);

            // A farmer sows in whatever time is left over from harvesting.
            if (_job == ThrallJob.Farm && _target == null) DoSow(dt);
        }

        // ------------------------------------------------------------------ upkeep

        private void DoRepair(float dt)
        {
            // Unity's null check also covers a piece that has since been destroyed.
            if (_repairTarget != null && _repairTarget.GetHealthPercentage() >= 1f)
                _repairTarget = null;

            if (_repairTarget == null)
            {
                _searchTimer -= dt;
                if (_searchTimer > 0f) { WalkTo(_anchor); return; }
                _searchTimer = 2f;

                _repairTarget = FindDamagedPiece();
                if (_repairTarget == null) { WalkTo(_anchor); return; }
                _stuckTimer = 0f;
                _lastDistance = float.MaxValue;
            }

            var spot = _repairTarget.transform.position;
            WalkTo(spot);

            var flat = transform.position - spot;
            flat.y = 0f;
            var dist = flat.magnitude;
            if (dist > Reach)
            {
                if (dist >= _lastDistance - 0.25f) _stuckTimer += dt; else _stuckTimer = 0f;
                _lastDistance = dist;
                if (_stuckTimer > 12f) { _repairTarget = null; }
                return;
            }

            _swingTimer -= dt;
            if (_swingTimer > 0f) return;
            _swingTimer = Mathf.Max(1.1f, SwingSeconds);

            FaceTarget(spot);
            PlaySwing();

            // Repair refuses more than once a second and when already whole, so this
            // naturally ends when the piece is sound again.
            if (_repairTarget.Repair()) AddXp(ThrallConfig.XpPerRepair.Value);
            else _repairTarget = null;
        }

        private WearNTear FindDamagedPiece()
        {
            var hits = Physics.OverlapSphere(_anchor, ThrallConfig.WorkRadius.Value,
                Physics.DefaultRaycastLayers);

            WearNTear best = null;
            var worst = 1f;

            for (int i = 0; i < hits.Length; i++)
            {
                var wnt = hits[i].GetComponentInParent<WearNTear>();
                if (wnt == null) continue;

                var health = wnt.GetHealthPercentage();
                if (health >= 1f || health >= worst) continue;

                worst = health;
                best = wnt;
            }
            return best;
        }

        // ------------------------------------------------------------------ building

        private void DoBuild(float dt)
        {
            if (_plan != null && !BuildPlans.All.Contains(_plan)) _plan = null;

            if (_plan == null)
            {
                _searchTimer -= dt;
                if (_searchTimer > 0f) { WalkTo(_anchor); return; }
                _searchTimer = 1.5f;

                _plan = BuildPlans.Nearest(_anchor, ThrallConfig.WorkRadius.Value);
                if (_plan == null) { WalkTo(_anchor); return; }

                _warnedNoMaterials = false;
                _stuckTimer = 0f;
                _lastDistance = float.MaxValue;
            }

            if (!HasMaterialsFor(_plan))
            {
                if (HasDropOff && !_restocking && Time.time >= _nextRestock)
                {
                    _restocking = true;
                    _hauling = true;
                }
                else if (!_warnedNoMaterials)
                {
                    _warnedNoMaterials = true;
                    Announce(_name + " lacks materials for " + PlanLabel(_plan) + ".");
                }
                return;
            }

            WalkTo(_plan.Position);

            var dist = Vector3.Distance(transform.position, _plan.Position);
            if (dist > Reach)
            {
                if (dist >= _lastDistance - 0.25f) _stuckTimer += dt; else _stuckTimer = 0f;
                _lastDistance = dist;
                if (_stuckTimer > 15f)
                {
                    // Cannot get to it. Leave the order standing and try a different one.
                    _plan = null;
                }
                return;
            }

            _swingTimer -= dt;
            if (_swingTimer > 0f) return;
            _swingTimer = Mathf.Max(0.5f, SwingSeconds);

            FaceTarget(_plan.Position);
            PlaySwing();
            Raise(_plan);
        }

        private void Raise(BuildPlan plan)
        {
            var piece = plan.Piece;
            var player = Player.m_localPlayer;
            if (piece == null || player == null)
            {
                BuildPlans.Remove(plan);
                _plan = null;
                return;
            }

            if (!SpendMaterialsFor(plan)) { _plan = null; return; }

            // The vanilla routine handles creator, private areas, wear and the placement effects.
            player.PlacePiece(piece, plan.Position, plan.Rotation, false);
            AddXp(ThrallConfig.XpPerBuild.Value);

            BuildPlans.Remove(plan);
            _plan = null;
            SaveState();
        }

        private static string PlanLabel(BuildPlan plan)
        {
            var piece = plan.Piece;
            return piece != null && !string.IsNullOrEmpty(piece.m_name) ? piece.m_name : plan.PrefabName;
        }

        private bool HasMaterialsFor(BuildPlan plan)
        {
            var piece = plan.Piece;
            if (piece == null || piece.m_resources == null) return true;

            foreach (var req in piece.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_amount <= 0) continue;
                var name = req.m_resItem.m_itemData.m_shared.m_name;
                if (CountByName(name) < req.m_amount) return false;
            }
            return true;
        }

        private bool SpendMaterialsFor(BuildPlan plan)
        {
            var piece = plan.Piece;
            if (piece == null || piece.m_resources == null) return true;
            if (!HasMaterialsFor(plan)) return false;

            foreach (var req in piece.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_amount <= 0) continue;
                RemoveByName(req.m_resItem.m_itemData.m_shared.m_name, req.m_amount);
            }
            return true;
        }

        private int CountByName(string sharedName)
        {
            var total = 0;
            var items = _inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i].m_shared != null && items[i].m_shared.m_name == sharedName)
                    total += items[i].m_stack;
            return total;
        }

        private void RemoveByName(string sharedName, int amount)
        {
            var remaining = amount;
            var items = new List<ItemDrop.ItemData>(_inventory.GetAllItems());
            foreach (var item in items)
            {
                if (remaining <= 0) break;
                if (item.m_shared == null || item.m_shared.m_name != sharedName) continue;

                var take = Mathf.Min(remaining, item.m_stack);
                _inventory.RemoveItem(item, take);
                remaining -= take;
            }
        }

        /// <summary>Sows one seed at a time from the pack, fetching more from the chest when empty.</summary>
        private void DoSow(float dt)
        {
            if (_sowing == null)
            {
                _sowing = NextSeedInPack();
                _haveSowSpot = false;
            }

            if (_sowing == null)
            {
                // Out of seed. Visit the depot to restock, but only if a previous trip was
                // not already a wasted journey - otherwise it paces to the depot forever.
                if (HasDropOff && !_restocking && Time.time >= _nextRestock)
                {
                    _restocking = true;
                    _hauling = true;
                }
                else if (!_warnedNoSeed)
                {
                    _warnedNoSeed = true;
                    Announce(_name + " has no seed to sow.");
                }
                return;
            }

            _warnedNoSeed = false;

            if (!_haveSowSpot)
            {
                _spotSearchTimer -= dt;
                if (_spotSearchTimer > 0f) return;
                _spotSearchTimer = 2f;

                if (!FarmPlanner.FindSpot(_sowing, _anchor, ThrallConfig.WorkRadius.Value, out _sowSpot))
                {
                    // Field is full, or the soil is not tilled. Nothing to do but wait.
                    return;
                }
                _haveSowSpot = true;
            }

            WalkTo(_sowSpot);

            if (Vector3.Distance(transform.position, _sowSpot) > ThrallConfig.HarvestRange.Value) return;

            _sowTimer -= dt;
            if (_sowTimer > 0f) return;
            _sowTimer = Mathf.Max(0.3f, SwingSeconds);

            // The ground may have been built on or planted since the spot was chosen.
            if (!FarmPlanner.CanPlantAt(_sowing, _sowSpot))
            {
                _haveSowSpot = false;
                return;
            }

            if (!SpendSeed(_sowing))
            {
                _sowing = null;
                _haveSowSpot = false;
                return;
            }

            FaceTarget(_sowSpot);
            PlaySwing();
            FarmPlanner.Sow(_sowing, _sowSpot, OwnerId());
            AddXp(ThrallConfig.XpPerPlant.Value);
            _haveSowSpot = false;

            if (SeedCount(_sowing) <= 0) _sowing = null;
        }

        private PlantRecipe NextSeedInPack()
        {
            var items = _inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var recipe = FarmPlanner.RecipeFor(items[i]);
                if (recipe != null && items[i].m_stack >= recipe.SeedAmount) return recipe;
            }
            return null;
        }

        private int SeedCount(PlantRecipe recipe)
        {
            var total = 0;
            var items = _inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i].m_shared != null && items[i].m_shared.m_name == recipe.SeedSharedName)
                    total += items[i].m_stack;
            return total;
        }

        private bool SpendSeed(PlantRecipe recipe)
        {
            var items = new List<ItemDrop.ItemData>(_inventory.GetAllItems());
            foreach (var item in items)
            {
                if (item.m_shared == null || item.m_shared.m_name != recipe.SeedSharedName) continue;
                if (item.m_stack < recipe.SeedAmount) continue;
                return _inventory.RemoveItem(item, recipe.SeedAmount);
            }
            return false;
        }

        private long OwnerId()
        {
            return _nview != null && _nview.IsValid() ? _nview.GetZDO().GetLong(ZOwner, 0L) : 0L;
        }

        private void DoWork(float dt)
        {
            if (_target != null && !_target.Alive)
            {
                // Give the drops a moment to land before moving on.
                AddXp(ThrallConfig.XpPerHarvest.Value);
                _target = null;
                _pickupTimer = 0.6f;
            }

            if (_target == null)
            {
                _searchTimer -= dt;
                if (_searchTimer > 0f) { WalkTo(_anchor); return; }
                _searchTimer = 1.5f;

                ExpireGiveUps();
                _target = WorkNode.FindNearest(_job, _anchor, ThrallConfig.WorkRadius.Value,
                    GiveUpIds(), Power.ToolTier);
                if (_target == null)
                {
                    // Nothing left in range: bring the haul home, then stand by. Seed in a
                    // farmer's pack is stock in hand, not produce, so it is not worth a trip.
                    if (CarriedProduce() > 0 && HasDropOff) _hauling = true;
                    else WalkTo(_anchor);
                    return;
                }

                _swingsOnTarget = 0;
                _stuckTimer = 0f;
                _lastDistance = float.MaxValue;
            }

            var aim = _target.AimPoint;
            WalkTo(_target.WalkPoint);

            // Measured along the ground, so a tall tree is not "too far" just for being tall.
            var dist = _target.GroundDistanceFrom(transform.position);
            if (dist > Reach)
            {
                // Not closing the gap? Something is in the way. Pick a different target.
                if (dist >= _lastDistance - 0.25f) _stuckTimer += dt;
                else _stuckTimer = 0f;
                _lastDistance = dist;

                if (_stuckTimer > 12f)
                {
                    GiveUpOn(_target, string.Format("cannot reach (stalled {0:0.0}m away)", dist));
                    _target = null;
                }
                return;
            }

            _stuckTimer = 0f;
            _swingTimer -= dt;
            if (_swingTimer > 0f) return;
            _swingTimer = SwingSeconds;

            FaceTarget(aim);
            PlaySwing();
            _target.Work(_character, _humanoid, Power);
            _swingsOnTarget++;
            AddXp(ThrallConfig.XpPerSwing.Value);

            if (ThrallConfig.Verbose.Value)
                ThrallsPlugin.Log.LogInfo(string.Format("{0} swing {1} at {2} ({3:0.0}m)",
                    _name, _swingsOnTarget, _target.Label, dist));

            if (_swingsOnTarget > 80)
            {
                GiveUpOn(_target, "taking too long");
                _target = null;
            }
        }

        private void DoHaul(float dt)
        {
            var depot = DepotFor();

            if (depot == null)
            {
                // Said once, not every frame: a crew with nowhere to unload would otherwise
                // fill the screen with the same line five times over.
                if (!_warnedNoDepot)
                {
                    _warnedNoDepot = true;
                    Announce(_name + " has a full pack and no depot within "
                             + Mathf.RoundToInt(ThrallConfig.DepotRange.Value)
                             + "m of where it works.");
                }
                _hauling = false;
                _restocking = false;
                WalkTo(_anchor);
                return;
            }

            _warnedNoDepot = false;

            var spot = depot.transform.position;
            WalkTo(spot);

            if (Vector3.Distance(transform.position, spot) > ThrallConfig.DepositRange.Value) return;

            // Torn down while it was walking. Not an error worth announcing - the next pass
            // finds the next nearest depot, or says there is none.
            if (!depot.Usable)
            {
                _hauling = false;
                return;
            }

            Unload(depot.Store);

            // A depot that had nothing we needed means no point walking back for a while.
            if ((_job == ThrallJob.Farm || _job == ThrallJob.Build) && _lastRestockTake == 0)
                _nextRestock = Time.time + 120f;

            _hauling = false;
            _restocking = false;
            _target = null;
            _searchTimer = 0f;
            _sowing = null;
            _haveSowSpot = false;
            SaveState();
        }

        private void FollowOwner()
        {
            var player = Player.m_localPlayer;
            if (player == null) { WalkTo(_anchor); return; }
            WalkTo(player.transform.position);
        }

        // ------------------------------------------------------------------ acting

        /// <summary>Parks the invisible waypoint the vanilla follow-AI walks toward.</summary>
        private void WalkTo(Vector3 destination)
        {
            if (_proxy == null) return;
            _proxy.transform.position = destination;
            if (_ai != null && _ai.GetFollowTarget() != _proxy) _ai.SetFollowTarget(_proxy);
        }

        private void FaceTarget(Vector3 aim)
        {
            var flat = aim - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 0.5f);
        }

        /// <summary>
        /// Puts the job's tool in the thrall's hand. This is VisEquipment only - the visual
        /// layer - so it cannot disturb attack selection or the animator on a non-player.
        /// </summary>
        private void UpdateTool()
        {
            if (!ThrallConfig.ShowTools.Value) return;

            var tool = ToolForJob();
            if (string.IsNullOrEmpty(tool) || tool == _currentTool) return;

            var vis = GetComponent<VisEquipment>();
            if (vis == null) return;

            if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(tool) == null)
            {
                ThrallsPlugin.Log.LogWarning("No such tool item '" + tool + "', leaving hands as they were.");
                return;
            }

            _currentTool = tool;
            vis.SetRightItem(tool);
        }

        private string ToolForJob()
        {
            // What it was actually handed wins over the configured stand-in, so you can
            // see which axe it is carrying rather than a generic one.
            if (!string.IsNullOrEmpty(_tool)) return _tool;

            switch (_job)
            {
                case ThrallJob.Chop: return ThrallConfig.ToolChop.Value;
                case ThrallJob.Mine: return ThrallConfig.ToolMine.Value;
                case ThrallJob.Farm: return ThrallConfig.ToolFarm.Value;
                case ThrallJob.Build:
                case ThrallJob.Repair: return ThrallConfig.ToolBuild.Value;
                default: return null; // idling or foraging keeps whatever it was holding
            }
        }

        private static readonly string[] SwingTriggers =
        {
            "swing_pickaxe", "swing_axe", "swing_longsword", "swing_sledge", "attack"
        };

        /// <summary>
        /// Plays the creature's own swing. Note this really does run the creature's attack,
        /// so a ranged body will fire a projectile - which is why the worker prefab needs
        /// to be a melee one.
        /// </summary>
        private void PlaySwing()
        {
            if (_anim == null) return;

            var configured = ThrallConfig.SwingAnimation.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                if (configured.Equals("none", System.StringComparison.OrdinalIgnoreCase)) return;
                if (_anim.HasParameter(configured, AnimatorControllerParameterType.Trigger))
                    _anim.SetTrigger(configured);
                return;
            }

            for (int i = 0; i < SwingTriggers.Length; i++)
            {
                if (_anim.HasParameter(SwingTriggers[i], AnimatorControllerParameterType.Trigger))
                {
                    _anim.SetTrigger(SwingTriggers[i]);
                    return;
                }
            }
        }

        private void CollectDrops()
        {
            var hits = Physics.OverlapSphere(transform.position, ThrallConfig.PickupRadius.Value,
                Physics.DefaultRaycastLayers);

            for (int i = 0; i < hits.Length; i++)
            {
                var drop = hits[i].GetComponentInParent<ItemDrop>();
                if (drop == null || drop.m_itemData == null) continue;
                if (drop.IsPiece()) continue;

                var nview = drop.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                if (!_inventory.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack)) continue;

                nview.ClaimOwnership();
                if (!nview.IsOwner()) continue;

                if (_inventory.AddItem(drop.m_itemData))
                    nview.Destroy();
            }
        }

        private void Unload(Container container)
        {
            var nview = container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            nview.ClaimOwnership();

            var chest = container.GetInventory();
            if (chest == null) return;

            var farming = _job == ThrallJob.Farm;
            var building = _job == ThrallJob.Build;
            var moved = 0;
            var items = new List<ItemDrop.ItemData>(_inventory.GetAllItems());
            foreach (var item in items)
            {
                // Stock in hand for the current job stays in the pack rather than being
                // handed back and immediately drawn again.
                if (farming && FarmPlanner.IsSeed(item)) continue;
                if (building && _plan != null && NeededByPlan(_plan, item)) continue;

                if (!chest.CanAddItem(item, item.m_stack)) continue;
                if (!chest.AddItem(item)) continue;
                _inventory.RemoveItem(item);
                moved++;
            }

            _lastRestockTake = 0;
            if (farming) _lastRestockTake = TakeSeed(chest);
            else if (building && _plan != null) _lastRestockTake = TakeMaterials(chest, _plan);
            moved += _lastRestockTake;

            if (moved > 0) SaveState();
            else if (!farming && !building) Announce(_name + " found the chest full.");
        }

        private static bool NeededByPlan(BuildPlan plan, ItemDrop.ItemData item)
        {
            var piece = plan.Piece;
            if (piece == null || piece.m_resources == null || item.m_shared == null) return false;

            foreach (var req in piece.m_resources)
                if (req != null && req.m_resItem != null
                    && req.m_resItem.m_itemData.m_shared.m_name == item.m_shared.m_name)
                    return true;
            return false;
        }

        /// <summary>Draws exactly what the current order is short of out of the chest.</summary>
        private int TakeMaterials(Inventory chest, BuildPlan plan)
        {
            var piece = plan.Piece;
            if (piece == null || piece.m_resources == null) return 0;

            var taken = 0;
            foreach (var req in piece.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_amount <= 0) continue;

                var wanted = req.m_resItem.m_itemData.m_shared.m_name;
                var short_ = req.m_amount - CountByName(wanted);
                if (short_ <= 0) continue;

                var stock = new List<ItemDrop.ItemData>(chest.GetAllItems());
                foreach (var item in stock)
                {
                    if (short_ <= 0) break;
                    if (item.m_shared == null || item.m_shared.m_name != wanted) continue;

                    var amount = Mathf.Min(item.m_stack, short_);
                    var portion = item.Clone();
                    portion.m_stack = amount;

                    if (!_inventory.CanAddItem(portion, amount)) break;
                    if (!_inventory.AddItem(portion)) break;

                    chest.RemoveItem(item, amount);
                    short_ -= amount;
                    taken += amount;
                }
            }
            return taken;
        }

        /// <summary>Draws a trip's worth of seed out of the drop-off chest.</summary>
        private int TakeSeed(Inventory chest)
        {
            var want = Mathf.Max(0, ThrallConfig.SeedsPerTrip.Value);
            if (want == 0) return 0;

            var taken = 0;
            var stock = new List<ItemDrop.ItemData>(chest.GetAllItems());
            foreach (var item in stock)
            {
                if (taken >= want) break;
                if (!FarmPlanner.IsSeed(item)) continue;

                var amount = Mathf.Min(item.m_stack, want - taken);
                var portion = item.Clone();
                portion.m_stack = amount;

                if (!_inventory.CanAddItem(portion, amount)) continue;
                if (!_inventory.AddItem(portion)) continue;

                chest.RemoveItem(item, amount);
                taken += amount;
            }
            return taken;
        }

        private void DropEverything()
        {
            var items = new List<ItemDrop.ItemData>(_inventory.GetAllItems());
            foreach (var item in items)
            {
                if (item.m_dropPrefab == null) continue;
                var pos = transform.position + Vector3.up + UnityEngine.Random.insideUnitSphere * 0.3f;
                var go = Instantiate(item.m_dropPrefab, pos, Quaternion.identity);
                var drop = go.GetComponent<ItemDrop>();
                if (drop == null) continue;
                drop.m_itemData = item;
                ItemDrop.OnCreateNew(drop);
            }
            _inventory.RemoveAll();
        }

        // ------------------------------------------------------------------ helpers

        private void GiveUpOn(WorkNode node, string why)
        {
            if (node == null) return;
            _giveUpList[node.Id] = Time.time + 60f;
            if (ThrallConfig.Verbose.Value)
                ThrallsPlugin.Log.LogInfo(string.Format("{0} gave up on {1}: {2}", _name, node.Label, why));
        }

        private void ExpireGiveUps()
        {
            if (_giveUpList.Count == 0) return;
            var stale = new List<int>();
            foreach (var kv in _giveUpList)
                if (Time.time > kv.Value) stale.Add(kv.Key);
            foreach (var id in stale) _giveUpList.Remove(id);
        }

        private HashSet<int> GiveUpIds()
        {
            var set = new HashSet<int>();
            foreach (var kv in _giveUpList) set.Add(kv.Key);
            return set;
        }

        private static bool IsNight()
        {
            return EnvMan.instance != null && EnvMan.IsNight();
        }

        private static void Announce(string msg)
        {
            if (Player.m_localPlayer == null) return;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, msg, 0, null);
        }
    }
}
