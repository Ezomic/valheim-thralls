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
        /// Every usable depot within range of a point, nearest first.
        ///
        /// A depot whose container has not woken up yet is skipped rather than listed - a
        /// thrall handed one of those walks the whole way and then finds nowhere to put
        /// anything, which looks exactly like the pathing being broken.
        /// </summary>
        public static List<ThrallDepot> InRange(Vector3 point, float range)
        {
            var found = new List<ThrallDepot>();

            var depots = All;
            for (int i = 0; i < depots.Count; i++)
            {
                if (!depots[i].Usable) continue;
                if (Vector3.Distance(point, depots[i].transform.position) > range) continue;
                found.Add(depots[i]);
            }

            found.Sort((a, b) => Vector3.Distance(point, a.transform.position)
                        .CompareTo(Vector3.Distance(point, b.transform.position)));
            return found;
        }

        /// <summary>The nearest usable depot to a point, full or not, or null.</summary>
        public static ThrallDepot Nearest(Vector3 point, float range)
        {
            var found = InRange(point, range);
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>
        /// Whether a load could be put down here.
        ///
        /// An empty slot is room for anything. Failing that, a stack that can be topped up
        /// counts too, or a depot holding forty-nine of a fifty stack would be declared
        /// full and the crew would walk past it. Only one item has to fit: Unload already
        /// puts down what it can and keeps the rest, so a part-emptied pack is a normal
        /// outcome rather than a failure.
        /// </summary>
        public bool HasRoomFor(Inventory load)
        {
            if (!Usable) return false;

            var store = _container.GetInventory();
            if (store == null) return false;
            if (store.GetEmptySlots() > 0) return true;
            if (load == null) return false;

            var items = load.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (store.CanAddItem(items[i], items[i].m_stack)) return true;

            return false;
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
