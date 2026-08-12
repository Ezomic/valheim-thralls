using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The summoning altar. Replaces the wandering steward: it is a piece you build, it
    /// keeps the build ledger and the roll of the dead on its own ZDO, and being a piece it
    /// simply cannot be killed by a raid.
    /// </summary>
    internal class ThrallAltar : MonoBehaviour, Hoverable, Interactable
    {
        private static readonly List<ThrallAltar> Active = new List<ThrallAltar>();

        public const string ZSlots = "thrallSlots";
        public const string ZLevel = "thrallAltarLevel";

        private ZNetView _nview;
        private float _slotTimer;

        private static int _groundMask;
        private static int _blockMask;

        /// <summary>
        /// A clear patch of ground to put a thrall on.
        ///
        /// Deliberately not <c>Random.insideUnitSphere</c>, which is what this used to be.
        /// That returns points anywhere *inside* the sphere - including the centre, which
        /// is inside this altar's own collider - and its Y component buried or floated
        /// whoever came out of it. A berserker summoned into the altar was stuck there,
        /// and the only way out was to deconstruct the altar.
        ///
        /// A ring guarantees clearance, the downward ray puts them on the ground rather
        /// than in it, and the capsule test rejects spots already filled by a wall, a
        /// tree or another thrall.
        /// </summary>
        public Vector3 SummonSpot(float clearance = 2.4f)
        {
            if (_groundMask == 0)
                _groundMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            if (_blockMask == 0)
                _blockMask = LayerMask.GetMask("Default", "static_solid", "piece");

            var origin = transform.position;
            var start = Random.Range(0f, Mathf.PI * 2f);
            const int tries = 12;

            for (int i = 0; i < tries; i++)
            {
                // Walk round the ring, easing outwards, so a crowded altar still finds room.
                var angle = start + i * (Mathf.PI * 2f / tries);
                var radius = clearance + (i / (float)tries) * 1.8f;
                var spot = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                RaycastHit hit;
                if (Physics.Raycast(spot + Vector3.up * 6f, Vector3.down, out hit, 14f, _groundMask))
                    spot.y = hit.point.y;

                // Room for a body, from ankle height to head. Terrain is excluded from this
                // mask or the ground itself would count as an obstruction every time.
                if (!Physics.CheckCapsule(spot + Vector3.up * 0.6f, spot + Vector3.up * 1.7f,
                                          0.4f, _blockMask))
                    return spot;
            }

            // Nowhere clear anywhere on the ring. Outside the altar is still better than in it.
            return origin + new Vector3(clearance + 1.5f, 0f, 0f);
        }

        private static bool _dumped;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();

            // Once, for the first altar that actually exists in the world. Everything so
            // far has been measured on the prefab, which cannot show anything a component
            // spawns at runtime - and something is drawing that the prefab does not have.
            if (!_dumped)
            {
                _dumped = true;
                AltarPrefab.DumpLive(gameObject);
            }
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            _slotTimer -= Time.deltaTime;
            if (_slotTimer > 0f) return;
            _slotTimer = 5f;

            RefreshSlots();
        }

        /// <summary>
        /// A crew is only as big as the workshop behind it. Every station upgrade built
        /// nearby - a chopping block, an anvil - earns room for one more thrall at work.
        /// </summary>
        private void RefreshSlots()
        {
            var upgrades = CountUpgrades();
            var slots = Mathf.Clamp(
                ThrallConfig.BaseWorkSlots.Value + upgrades,
                ThrallConfig.BaseWorkSlots.Value,
                Mathf.Max(1, ThrallConfig.MaxWorkSlots.Value));

            if (_nview.GetZDO().GetInt(ZSlots, -1) != slots) _nview.GetZDO().Set(ZSlots, slots);
        }

        private int CountUpgrades()
        {
            var range = ThrallConfig.SlotSearchRange.Value;
            var found = new HashSet<int>();

            var hits = Physics.OverlapSphere(transform.position, range, Physics.DefaultRaycastLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                var ext = hits[i].GetComponentInParent<StationExtension>();
                if (ext != null) found.Add(ext.gameObject.GetInstanceID());
            }
            return found.Count;
        }

        /// <summary>
        /// Work slots as last counted. Kept on the ZDO so walking away from your base does
        /// not lay the crew off just because the workshop stopped being loaded.
        /// </summary>
        public int WorkSlots
        {
            get
            {
                var fallback = Mathf.Max(1, ThrallConfig.BaseWorkSlots.Value);
                if (_nview == null || !_nview.IsValid()) return fallback;
                return Mathf.Max(fallback, _nview.GetZDO().GetInt(ZSlots, fallback));
            }
        }

        /// <summary>
        /// How many times this altar has been built up. Each upgrade opens the next breed,
        /// so the crew you can raise is limited by what you have put into the altar rather
        /// than only by which boss you happened to kill.
        /// </summary>
        public int Upgrades
        {
            get
            {
                return AltarUpgrade.LevelNear(transform.position,
                    Mathf.Max(2f, ThrallConfig.SlotSearchRange.Value));
            }
        }

        /// <summary>Upgrades on the altar you are standing at, for the unlock checks.</summary>
        public static int Level
        {
            get
            {
                var altar = Current;
                return altar != null ? altar.Upgrades : 0;
            }
        }

        public static int Slots
        {
            get
            {
                var altar = Current;
                return altar != null ? altar.WorkSlots : Mathf.Max(1, ThrallConfig.BaseWorkSlots.Value);
            }
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public ZNetView View { get { return _nview; } }

        public bool Usable
        {
            get { return _nview != null && _nview.IsValid(); }
        }

        /// <summary>The altar the ledgers belong to: whichever one is nearest the player.</summary>
        public static ThrallAltar Current
        {
            get
            {
                Active.RemoveAll(a => a == null);

                var player = Player.m_localPlayer;
                if (player == null) return Active.Count > 0 ? Active[0] : null;

                ThrallAltar best = null;
                var bestDist = float.MaxValue;

                for (int i = 0; i < Active.Count; i++)
                {
                    if (!Active[i].Usable) continue;
                    var d = Vector3.Distance(player.transform.position, Active[i].transform.position);
                    if (d < bestDist) { bestDist = d; best = Active[i]; }
                }
                return best;
            }
        }

        public static ThrallAltar Within(float range)
        {
            var altar = Current;
            if (altar == null || Player.m_localPlayer == null) return null;

            return Vector3.Distance(Player.m_localPlayer.transform.position, altar.transform.position) <= range
                ? altar
                : null;
        }

        // ------------------------------------------------------------- Hoverable

        public string GetHoverName()
        {
            return ThrallConfig.AltarName.Value;
        }

        public string GetHoverText()
        {
            return Localization.instance.Localize(
                ThrallConfig.AltarName.Value
                + "\n<color=yellow>" + ThrallRegistry.Count() + " thralls bound</color>"
                + "\n[<color=yellow><b>$KEY_Use</b></color>] command your thralls");
        }

        // ------------------------------------------------------------- Interactable

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (user != Player.m_localPlayer) return false;

            AltarUI.Toggle(this);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits a couple of seconds after a placed altar wakes, then reports every renderer
    /// on it that is genuinely drawing. Runtime-spawned effects do not exist on the
    /// prefab, so a prefab-time dump cannot see them.
    /// </summary>
    internal class LiveDump : MonoBehaviour
    {
        private float _wait = 2f;

        private void Update()
        {
            _wait -= Time.deltaTime;
            if (_wait > 0f) return;

            ThrallsPlugin.Log.LogInfo("=== live altar renderers ===");

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;

                var path = renderer.name;
                for (var t = renderer.transform.parent; t != null && t != transform; t = t.parent)
                    path = t.name + "/" + path;

                var skins = new List<string>();
                foreach (var material in renderer.sharedMaterials)
                    skins.Add(material == null ? "<none>" : material.name);

                ThrallsPlugin.Log.LogInfo(string.Format(
                    "  DRAWING {0} [{1}] size={2} :: {3}",
                    path, renderer.GetType().Name, renderer.bounds.size,
                    string.Join(" | ", skins.ToArray())));
            }

            // Anything nearby that is not part of the altar but might be drawing over it.
            foreach (var projector in Object.FindObjectsOfType<Projector>())
            {
                if (Vector3.Distance(projector.transform.position, transform.position) > 12f) continue;
                ThrallsPlugin.Log.LogInfo("  PROJECTOR near altar: " + projector.name
                                          + " material=" + (projector.material != null
                                              ? projector.material.name : "<none>"));
            }

            ThrallsPlugin.Log.LogInfo("=== end ===");

            // The full bisection is off unless asked for: it spends four seconds cycling
            // the altar's renderers on and off and writes six pictures every time a world
            // loads, which was worth it while hunting the index bug and is only noise now.
            if (ThrallConfig.AltarDiagnostics.Value) gameObject.AddComponent<AltarDiagnose>();
            else if (ThrallConfig.AltarScreenshot.Value) gameObject.AddComponent<AltarDaylightShot>();

            Destroy(this);
        }
    }

    /// <summary>
    /// Builds the altar prefab at runtime by cloning a ward, so the mod stays a single DLL
    /// with no asset bundle. Registered into ZNetScene and bolted onto the hammer.
    /// </summary>
    internal static class AltarPrefab
    {
        public const string Name = "thrall_altar";

        private static readonly List<GameObject> Built = new List<GameObject>();
        private static GameObject _prefab;
        private static GameObject _holder;

        /// <summary>Every altar shape offered on the hammer, as name/model/label triples.</summary>
        private static List<string[]> Shapes()
        {
            var shapes = new List<string[]>();

            var keys = new List<string>();
            foreach (var entry in ThrallConfig.AltarShapes.Value.Split(','))
            {
                var key = entry.Trim();
                if (key.Length > 0) keys.Add(key);
            }

            foreach (var key in keys)
            {
                // The key is a prefab name and is deliberately NOT a description of the
                // shape - "bindstone" now builds the pit, and renaming the key would
                // delete every altar already standing. So it is only shown when there is
                // more than one shape to tell apart; with a single shape the altar is just
                // called what AltarName says.
                var label = ThrallConfig.AltarName.Value;
                if (keys.Count > 1)
                    label += " (" + char.ToUpperInvariant(key[0]) + key.Substring(1) + ")";

                shapes.Add(new[]
                {
                    Name + "_" + key,
                    "thrall_altar_" + key + ".obj",
                    label
                });
            }

            // Nothing configured, or no models on disk: fall back to the single altar.
            if (shapes.Count == 0)
                shapes.Add(new[] { Name, ThrallConfig.AltarModel.Value, ThrallConfig.AltarName.Value });

            return shapes;
        }

        public static bool Ready
        {
            get
            {
                if (ZNetScene.instance == null || Built.Count == 0) return false;
                foreach (var shape in Shapes())
                    if (ZNetScene.instance.GetPrefab(shape[0]) == null) return false;
                return true;
            }
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Register()
        {
            if (Ready) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            if (Built.Count == 0)
            {
                foreach (var shape in Shapes())
                {
                    var built = BuildPrefab(shape[0], shape[1], shape[2]);
                    if (built != null) Built.Add(built);
                }
                if (Built.Count == 0) return false;
            }

            BuildUpgrades();

            AddToScene();
            AddToHammer();
            Measure("piece_workbench");
            return Ready;
        }

        /// <summary>
        /// Every renderer left on the built altar, with what it is wearing and how big it
        /// is. The altar came out in game wrapped in a dark glassy box that appears in no
        /// preview, so the question is what is drawing that - and guessing at it from a
        /// screenshot is how three builds get spent on the wrong thing.
        /// </summary>
        private static void DumpRenderers(GameObject prefab)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var path = renderer.name;
                for (var t = renderer.transform.parent; t != null && t != prefab.transform; t = t.parent)
                    path = t.name + "/" + path;

                var skins = new List<string>();
                foreach (var material in renderer.sharedMaterials)
                {
                    skins.Add(material == null
                        ? "<none>"
                        : material.name + " (" + (material.shader == null ? "?" : material.shader.name) + ")");
                }

                // activeInHierarchy, not activeSelf. A leaf reports itself active while
                // its parent is switched off, so the first run of this told us nothing
                // about what was actually going to draw.
                ThrallsPlugin.Log.LogInfo(string.Format(
                    "  renderer {0} [{1}] drawing={2} size={3} :: {4}",
                    path, renderer.GetType().Name, renderer.gameObject.activeInHierarchy,
                    renderer.bounds.size, string.Join(" | ", skins.ToArray())));
            }
        }

        /// <summary>
        /// The same dump, but of a placed altar a frame after it wakes, so anything a
        /// component spawns at runtime has had a chance to appear.
        /// </summary>
        public static void DumpLive(GameObject instance)
        {
            instance.AddComponent<LiveDump>();
        }

        private static bool _measured;

        /// <summary>
        /// Logs what a vanilla piece is actually made of, so "how does ours compare to the
        /// workbench" can be answered with its numbers instead of an impression of them.
        /// </summary>
        private static void Measure(string prefabName)
        {
            if (_measured) return;
            _measured = true;

            var donor = ZNetScene.instance.GetPrefab(prefabName);
            if (donor == null) return;

            var verts = 0;
            var tris = 0;
            var submeshes = 0;
            var materials = new HashSet<string>();
            var shaders = new HashSet<string>();
            var textures = new HashSet<string>();

            foreach (var filter in donor.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                verts += mesh.vertexCount;
                tris += mesh.triangles.Length / 3;
                submeshes += mesh.subMeshCount;
            }

            foreach (var renderer in donor.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    materials.Add(material.name);
                    if (material.shader != null) shaders.Add(material.shader.name);

                    if (!material.HasProperty("_MainTex")) continue;
                    var tex = material.GetTexture("_MainTex");
                    if (tex != null) textures.Add(tex.name + " " + tex.width + "x" + tex.height);
                }
            }

            ThrallsPlugin.Log.LogInfo(string.Format(
                "{0}: {1} verts, {2} tris, {3} submeshes, {4} materials [{5}], shaders [{6}], textures [{7}]",
                prefabName, verts, tris, submeshes, materials.Count,
                string.Join(", ", new List<string>(materials).ToArray()),
                string.Join(", ", new List<string>(shaders).ToArray()),
                string.Join(", ", new List<string>(textures).ToArray())));
        }

        /// <summary>
        /// The three upgrade pieces, each wearing a model of its own and each carrying the
        /// breed it unlocks - a draugr coming up out of the guck, a golem under the cairn,
        /// a fuling war totem. They used to be assembled out of prefabs the game already
        /// ships, and that assembly is still what they fall back to when the model file is
        /// missing: Compose reaches for UpgradeParts whenever AddModel finds nothing.
        /// </summary>
        private static void BuildUpgrades()
        {
            for (int level = 1; level <= 4; level++)
            {
                var name = Name + "_upgrade" + level;
                if (ZNetScene.instance.GetPrefab(name) != null) continue;

                var built = BuildPrefab(name, ThrallConfig.UpgradeModel(level),
                                        ThrallConfig.UpgradeName(level));
                if (built == null) continue;

                var piece = built.GetComponent<Piece>();
                if (piece != null)
                {
                    // Says what it opens and what it has to stand next to. The chain part
                    // is not decoration: AltarUpgrade.LevelNear only counts an unbroken
                    // run, so a mountain cairn raised on its own does nothing at all and
                    // nothing in the game was telling anyone that.
                    piece.m_description = "Raised beside " + ThrallConfig.AltarName.Value.ToLowerInvariant()
                                          + (level > 1
                                             ? " and its " + ThrallConfig.UpgradeName(level - 1).ToLowerInvariant()
                                             : "")
                                          + ". Opens " + ThrallBreed.NameFor(level + 1) + " thralls.";
                    piece.m_resources = Requirements(ThrallConfig.UpgradeCost(level));

                    // The star the build menu draws on chopping blocks and tanning racks.
                    // Hud.UpdatePieceList does pieceIconData.m_upgrade.SetActive(piece
                    // .m_isUpgrade) against an overlay object that already exists on every
                    // slot, so this is a flag rather than something to paint into the icon -
                    // it stays sharp at any UI scale and cannot drift from the vanilla mark.
                    piece.m_isUpgrade = true;
                }

                var upgrade = built.GetComponent<AltarUpgrade>();
                if (upgrade == null) upgrade = built.AddComponent<AltarUpgrade>();
                upgrade.Level = level;

                // Not a ThrallAltar: an upgrade should not open the panel or keep ledgers.
                var altar = built.GetComponent<ThrallAltar>();
                if (altar != null) Object.Destroy(altar);

                Built.Add(built);
            }
        }

        private static GameObject BuildPrefab(string prefabName, string modelFile, string label)
        {
            var basePrefab = ZNetScene.instance.GetPrefab(ThrallConfig.AltarBasePrefab.Value);
            if (basePrefab == null)
            {
                ThrallsPlugin.Log.LogError("Cannot find '" + ThrallConfig.AltarBasePrefab.Value
                                           + "' to base the altar on.");
                return null;
            }

            // An inactive parent keeps the clone's Awake from firing, which is what makes
            // this a prefab rather than a thing standing in the world.
            if (_holder == null)
            {
                _holder = new GameObject("thralls_prefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            GameObject prefab;
            ZNetView.m_forceDisableInit = true;
            try { prefab = Object.Instantiate(basePrefab, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = false; }

            prefab.name = prefabName;

            // It is an altar, not a ward: strip the area protection and its effects.
            var area = prefab.GetComponent<PrivateArea>();
            if (area != null) Object.Destroy(area);

            var piece = prefab.GetComponent<Piece>();
            if (piece == null) piece = prefab.AddComponent<Piece>();

            piece.m_name = label;
            piece.m_description = "Binds thralls and sets them to work. Upgrades raised "
                                  + "beside it open stronger breeds.";
            // Cleared here and set again by BuildUpgrades for the four that are upgrades,
            // so whatever the donor prefab happened to carry does not leak through.
            piece.m_isUpgrade = false;
            // Crafting, alongside the workbench and forge. It is a station you make things
            // at, not a decoration.
            piece.m_category = Piece.PieceCategory.Crafting;
            piece.m_resources = Requirements();

            // Cloning a ward brought its placement rules with it, and a ward is fussy: flat
            // untilted ground, nothing nearby, not on a floor. An altar this size would be
            // refused almost everywhere, so the restrictions are cleared.
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            piece.m_cultivatedGroundOnly = false;
            piece.m_notOnTiltingSurface = false;
            piece.m_notOnFloor = false;
            piece.m_notOnWood = false;
            piece.m_inCeilingOnly = false;
            piece.m_noInWater = false;
            piece.m_onlyInTeleportArea = false;
            piece.m_allowedInDungeons = false;

            // Its collision is a scatter of boxes covering several separate stones, which
            // the overlap test reads as "blocked" against almost anything.
            piece.m_clipEverything = true;
            piece.m_noClipping = false;

            AssignIcon(piece, modelFile);

            if (!Compose(prefab, modelFile))
            {
                Object.Destroy(prefab);
                return null;
            }

            var scale = Mathf.Clamp(ThrallConfig.AltarScale.Value, 0.5f, 4f);
            prefab.transform.localScale = prefab.transform.localScale * scale;

            if (prefab.GetComponent<ThrallAltar>() == null) prefab.AddComponent<ThrallAltar>();

            _prefab = prefab;
            ThrallsPlugin.Log.LogInfo("Altar '" + label + "' built.");
            DumpRenderers(prefab);
            return prefab;
        }

        /// <summary>
        /// Gives the altar its look: the hand-modelled mesh if its file is on disk, and
        /// otherwise a stand-in assembled from pieces the game already ships.
        /// </summary>
        private static bool Compose(GameObject prefab, string modelFile)
        {
            var visual = new GameObject("altar_visual");
            visual.transform.SetParent(prefab.transform, false);

            // The hand-modelled altar first; the assembled-from-pieces version is the
            // fallback for when the model file is missing.
            var added = AddModel(visual.transform, modelFile) ? 1 : 0;

            if (added == 0)
            {
                var recipe = UpgradeParts(prefab.name);
                if (string.IsNullOrEmpty(recipe)) recipe = ThrallConfig.AltarParts.Value;
                if (!string.IsNullOrEmpty(recipe))
                    foreach (var part in recipe.Split(';'))
                        if (AddPart(visual.transform, part.Trim())) added++;
            }

            if (added == 0)
            {
                Object.Destroy(visual);
                ThrallsPlugin.Log.LogWarning("No model or parts resolved for " + modelFile + ".");
                return false;
            }

            AddProps(visual.transform, modelFile);

            // Retire everything the donor brought with it, renderer or not.
            //
            // This used to switch off only those children that had a Renderer somewhere
            // underneath at build time, which let a ward's area marker straight through:
            // it is a CircleProjector, and a CircleProjector builds its ring out of
            // spawned segments in Start, so at build time there is nothing to find. In
            // game it drew a dark pane over every face of the altar. The component that
            // would normally hide it is PrivateArea.Awake, and that is destroyed above.
            foreach (Transform child in prefab.transform)
            {
                if (child == visual.transform) continue;
                child.gameObject.SetActive(false);
            }

            // Belt and braces for anything that draws without being a Renderer.
            foreach (var projector in prefab.GetComponentsInChildren<CircleProjector>(true))
                Object.Destroy(projector);

            // WearNTear swaps these objects as a piece takes damage; point it at ours so it
            // is not toggling something that is no longer there.
            var wear = prefab.GetComponent<WearNTear>();
            if (wear != null)
            {
                wear.m_new = visual;
                wear.m_worn = null;
                wear.m_broken = null;
            }

            AddColliders(prefab, modelFile);

            var pieceLayer = LayerMask.NameToLayer("piece");
            if (pieceLayer >= 0) prefab.layer = pieceLayer;

            return true;
        }

        /// <summary>Which parts recipe an upgrade piece is assembled from.</summary>
        private static string UpgradeParts(string prefabName)
        {
            if (prefabName == null) return "";
            if (prefabName.EndsWith("_upgrade1")) return ThrallConfig.Upgrade1Parts.Value;
            if (prefabName.EndsWith("_upgrade2")) return ThrallConfig.Upgrade2Parts.Value;
            if (prefabName.EndsWith("_upgrade3")) return ThrallConfig.Upgrade3Parts.Value;
            if (prefabName.EndsWith("_upgrade4")) return ThrallConfig.Upgrade4Parts.Value;
            return "";
        }

        private static readonly Dictionary<string, ModelData> Models = new Dictionary<string, ModelData>();
        private static readonly Dictionary<string, Material> Skins = new Dictionary<string, Material>();
        private static readonly HashSet<Mesh> Fitted = new HashSet<Mesh>();

        /// <summary>
        /// Collision is a box per block of stone, listed in a .col file written out beside
        /// the model. One box round the whole altar is far too crude for four shapes this
        /// different, and a concave mesh collider is both costly and horrible to walk on.
        /// </summary>
        private static void AddColliders(GameObject prefab, string modelFile)
        {
            var dir = Path.GetDirectoryName(typeof(AltarPrefab).Assembly.Location);
            var path = Path.Combine(dir, Path.ChangeExtension(modelFile, ".col"));

            var body = new GameObject("altar_collision");
            body.transform.SetParent(prefab.transform, false);

            var pieceLayer = LayerMask.NameToLayer("piece");
            if (pieceLayer >= 0) body.layer = pieceLayer;

            var boxes = 0;
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    var f = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length < 7 || f[0] != "box") continue;

                    var centre = new Vector3(ParseFloat(f[1], 0f), ParseFloat(f[2], 0f), ParseFloat(f[3], 0f));
                    var size = new Vector3(ParseFloat(f[4], 1f), ParseFloat(f[5], 1f), ParseFloat(f[6], 1f));
                    var yaw = f.Length > 7 ? ParseFloat(f[7], 0f) : 0f;

                    // A BoxCollider cannot be rotated on its own, so a turned block gets its
                    // own child transform. The sign flips because the export swaps handedness.
                    var owner = body;
                    if (Mathf.Abs(yaw) > 0.5f)
                    {
                        owner = new GameObject("box");
                        owner.layer = body.layer;
                        owner.transform.SetParent(body.transform, false);
                        owner.transform.localPosition = centre;
                        owner.transform.localRotation = Quaternion.Euler(0f, -yaw, 0f);
                        centre = Vector3.zero;
                    }

                    var box = owner.AddComponent<BoxCollider>();
                    box.center = centre;
                    box.size = size;
                    boxes++;
                }
            }

            if (boxes == 0)
            {
                // No collision file: a single box is wrong, but better than walking through it.
                var fallback = body.AddComponent<BoxCollider>();
                fallback.center = new Vector3(0f, 0.9f, 0f);
                fallback.size = new Vector3(3.2f, 1.8f, 3.2f);
                ThrallsPlugin.Log.LogWarning("No collision file for " + modelFile + ", using one box.");
                return;
            }

            ThrallsPlugin.Log.LogInfo("Collision for " + modelFile + ": " + boxes + " boxes.");
        }

        /// <summary>
        /// The hand-modelled altar, wearing a material lifted off a vanilla stone piece so
        /// it takes the game's own shader, lighting, wetness and snow rather than looking
        /// like a foreign object dropped into the world.
        /// </summary>
        private static bool AddModel(Transform parent, string modelFile)
        {
            ModelData model;
            if (!Models.TryGetValue(modelFile, out model))
            {
                var dir = Path.GetDirectoryName(typeof(AltarPrefab).Assembly.Location);
                model = ObjMesh.Load(Path.Combine(dir, modelFile));
                Models[modelFile] = model;
            }
            if (model == null) return false;

            // One material per material group in the model, each wearing its own texture,
            // so the timber, the iron and the stone are not all the same brown.
            var materials = new Material[model.Groups.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = SkinFor(modelFile, model.Groups[i], model.Mesh);
                if (materials[i] == null) return false;
            }

            var go = new GameObject("altar_model");
            go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;

            return true;
        }

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>
        /// The picture the hammer menu and the unlock card show.
        ///
        /// Cloned from a ward, the pieces inherited its icon, so the build menu offered four
        /// different altars all wearing a guard stone. The icons are rendered beside the
        /// models - same Blender pass, transparent background - and simply loaded here.
        ///
        /// A missing file is not an error: the piece keeps the donor's icon, which is wrong
        /// but is at least a picture.
        /// </summary>
        private static void AssignIcon(Piece piece, string modelFile)
        {
            if (piece == null || string.IsNullOrEmpty(modelFile)) return;

            Sprite sprite;
            if (!Icons.TryGetValue(modelFile, out sprite))
            {
                var dir = Path.GetDirectoryName(typeof(AltarPrefab).Assembly.Location);
                var stem = Path.GetFileNameWithoutExtension(modelFile);
                var texture = LoadTexture(Path.Combine(dir, stem + "_icon.png"));

                sprite = texture == null
                    ? null
                    : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                                    new Vector2(0.5f, 0.5f));

                Icons[modelFile] = sprite;
                if (sprite != null)
                    ThrallsPlugin.Log.LogInfo("Icon loaded for " + stem + " ("
                                              + texture.width + "x" + texture.height + ")");
            }

            if (sprite != null) piece.m_icon = sprite;
        }

        private static Material _stone;
        private static Rect _stoneUv = new Rect(0f, 0f, 1f, 1f);
        private static bool _stoneUvKnown;

        /// <summary>
        /// Each altar wears its own stone. The texture is ours, so it owns the whole 0-1 UV
        /// square and none of the shared-atlas juggling applies. The Valheim material is
        /// still the base, which keeps the game's shader, lighting, wetness and snow.
        /// </summary>
        private static Material SkinFor(string modelFile, string group, Mesh model)
        {
            var key = modelFile + "|" + group;

            Material skin;
            if (Skins.TryGetValue(key, out skin)) return skin;

            // Wood groups are skinned off a wooden piece, everything else off stone, so a
            // group handed back its donor untouched gets the right donor to be handed.
            var wooden = !string.IsNullOrEmpty(group)
                         && ("," + (ThrallConfig.AltarVanillaWoodGroups.Value ?? "").Replace(" ", "") + ",")
                            .IndexOf("," + group + ",", System.StringComparison.OrdinalIgnoreCase) >= 0;

            var basis = wooden ? BorrowWoodMaterial() : BorrowStoneMaterial();
            if (basis == null)
            {
                ThrallsPlugin.Log.LogWarning("No " + (wooden ? "wood" : "stone")
                                             + " material to skin the altar with.");
                return null;
            }

            var dir = Path.GetDirectoryName(typeof(AltarPrefab).Assembly.Location);
            var stem = Path.GetFileNameWithoutExtension(modelFile);

            // Groups that are handed straight to the game's own material instead of one of
            // ours. Measured against Valheim's stone on the same mesh, a sheet of ours came
            // out at 84% of its brightness and 66% of its contrast, and the contrast half of
            // that gap is not closable here: the branch below flattens the normals, so the
            // normal map the vanilla stone gets its highlights from is switched off. Rather
            // than keep chasing that with albedo, stone can simply be the game's stone.
            Texture2D texture = null;
            var vanillaGroups = ThrallConfig.AltarVanillaGroups.Value ?? "";
            var useVanilla = wooden
                             || (!string.IsNullOrEmpty(group)
                                 && ("," + vanillaGroups.Replace(" ", "") + ",")
                                    .IndexOf("," + group + ",", System.StringComparison.OrdinalIgnoreCase) >= 0);

            // thrall_altar_worktable_iron.png for a named group, falling back to the one
            // sheet the single-material altars use.
            if (!useVanilla)
            {
                if (!string.IsNullOrEmpty(group))
                    texture = LoadTexture(Path.Combine(dir, stem + "_" + group + ".png"));

                if (texture == null) texture = LoadTexture(Path.Combine(dir, stem + ".png"));
            }

            if (texture == null)
            {
                // No texture of our own, so fall back to the borrowed material and squeeze
                // the UVs into whatever patch of its atlas the donor actually uses.
                //
                // Only the first group to get here does the fitting - the UVs belong to the
                // mesh, not the submesh, so stone and wood cannot each have their own patch.
                // Whichever is listed first in the OBJ wins and the other samples through
                // it, which is survivable while both donors use most of their sheet.
                var rect = wooden ? _woodUv : _stoneUv;
                var rectKnown = wooden ? _woodUvKnown : _stoneUvKnown;

                if (rectKnown && Fitted.Add(model))
                    FitUvs(model, rect, Mathf.Max(0.01f, ThrallConfig.AltarUvScale.Value));

                Skins[key] = basis;
                return basis;
            }

            skin = new Material(basis)
            {
                name = "thrall_" + stem + (string.IsNullOrEmpty(group) ? "" : "_" + group)
            };
            skin.SetTexture("_MainTex", texture);

            if (ThrallConfig.AltarFlattenNormals.Value) FlattenNormals(skin);

            // Guarded on the mesh, not the material: the UVs are shared by every group,
            // so tiling them once per group would compound the scale.
            if (Fitted.Add(model))
                TileUvs(model, Mathf.Max(0.01f, ThrallConfig.AltarUvScale.Value));

            Skins[key] = skin;
            ThrallsPlugin.Log.LogInfo("Altar skin built for " + key
                                      + " (" + texture.width + "x" + texture.height + ")");
            return skin;
        }

        private static Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!DecodePng(texture, File.ReadAllBytes(path))) return null;

                texture.wrapMode = TextureWrapMode.Repeat;

                // Point sampling, so texels stay square instead of being blurred into
                // each other. Bilinear on an already low resolution sheet gives a soft,
                // smeared surface, which is the opposite of the crisp blocky look the
                // game's own props have close up.
                texture.filterMode = ThrallConfig.AltarTexturePoint.Value
                    ? FilterMode.Point
                    : FilterMode.Bilinear;

                texture.anisoLevel = ThrallConfig.AltarTexturePoint.Value ? 0 : 4;

                // Mipmaps stay on regardless: without them a point sampled texture
                // shimmers badly the moment you walk away from it.
                texture.Apply(true, false);
                return texture;
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        private static System.Reflection.MethodInfo _loadImage;

        /// <summary>
        /// ImageConversion.LoadImage, reached by reflection: its assembly targets a newer
        /// netstandard than this one and cannot be referenced at compile time.
        /// </summary>
        private static bool DecodePng(Texture2D texture, byte[] bytes)
        {
            if (_loadImage == null)
            {
                var type = System.Type.GetType(
                    "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");

                if (type == null)
                {
                    ThrallsPlugin.Log.LogWarning("No image decoder available in this build.");
                    return false;
                }

                _loadImage = AccessTools.Method(type, "LoadImage",
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            }
            if (_loadImage == null) return false;

            var result = _loadImage.Invoke(null, new object[] { texture, bytes, false });
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Valheim's shaders do not all call the normal map "_BumpMap" - Custom/Piece uses
        /// its own names. Only checking that one name meant that on this shader the switch
        /// silently did nothing and the donor's ATLAS normal map stayed on the material,
        /// sampled with our own tiled UVs. That gives every face a normal taken from an
        /// unrelated patch of somebody else's texture, which is what lights flat surfaces
        /// as black shards with hard highlights across them.
        ///
        /// So every name the game might be using gets a flat map, and what was actually
        /// found is logged rather than assumed.
        /// </summary>
        private static readonly string[] NormalProperties =
        {
            "_BumpMap", "_MainNormal", "_NormalMap", "_Normal", "_BumpMap2",
            "_MainBump", "_NormalTex", "_MainTexNormal", "_DetailNormalMap"
        };

        private static bool _normalsLogged;

        private static void FlattenNormals(Material skin)
        {
            var found = new List<string>();
            var all = new List<string>();

            // Ask the shader what it actually has rather than guessing at names. This is
            // the whole reason the first attempt did nothing: "_BumpMap" is the Standard
            // shader's name for it, and Custom/Piece is not the Standard shader.
            if (!Enumerate(skin, found, all))
            {
                for (int i = 0; i < NormalProperties.Length; i++)
                {
                    if (!skin.HasProperty(NormalProperties[i])) continue;
                    skin.SetTexture(NormalProperties[i], FlatNormal());
                    found.Add(NormalProperties[i]);
                }
            }

            if (_normalsLogged) return;
            _normalsLogged = true;

            ThrallsPlugin.Log.LogInfo("Shader " + skin.shader.name + " textures: ["
                                      + string.Join(", ", all.ToArray()) + "]");

            ThrallsPlugin.Log.LogInfo(found.Count > 0
                ? "Flattened: " + string.Join(", ", found.ToArray())
                : "No normal map property found - the donor's own normals are still in "
                  + "place and will be sampled with our UVs.");
        }

        /// <summary>
        /// Walks the shader's own property list, flattening every texture that is a normal
        /// map. Returns false if this build of Unity will not tell us, so the guessed list
        /// can be used instead.
        /// </summary>
        private static bool Enumerate(Material skin, List<string> found, List<string> all)
        {
            var shader = skin.shader;
            if (shader == null) return false;

            try
            {
                var count = shader.GetPropertyCount();

                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                        continue;

                    var name = shader.GetPropertyName(i);
                    all.Add(name);

                    // Unity flags normal maps on the property itself, which is exact.
                    // The name check is a safety net for shaders that do not set it.
                    var flags = shader.GetPropertyFlags(i);
                    var isNormal =
                        (flags & UnityEngine.Rendering.ShaderPropertyFlags.Normal) != 0
                        || name.IndexOf("ormal", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("ump", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isNormal) continue;

                    skin.SetTexture(name, FlatNormal());
                    found.Add(name);
                }

                return true;
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Cannot read shader properties: " + e.Message);
                return false;
            }
        }

        private static Texture2D _flatNormal;

        private static Texture2D FlatNormal()
        {
            if (_flatNormal != null) return _flatNormal;

            _flatNormal = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            // Alpha 0.5, not 1. Unity has two normal-map encodings and a shader may use
            // either: plain RGB reads the normal from r,g,b, while DXT5nm reads x from
            // ALPHA and y from green. With alpha at 1 the second reading gives x = 1,
            // y = 0, z = 0 - a tangent normal lying flat along the surface instead of
            // standing up out of it. Every face is then lit as though it points 90 degrees
            // away from where it actually points, which turns flat walls black and throws
            // hard specular streaks across them.
            //
            // (0.5, 0.5, 1, 0.5) decodes to straight up under both readings.
            var flat = new Color(0.5f, 0.5f, 1f, 0.5f);
            _flatNormal.SetPixels(new[] { flat, flat, flat, flat });
            _flatNormal.Apply();
            return _flatNormal;
        }

        /// <summary>Plain repeat across our own texture, scaled to taste.</summary>
        private static void TileUvs(Mesh mesh, float scale)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            for (int i = 0; i < uv.Length; i++) uv[i] *= scale;
            mesh.uv = uv;
        }

        /// <summary>
        /// Valheim's stone pieces share one atlas: the stone occupies a patch of it and the
        /// rest is other material entirely. UVs that run across the whole sheet therefore
        /// land half on stone and half on blank, which is the checkerboard. Reading the
        /// donor mesh's own UV bounds tells us which patch is actually the stone.
        /// </summary>
        private static Rect StoneUvRegion(Renderer donorRenderer)
        {
            var configured = ThrallConfig.AltarUvRegion.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                var f = configured.Split(',');
                if (f.Length == 4)
                {
                    return new Rect(ParseFloat(f[0], 0f), ParseFloat(f[1], 0f),
                        ParseFloat(f[2], 1f), ParseFloat(f[3], 1f));
                }
            }

            var filter = donorRenderer != null ? donorRenderer.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return new Rect(0f, 0f, 1f, 1f);

            Vector2[] uv;
            try
            {
                // Imported meshes are frequently upload-only; reading them then throws.
                if (!mesh.isReadable) return new Rect(0f, 0f, 1f, 1f);
                uv = mesh.uv;
            }
            catch { return new Rect(0f, 0f, 1f, 1f); }

            if (uv == null || uv.Length == 0) return new Rect(0f, 0f, 1f, 1f);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            var size = max - min;
            if (size.x <= 0.001f || size.y <= 0.001f) return new Rect(0f, 0f, 1f, 1f);

            return new Rect(min.x, min.y, size.x, size.y);
        }

        /// <summary>Folds the mesh's UVs into the stone patch, tiling within it.</summary>
        private static void FitUvs(Mesh mesh, Rect region, float scale)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            for (int i = 0; i < uv.Length; i++)
            {
                // Repeat inside the patch rather than across the whole sheet.
                var u = Mathf.Repeat(uv[i].x * scale, 1f);
                var v = Mathf.Repeat(uv[i].y * scale, 1f);

                uv[i] = new Vector2(region.x + u * region.width, region.y + v * region.height);
            }

            mesh.uv = uv;
        }

        /// <summary>
        /// Lifts a material off a real stone piece. Picks the renderer with the most
        /// submeshes-worth of texture rather than the first one found, because the first
        /// child of a piece is often a tiny detail mesh with a flat material on it.
        /// </summary>
        /// <summary>The untouched vanilla material, for comparing ours against.</summary>
        public static Material DonorMaterial { get { return BorrowStoneMaterial(); } }

        private static Material BorrowStoneMaterial()
        {
            if (_stone != null) return _stone;

            foreach (var name in ThrallConfig.AltarMaterialFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance.GetPrefab(name.Trim());
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    // Skip anything with no albedo to give us - those render flat.
                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
                    {
                        ThrallsPlugin.Log.LogInfo(string.Format(
                            "Skipping {0}/{1} (shader {2}, no albedo)",
                            name.Trim(), material.name, material.shader.name));
                        continue;
                    }

                    _stoneUv = StoneUvRegion(renderer);
                    _stoneUvKnown = true;

                    ThrallsPlugin.Log.LogInfo(string.Format(
                        "Altar skinned with {0} from {1} (shader {2}), atlas patch {3}",
                        material.name, name.Trim(), material.shader.name, _stoneUv));

                    _stone = material;
                    return _stone;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no textured stone material to borrow.");
            return null;
        }

        private static Material _wood;
        private static Rect _woodUv = new Rect(0f, 0f, 1f, 1f);
        private static bool _woodUvKnown;

        /// <summary>
        /// The same trick as the stone, off a wooden piece instead.
        ///
        /// Our own timber sheet is both darker than the game's wood and, because every
        /// custom skin has its normals flattened, carries none of the grain highlights that
        /// make a vanilla board read as a board. Side by side with a workbench the poles
        /// came out nearly black. Borrowing the wood outright fixes both at once, the same
        /// way borrowing the stone did.
        /// </summary>
        private static Material BorrowWoodMaterial()
        {
            if (_wood != null) return _wood;

            foreach (var name in ThrallConfig.AltarWoodMaterialFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance.GetPrefab(name.Trim());
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
                        continue;

                    _woodUv = StoneUvRegion(renderer);
                    _woodUvKnown = true;

                    ThrallsPlugin.Log.LogInfo(string.Format(
                        "Altar wood borrowed from {0}/{1} (shader {2}), atlas patch {3}",
                        name.Trim(), material.name, material.shader.name, _woodUv));

                    _wood = material;
                    return _wood;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no textured wood material to borrow.");
            return null;
        }

        /// <summary>
        /// Torches, lamps and fires dressed onto each altar, so they are not four piles of
        /// bare rock. Stripping leaves lights and particles intact, so the flames still burn.
        /// </summary>
        private static void AddProps(Transform parent, string modelFile)
        {
            var key = Path.GetFileNameWithoutExtension(modelFile);
            var dash = key.LastIndexOf('_');
            if (dash >= 0) key = key.Substring(dash + 1);

            // Before the props, so an altar with no props still gets its motes.
            AltarEffects.Attach(parent, key);
            AltarAmbience.Attach(parent);

            string recipe;
            switch (key)
            {
                case "plinth": recipe = ThrallConfig.PropsPlinth.Value; break;
                case "dolmen": recipe = ThrallConfig.PropsDolmen.Value; break;
                case "cairn": recipe = ThrallConfig.PropsCairn.Value; break;
                case "circle": recipe = ThrallConfig.PropsCircle.Value; break;
                case "barrow": recipe = ThrallConfig.PropsBarrow.Value; break;
                case "worktable": recipe = ThrallConfig.PropsWorktable.Value; break;
                case "shrine": recipe = ThrallConfig.PropsShrine.Value; break;
                case "bindstone": recipe = ThrallConfig.PropsBindstone.Value; break;
                default: return;
            }
            if (string.IsNullOrEmpty(recipe)) return;

            var added = 0;
            foreach (var part in recipe.Split(';'))
                if (AddPart(parent, part.Trim())) added++;

            if (added > 0) ThrallsPlugin.Log.LogInfo("Dressed " + key + " with " + added + " props.");
        }

        /// <summary>One "prefab:x,y,z:scale:yaw" entry, stripped down to pure scenery.</summary>
        private static bool AddPart(Transform parent, string spec)
        {
            if (string.IsNullOrEmpty(spec)) return false;

            var fields = spec.Split(':');
            if (fields.Length < 2) return false;

            var donor = ZNetScene.instance.GetPrefab(fields[0].Trim());
            if (donor == null)
            {
                ThrallsPlugin.Log.LogWarning("Altar part '" + fields[0].Trim() + "' does not exist, skipping.");
                return false;
            }

            GameObject copy;
            ZNetView.m_forceDisableInit = true;
            try { copy = Object.Instantiate(donor, parent); }
            finally { ZNetView.m_forceDisableInit = false; }

            Strip(copy);
            Quieten(copy, fields[0].Trim());

            copy.transform.localPosition = ParseVector(fields[1]);
            copy.transform.localScale *= fields.Length > 2 ? ParseFloat(fields[2], 1f) : 1f;
            copy.transform.localRotation = ParseRotation(fields.Length > 3 ? fields[3] : "0");
            return true;
        }

        /// <summary>
        /// A yaw on its own, or "pitch,yaw,roll" for a prop that needs tipping.
        ///
        /// Yaw alone is fine for a torch or a chest, but an item prefab - a trophy, say -
        /// is modelled lying face up the way it sits in an inventory slot. Hung on a wall
        /// with only a yaw available it stares at the sky.
        /// </summary>
        private static Quaternion ParseRotation(string text)
        {
            var f = text.Split(',');

            if (f.Length >= 3)
                return Quaternion.Euler(ParseFloat(f[0], 0f), ParseFloat(f[1], 0f), ParseFloat(f[2], 0f));

            return Quaternion.Euler(0f, ParseFloat(text, 0f), 0f);
        }

        /// <summary>
        /// Strips the effects off props that should just sit there.
        ///
        /// A dropped item sparkles so you can find it in long grass, and that effect is a
        /// particle system rather than a component - so Strip leaves it running and a
        /// trophy mounted on the altar glitters away like loot on the floor. Torches and
        /// candles are deliberately not covered: their flame is the whole point.
        /// </summary>
        private static void Quieten(GameObject copy, string prefabName)
        {
            var quiet = false;
            foreach (var prefix in ThrallConfig.PropsNoEffects.Value.Split(','))
            {
                var trimmed = prefix.Trim();
                if (trimmed.Length == 0) continue;

                if (prefabName.StartsWith(trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                    break;
                }
            }
            if (!quiet) return;

            foreach (var system in copy.GetComponentsInChildren<ParticleSystem>(true))
                Object.Destroy(system.gameObject);

            foreach (var light in copy.GetComponentsInChildren<Light>(true))
                Object.Destroy(light);
        }

        /// <summary>Scenery only: no logic, no physics, no networking, nothing to collide with.</summary>
        private static void Strip(GameObject go)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) Object.Destroy(behaviour);

            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);

            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(body);

            foreach (var view in go.GetComponentsInChildren<ZNetView>(true))
                Object.Destroy(view);
        }

        private static Vector3 ParseVector(string text)
        {
            var parts = text.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            return new Vector3(ParseFloat(parts[0], 0f), ParseFloat(parts[1], 0f), ParseFloat(parts[2], 0f));
        }

        private static float ParseFloat(string text, float fallback)
        {
            float value;
            return float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static Piece.Requirement[] Requirements()
        {
            return Requirements(ThrallConfig.AltarCost.Value);
        }

        private static Piece.Requirement[] Requirements(string spec)
        {
            var list = new List<Piece.Requirement>();

            foreach (var entry in ItemCost.Parse(spec))
            {
                var prefab = ObjectDB.instance.GetItemPrefab(entry.Key);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    ThrallsPlugin.Log.LogWarning("Altar cost mentions unknown item '" + entry.Key + "'.");
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = entry.Value,
                    m_recover = true
                });
            }
            return list.ToArray();
        }

        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedRef =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");

        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownRef =
            AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes");

        /// <summary>
        /// Suffixes the altar used to carry, back when the shape's key was appended to
        /// its name and a single shape still got one.
        /// </summary>
        private static readonly string[] LegacyAltarSuffixes =
        {
            " (Bindstone)", " (Pit)", " (Worktable)"
        };

        /// <summary>
        /// Every name this piece has been shipped under before its current one.
        ///
        /// An upgrade is told apart from the altar by its AltarUpgrade component rather
        /// than by its name, since the name is precisely what cannot be trusted here.
        /// </summary>
        private static IEnumerable<string> FormerNames(GameObject prefab)
        {
            var upgrade = prefab.GetComponent<AltarUpgrade>();
            if (upgrade != null)
                return ThrallConfig.UpgradeLegacyNames(upgrade.Level);

            var names = new List<string>();
            for (int i = 0; i < LegacyAltarSuffixes.Length; i++)
                names.Add(ThrallConfig.AltarName.Value + LegacyAltarSuffixes[i]);
            return names;
        }

        /// <summary>
        /// True if this piece was already known under a name the mod no longer uses, in
        /// which case the current name is recorded and the old one dropped.
        /// </summary>
        private static bool Renamed(HashSet<string> known, GameObject prefab, Piece piece)
        {
            foreach (var was in FormerNames(prefab))
            {
                if (was == piece.m_name || !known.Contains(was)) continue;

                known.Remove(was);
                known.Add(piece.m_name);
                ThrallsPlugin.Log.LogInfo("Carried '" + was + "' over to '" + piece.m_name
                                          + "' - a rename would otherwise have un-learned it.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// The build menu hides any piece the player has not learned, and nothing in the
        /// game will ever teach one the game does not know about, so the altars are taught
        /// directly. Without this they are registered and buildable but simply invisible.
        /// </summary>
        public static void Teach()
        {
            var player = Player.m_localPlayer;
            if (player == null || Built.Count == 0) return;

            HashSet<string> known;
            try { known = KnownRef(player); }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogError("Cannot reach the known-pieces list: " + e.Message);
                return;
            }
            if (known == null) return;

            foreach (var prefab in Built)
            {
                if (prefab == null) continue;
                var piece = prefab.GetComponent<Piece>();
                if (piece == null || string.IsNullOrEmpty(piece.m_name)) continue;

                if (known.Contains(piece.m_name)) continue;

                // A piece the player already learned under an older name still counts.
                //
                // m_knownRecipes is keyed by the display name, and there is no separate
                // id - so renaming a piece silently un-learns it for every character that
                // had it, and it vanishes out of the hammer with no message. Dropping the
                // "(Bindstone)" suffix did exactly that. The upgrades kept their names and
                // stayed put, which is why the altar was the only one to disappear.
                if (Renamed(known, prefab, piece)) continue;

                // Unlocked the way every other piece in the game is: once you have had at
                // least one of each of its materials in hand. RequirementMode.IsKnown is
                // exactly the test Player.UpdateKnownRecipesList applies to pieces.
                //
                // This used to teach unconditionally, which handed a brand new character
                // all five altars before it had swung an axe. That was written when the
                // pieces were not reaching the hammer's table at all and nothing would ever
                // teach them; they are registered there now, so the only job left here is
                // to re-check between the game's own passes - it runs that list on
                // inventory changes, and a piece added to the table mid-session would
                // otherwise wait for the next one.
                if (!player.HaveRequirements(piece, Player.RequirementMode.IsKnown)) continue;

                // Through the game's own method where possible. Adding the name straight to
                // the set - which is all this used to do - leaves the player with no idea
                // anything new exists: AddKnownPiece is what queues the unlock card and
                // plays its sound, and it is the only difference between the two paths.
                if (AddKnownPieceRef != null && MessageHud.instance != null)
                {
                    try
                    {
                        AddKnownPieceRef.Invoke(player, new object[] { piece });
                        ThrallsPlugin.Log.LogInfo("Taught piece with card: " + piece.m_name);
                        continue;
                    }
                    catch (System.Exception e)
                    {
                        ThrallsPlugin.Log.LogWarning("AddKnownPiece failed, adding quietly: "
                                                     + e.Message);
                    }
                }

                // No MessageHud yet, or the method moved: teach it silently rather than not
                // at all. A piece you cannot see in the hammer is worse than an unannounced one.
                if (known.Add(piece.m_name))
                    ThrallsPlugin.Log.LogInfo("Taught piece: " + piece.m_name);
            }
        }

        /// <summary>
        /// Player.AddKnownPiece is private, so it is reached by reflection. Resolved once and
        /// allowed to be null - if a game update renames it the altars still get taught, just
        /// without the card.
        /// </summary>
        private static readonly System.Reflection.MethodInfo AddKnownPieceRef =
            AccessTools.Method(typeof(Player), "AddKnownPiece", new[] { typeof(Piece) });

        private static void AddToScene()
        {
            var scene = ZNetScene.instance;

            foreach (var prefab in Built)
            {
                if (prefab == null || scene.GetPrefab(prefab.name) != null) continue;
                if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);

                try { NamedRef(scene)[prefab.name.GetStableHashCode()] = prefab; }
                catch (System.Exception e)
                {
                    ThrallsPlugin.Log.LogError("Could not register " + prefab.name + ": " + e.Message);
                }
            }
        }

        private static void AddToHammer()
        {
            var hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            var drop = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            if (table == null || table.m_pieces == null) return;

            foreach (var prefab in Built)
                if (prefab != null && !table.m_pieces.Contains(prefab))
                    table.m_pieces.Add(prefab);
        }
    }
}
