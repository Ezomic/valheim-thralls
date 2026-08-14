using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>Keeps track of the thralls in the loaded world and re-attaches behaviour after a reload.</summary>
    internal static class ThrallRegistry
    {
        private static readonly List<Thrall> Active = new List<Thrall>();

        public static IList<Thrall> All { get { return Active; } }

        public static void Register(Thrall thrall)
        {
            if (thrall != null && !Active.Contains(thrall)) Active.Add(thrall);
        }

        public static void Unregister(Thrall thrall)
        {
            Active.Remove(thrall);
        }

        /// <summary>
        /// The world reloads creatures from their ZDO without our component, so anything
        /// flagged as a thrall gets its behaviour put back on.
        /// </summary>
        public static void AttachToExisting()
        {
            Active.RemoveAll(t => t == null);

            var characters = Character.GetAllCharacters();
            for (int i = 0; i < characters.Count; i++)
            {
                var c = characters[i];
                if (c == null || c.IsPlayer()) continue;

                var nview = c.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                if (c.GetComponent<Thrall>() != null) continue;
                if (!Thrall.IsThrall(nview)) continue;
                if (!nview.IsOwner()) continue;

                c.gameObject.AddComponent<Thrall>();
            }
        }

        public static int Count()
        {
            Active.RemoveAll(t => t == null);
            return Active.Count;
        }

        /// <summary>Thralls actually on a job. Idle and following ones cost you no work slot.</summary>
        public static int WorkingCount()
        {
            Active.RemoveAll(t => t == null);

            var working = 0;
            for (int i = 0; i < Active.Count; i++)
                if (IsWork(Active[i].Job)) working++;

            return working;
        }

        public static bool IsWork(ThrallJob job)
        {
            return job != ThrallJob.None && job != ThrallJob.Follow;
        }

        /// <summary>True when there is room for one more thrall to start work.</summary>
        public static bool HasFreeSlot(Thrall joining)
        {
            if (joining != null && IsWork(joining.Job)) return true;
            return WorkingCount() < ThrallAltar.Slots;
        }

    }
}
