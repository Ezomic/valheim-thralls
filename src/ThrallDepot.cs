using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The depot: the store a crew hauls its work to.
    ///
    /// It is a chest underneath - cloned from one, so the inventory, the network sync, the
    /// open and close sounds and the "who may take from this" rules are all the game's own
    /// code. What this component adds is only the part the game has no idea about: a
    /// register of where the depots are, so a thrall can find the nearest one to the spot
    /// it is working at.
    ///
    /// Why a piece rather than a setting on the thrall. The mod used to ask you to point at
    /// a chest and press a key, once for each thrall, and then grew an auto-adopt to soften
    /// that - which meant a thrall could quietly claim a box you were keeping something
    /// else in. Building the store where you want the store is the whole instruction, and
    /// it is a thing standing in the world rather than a state you cannot see.
    /// </summary>
    internal class ThrallDepot : MonoBehaviour, Hoverable
    {
        private static readonly List<ThrallDepot> Active = new List<ThrallDepot>();

        private Container _container;
        private ZNetView _nview;

        public Container Store { get { return _container; } }

        private void Awake()
        {
            _container = GetComponent<Container>();
            _nview = GetComponent<ZNetView>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Every depot standing right now, nulls pruned.</summary>
        public static List<ThrallDepot> All
        {
            get
            {
                Active.RemoveAll(d => d == null);
                return Active;
            }
        }

        public static int Count { get { return All.Count; } }

        /// <summary>
        /// The nearest usable depot to a point, or null.
        ///
        /// A depot whose container has not woken up yet is skipped rather than returned -
        /// a thrall handed one of those walks the whole way and then finds nowhere to put
        /// anything, which looks exactly like the pathing being broken.
        /// </summary>
        public static ThrallDepot Nearest(Vector3 point, float range)
        {
            ThrallDepot best = null;
            var bestDist = range;

            var depots = All;
            for (int i = 0; i < depots.Count; i++)
            {
                var depot = depots[i];
                if (!depot.Usable) continue;

                var d = Vector3.Distance(point, depot.transform.position);
                if (d > bestDist) continue;

                bestDist = d;
                best = depot;
            }
            return best;
        }

        public bool Usable
        {
            get
            {
                return _container != null
                       && _nview != null && _nview.IsValid()
                       && _container.GetInventory() != null;
            }
        }

        /// <summary>How full it is, 0 to 1, for the hover text and the thrall panel.</summary>
        public float Fullness
        {
            get
            {
                if (!Usable) return 0f;

                var inv = _container.GetInventory();
                var slots = inv.GetWidth() * inv.GetHeight();
                if (slots <= 0) return 0f;
                return Mathf.Clamp01(inv.NrOfItems() / (float)slots);
            }
        }

        /// <summary>How many thralls are currently hauling here, for the hover text.</summary>
        public int Crew
        {
            get
            {
                var count = 0;
                var crew = ThrallRegistry.All;
                for (int i = 0; i < crew.Count; i++)
                    if (crew[i] != null && crew[i].DepotFor() == this) count++;
                return count;
            }
        }

        // ------------------------------------------------------------- Hoverable

        /// <summary>
        /// Never actually asked for by the game: Container is also a Hoverable and sits on
        /// the same object, so GetComponentInParent finds whichever was added first. The
        /// depot's line reaches the player through the Container.GetHoverText postfix
        /// below instead, and this is here so the component is honest about what it can
        /// say rather than depending on which of the two won.
        /// </summary>
        public string GetHoverName()
        {
            return ThrallConfig.DepotName.Value;
        }

        public string GetHoverText()
        {
            return ThrallConfig.DepotName.Value + "\n" + CrewLine();
        }

        public string CrewLine()
        {
            var crew = Crew;
            if (crew == 0)
                return "no thralls working within "
                       + Mathf.RoundToInt(ThrallConfig.DepotRange.Value) + "m";

            return crew == 1
                ? "1 thrall unloads here"
                : crew + " thralls unload here";
        }
    }

    /// <summary>
    /// Adds the depot's own line to the chest hover text.
    ///
    /// A postfix on Container rather than a Hoverable of our own, because both components
    /// live on the same GameObject and the game takes the first Hoverable it finds - which
    /// is a coin toss decided by the order the components were added to the prefab. This
    /// way the depot keeps the chest's own text, in the chest's own format, with a line
    /// added.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class DepotHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            var depot = __instance.GetComponent<ThrallDepot>();
            if (depot == null) return;

            __result = "<color=#d8c89a>" + depot.CrewLine() + "</color>\n" + __result;
        }
    }
}
