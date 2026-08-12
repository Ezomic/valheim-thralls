using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>A piece the player has ordered built, but nobody has built yet.</summary>
    internal class BuildPlan
    {
        public string PrefabName;
        public Vector3 Position;
        public Quaternion Rotation;
        public GameObject Ghost;

        public GameObject Prefab
        {
            get { return ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PrefabName) : null; }
        }

        public Piece Piece
        {
            get
            {
                var prefab = Prefab;
                return prefab != null ? prefab.GetComponent<Piece>() : null;
            }
        }
    }

    /// <summary>
    /// The build queue. It lives on the steward's ZDO rather than in a loose file, so the
    /// orders belong to the world and survive relogs the same way the thralls do.
    /// </summary>
    internal static class BuildPlans
    {
        public const string ZKey = "thrallBuildPlans";

        private static readonly List<BuildPlan> Plans = new List<BuildPlan>();
        private static ThrallAltar _boundTo;
        private static float _ghostTimer;

        public static int Count { get { return Plans.Count; } }
        public static IList<BuildPlan> All { get { return Plans; } }

        // ---------------------------------------------------------------- storage

        private static ZNetView Ledger()
        {
            var altar = ThrallAltar.Current;
            return altar != null && altar.Usable ? altar.View : null;
        }

        public static bool HasLedger { get { return Ledger() != null; } }

        private static void Load()
        {
            ClearGhosts();
            Plans.Clear();

            var nview = Ledger();
            if (nview == null) return;

            var blob = nview.GetZDO().GetByteArray(ZKey, null);
            if (blob == null || blob.Length == 0) return;

            try
            {
                var pkg = new ZPackage(blob);
                var count = pkg.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    Plans.Add(new BuildPlan
                    {
                        PrefabName = pkg.ReadString(),
                        Position = pkg.ReadVector3(),
                        Rotation = pkg.ReadQuaternion()
                    });
                }
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not read the build ledger: " + e.Message);
                Plans.Clear();
            }
        }

        private static void Save()
        {
            var nview = Ledger();
            if (nview == null) return;

            nview.ClaimOwnership();

            var pkg = new ZPackage();
            pkg.Write(Plans.Count);
            for (int i = 0; i < Plans.Count; i++)
            {
                pkg.Write(Plans[i].PrefabName);
                pkg.Write(Plans[i].Position);
                pkg.Write(Plans[i].Rotation);
            }
            nview.GetZDO().Set(ZKey, pkg.GetArray());
        }

        // ---------------------------------------------------------------- queue

        public static bool Add(string prefabName, Vector3 pos, Quaternion rot)
        {
            if (Ledger() == null) return false;

            Plans.Add(new BuildPlan { PrefabName = prefabName, Position = pos, Rotation = rot });
            Save();
            return true;
        }

        public static void Remove(BuildPlan plan)
        {
            if (plan == null) return;
            if (plan.Ghost != null) Object.Destroy(plan.Ghost);
            Plans.Remove(plan);
            Save();
        }

        public static void Clear()
        {
            ClearGhosts();
            Plans.Clear();
            Save();
        }

        public static BuildPlan Nearest(Vector3 pos, float radius)
        {
            BuildPlan best = null;
            var bestDist = float.MaxValue;

            for (int i = 0; i < Plans.Count; i++)
            {
                var d = Vector3.Distance(pos, Plans[i].Position);
                if (d > radius || d >= bestDist) continue;
                if (Plans[i].Prefab == null) continue;
                bestDist = d;
                best = Plans[i];
            }
            return best;
        }

        public static bool AnyNear(Vector3 pos, float radius)
        {
            return Nearest(pos, radius) != null;
        }

        /// <summary>Reloads when the steward changes and keeps the ghosts in step with the queue.</summary>
        public static void Tick(float dt)
        {
            if (ThrallAltar.Current != _boundTo)
            {
                _boundTo = ThrallAltar.Current;
                Load();
            }

            _ghostTimer -= dt;
            if (_ghostTimer > 0f) return;
            _ghostTimer = 2f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            for (int i = 0; i < Plans.Count; i++)
            {
                var plan = Plans[i];
                var near = Vector3.Distance(player.transform.position, plan.Position) < 96f;

                if (near && plan.Ghost == null)
                {
                    plan.Ghost = MakeGhost(plan);
                }
                else if (!near && plan.Ghost != null)
                {
                    Object.Destroy(plan.Ghost);
                    plan.Ghost = null;
                }
            }
        }

        private static void ClearGhosts()
        {
            for (int i = 0; i < Plans.Count; i++)
            {
                if (Plans[i].Ghost == null) continue;
                Object.Destroy(Plans[i].Ghost);
                Plans[i].Ghost = null;
            }
        }

        // ---------------------------------------------------------------- ghosts

        /// <summary>
        /// A look-but-do-not-touch copy of the piece. ZNetView init is forced off so this
        /// never becomes a real networked object, and every behaviour is switched off so it
        /// cannot tick, burn, spawn or make noise.
        /// </summary>
        private static GameObject MakeGhost(BuildPlan plan)
        {
            var prefab = plan.Prefab;
            if (prefab == null) return null;

            GameObject ghost;
            ZNetView.m_forceDisableInit = true;
            try { ghost = Object.Instantiate(prefab, plan.Position, plan.Rotation); }
            finally { ZNetView.m_forceDisableInit = false; }

            ghost.name = "thrall_plan_" + plan.PrefabName;

            foreach (var joint in ghost.GetComponentsInChildren<Joint>()) Object.Destroy(joint);
            foreach (var body in ghost.GetComponentsInChildren<Rigidbody>()) Object.Destroy(body);
            foreach (var col in ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var light in ghost.GetComponentsInChildren<Light>()) Object.Destroy(light);
            foreach (var ps in ghost.GetComponentsInChildren<ParticleSystem>()) ps.gameObject.SetActive(false);
            foreach (var audio in ghost.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
            foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;

            var ghostLayer = LayerMask.NameToLayer("ghost");
            if (ghostLayer >= 0)
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = ghostLayer;

            Tint(ghost);
            return ghost;
        }

        private static void Tint(GameObject ghost)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(0.4f, 0.8f, 1f, 0.5f));

            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
