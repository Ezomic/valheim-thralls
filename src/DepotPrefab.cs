using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Registers the depot as a buildable piece.
    ///
    /// Cloned from a chest rather than built from scratch, on the rule that you clone when
    /// you want the machinery: Container is not a small component - it carries the
    /// inventory, the ZDO serialisation of its contents, the request-and-response dance
    /// that lets two players open the same box without losing items, the open and close
    /// effects, and the drop-everything-on-destroy behaviour. None of that is worth
    /// rewriting to get a bin with a post in it.
    ///
    /// The clone's own model is switched off and the mast is put in its place, the same way
    /// the altar treats the ward it comes from.
    /// </summary>
    internal static class DepotPrefab
    {
        public const string Name = "thrall_depot";

        private static GameObject _prefab;
        private static GameObject _holder;

        /// <summary>
        /// Set when the depot cannot be built for a reason that will not change - the
        /// configured donor is not a prefab this game has, or is not a container.
        ///
        /// Registration is retried from Update until it reports ready, so without this a
        /// mistyped DepotBasePrefab writes the same error to the log sixty times a second
        /// and takes the altars down with it: they share the pass, and it never completes.
        /// Every vanilla prefab exists by the time ZNetScene.Awake returns, which is where
        /// the first attempt happens, so a donor missing then is missing for good.
        /// </summary>
        private static bool _failed;

        public static GameObject Prefab { get { return _prefab; } }

        public static bool Ready
        {
            get
            {
                if (_failed) return true;

                return ZNetScene.instance != null
                       && ZNetScene.instance.GetPrefab(Name) != null;
            }
        }

        /// <summary>
        /// Builds it, or returns null if the game is not ready yet.
        ///
        /// The caller hands the result to AltarPrefab's Built list, so the depot rides the
        /// same registration, hammer-table and unlock passes the altars do rather than
        /// growing a second copy of each.
        /// </summary>
        public static GameObject Build()
        {
            if (_prefab != null) return _prefab;
            if (_failed) return null;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return null;

            var donorName = (ThrallConfig.DepotBasePrefab.Value ?? "").Trim();
            var donor = ZNetScene.instance.GetPrefab(donorName);
            if (donor == null)
            {
                _failed = true;
                ThrallsPlugin.Log.LogError("Cannot find '" + donorName + "' to base the depot on. "
                                           + "No depot this session; set DepotBasePrefab to a chest.");
                return null;
            }

            if (donor.GetComponent<Container>() == null)
            {
                _failed = true;
                ThrallsPlugin.Log.LogError("'" + donorName + "' has no Container, so a depot "
                                           + "cloned from it would have nowhere to put anything.");
                return null;
            }

            // Inactive parent, init suppressed: otherwise the clone tries to register itself
            // on the network while it is still half built.
            if (_holder == null)
            {
                _holder = new GameObject("thralls_depot_prefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            GameObject prefab;
            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try { prefab = Object.Instantiate(donor, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            prefab.name = Name;

            Dress(prefab);
            Store(prefab);
            Describe(prefab);

            if (prefab.GetComponent<ThrallDepot>() == null) prefab.AddComponent<ThrallDepot>();

            var scale = Mathf.Clamp(ThrallConfig.DepotScale.Value, 0.4f, 3f);
            prefab.transform.localScale = prefab.transform.localScale * scale;

            _prefab = prefab;
            ThrallsPlugin.Log.LogInfo("Depot '" + ThrallConfig.DepotName.Value + "' built from "
                                      + donorName + ".");
            return prefab;
        }

        /// <summary>
        /// Swaps the chest's look for the mast, and its collision for the boxes written
        /// beside the model.
        /// </summary>
        private static void Dress(GameObject prefab)
        {
            var model = (ThrallConfig.DepotModel.Value ?? "").Trim();

            var visual = new GameObject("depot_visual");
            visual.transform.SetParent(prefab.transform, false);

            if (!AltarPrefab.AddModel(visual.transform, model))
            {
                // No model on disk. Keeping the chest's own is deliberate: a depot that
                // looks like a chest still works, and a piece that silently fails to
                // register would leave the goods with nowhere to go at all.
                Object.Destroy(visual);
                ThrallsPlugin.Log.LogWarning("No depot model at '" + model
                                             + "'; keeping the donor chest's own look.");
                return;
            }

            // Everything the donor brought with it goes dark, the open/closed swap
            // included - Container toggles those two objects as the lid is used, and a
            // chest lid opening inside the mast's bin would be a ghost of the old piece.
            foreach (Transform child in prefab.transform)
            {
                if (child == visual.transform) continue;
                child.gameObject.SetActive(false);
            }

            var container = prefab.GetComponent<Container>();
            if (container != null)
            {
                container.m_open = null;
                container.m_closed = null;
            }

            // WearNTear swaps these as the piece takes damage, so point it at ours rather
            // than at objects that are no longer drawn.
            var wear = prefab.GetComponent<WearNTear>();
            if (wear != null)
            {
                wear.m_new = visual;
                wear.m_worn = null;
                wear.m_broken = null;
            }

            // The chest's own collider is a box the size of a chest, and the mast is
            // nothing like that shape.
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            AltarPrefab.AddColliders(prefab, model);

            var pieceLayer = LayerMask.NameToLayer("piece");
            if (pieceLayer >= 0) prefab.layer = pieceLayer;
        }

        /// <summary>How much it holds, and who may take from it.</summary>
        private static void Store(GameObject prefab)
        {
            var container = prefab.GetComponent<Container>();
            if (container == null) return;

            container.m_name = ThrallConfig.DepotName.Value;
            container.m_width = Mathf.Clamp(ThrallConfig.DepotWidth.Value, 1, 8);
            container.m_height = Mathf.Clamp(ThrallConfig.DepotHeight.Value, 1, 6);

            // Public, not private. A private container refuses anyone but its owner, and a
            // thrall writing into it is not its owner - the check is against a player id,
            // and a creature has none. A locked depot is a depot the crew cannot use.
            container.m_privacy = Container.PrivacySetting.Public;
            container.m_checkGuardStone = false;

            // A depot that deletes itself the moment the crew empties it would take the
            // build cost with it and leave the thralls with nowhere to haul to.
            container.m_autoDestroyEmpty = false;
        }

        /// <summary>Its name, its price and where it sits in the build menu.</summary>
        private static void Describe(GameObject prefab)
        {
            var piece = prefab.GetComponent<Piece>();
            if (piece == null) piece = prefab.AddComponent<Piece>();

            piece.m_name = ThrallConfig.DepotName.Value;
            piece.m_description = "A store your thralls haul their work to. Any thrall working "
                                  + "within " + Mathf.RoundToInt(ThrallConfig.DepotRange.Value)
                                  + "m brings its pack here.";
            piece.m_isUpgrade = false;
            piece.m_category = Piece.PieceCategory.Crafting;
            piece.m_resources = AltarPrefab.CostOf(ThrallConfig.DepotCostNow());

            // No workbench needed, and this is why the depot was missing from the build
            // menu entirely rather than sitting in the wrong tab.
            //
            // The donor is a chest, and a chest requires a workbench, so the clone came
            // with m_craftingStation pointing at one. Player.HaveRequirements checks that
            // field FIRST and returns false when the station is not in m_knownStations -
            // before it looks at a single resource - and the mod's own Teach pass gates on
            // exactly that call. So the piece was registered, in the hammer's table and
            // correctly categorised, and still never became a known recipe. The altar is
            // cloned from a guard stone, which has no station, which is why it taught
            // itself perfectly well and the depot did not.
            //
            // Clearing it is also the right answer on its own terms: the depot belongs out
            // at the treeline where the crew is working, and a piece you can only raise
            // within range of a workbench cannot go there.
            piece.m_craftingStation = null;

            // The mast has a wide plank bin and a post standing out of the middle of it, so
            // its collision is a scatter of boxes rather than one solid mass. The overlap
            // test reads that as blocked against almost anything, which is the same
            // problem the altar had.
            piece.m_clipEverything = true;
            piece.m_noClipping = false;

            // A chest is fussier about where it goes than a stockpile should be.
            piece.m_groundOnly = false;
            piece.m_notOnTiltingSurface = false;
            piece.m_notOnFloor = false;
            piece.m_noInWater = false;

            AltarPrefab.AssignIcon(piece, (ThrallConfig.DepotModel.Value ?? "").Trim());
        }
    }
}
