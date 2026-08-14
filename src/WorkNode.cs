using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    internal enum ThrallJob
    {
        None = 0,
        Chop = 1,
        Mine = 2,
        Gather = 3,
        Follow = 4,
        Farm = 5,
        Repair = 6

        // 7 was Build, and it is retired rather than reused. The job is written to the
        // creature's ZDO as a plain int, so handing 7 to a new job would silently put
        // every thrall that was building when the feature was removed into that job
        // instead. Thrall.LoadState maps anything it does not recognise back to None.
    }

    /// <summary>How hard a particular thrall hits, which now depends on its rank.</summary>
    internal struct WorkPower
    {
        public int ToolTier;
        public float Chop;
        public float Pickaxe;

        /// <summary>
        /// Tier is the creature you hired and sets what materials it can touch at all;
        /// level is training on top, which only makes it hit harder and faster.
        /// </summary>
        public static WorkPower For(int tier, int level)
        {
            var t = Mathf.Max(0, ThrallBreed.Clamp(tier) - 1);
            var l = Mathf.Max(0, level - 1);

            var step = (1f + t * ThrallConfig.TierDamageStep.Value)
                       * (1f + l * ThrallConfig.LevelDamageStep.Value);

            return new WorkPower
            {
                ToolTier = ThrallConfig.ToolTier.Value + t,
                Chop = ThrallConfig.ChopDamage.Value * step,
                Pickaxe = ThrallConfig.PickaxeDamage.Value * step
            };
        }
    }

    /// <summary>
    /// One harvestable thing in the world, wrapped so the thrall does not have to care
    /// whether it is a tree, an ore vein, a rock or a berry bush.
    /// </summary>
    internal class WorkNode
    {
        public GameObject Root;
        public Collider Collider;
        public ThrallJob Job;

        private TreeBase _tree;
        private TreeLog _log;
        private MineRock5 _vein;
        private Destructible _destructible;
        private Pickable _pickable;

        public Vector3 Position
        {
            get { return Root != null ? Root.transform.position : Vector3.zero; }
        }

        /// <summary>Where to aim the blow - the middle of the mass, which may be well overhead.</summary>
        public Vector3 AimPoint
        {
            get
            {
                if (Collider != null) return Collider.bounds.center;
                return Position + Vector3.up;
            }
        }

        /// <summary>
        /// Where the thrall should stand. Deliberately not AimPoint: a tree's collider centre
        /// sits halfway up the trunk, and walking to a point in mid-air means never arriving.
        /// </summary>
        public Vector3 WalkPoint
        {
            get
            {
                if (Collider != null)
                {
                    var b = Collider.bounds;
                    return new Vector3(b.center.x, b.min.y, b.center.z);
                }
                return Position;
            }
        }

        /// <summary>Ground distance from a worker, ignoring how tall the thing is.</summary>
        public float GroundDistanceFrom(Vector3 from)
        {
            var flat = from - WalkPoint;
            flat.y = 0f;
            return flat.magnitude;
        }

        public int Id
        {
            get { return Root != null ? Root.GetInstanceID() : 0; }
        }

        /// <summary>A felled trunk is unfinished business, so it outranks anything still standing.</summary>
        public bool IsFelledLog
        {
            get { return _log != null; }
        }

        public bool Alive
        {
            get
            {
                if (Root == null) return false;
                if (_pickable != null) return _pickable.CanBePicked() && !_pickable.GetPicked();
                if (_vein != null) return !VeinFullyMined(_vein);
                return true;
            }
        }

        // MineRock5 keeps its per-chunk health private; this is the cheapest honest read of "is it gone".
        private static readonly MethodInfo AllDestroyedMethod =
            AccessTools.Method(typeof(MineRock5), "AllDestroyed");

        private static bool VeinFullyMined(MineRock5 vein)
        {
            if (AllDestroyedMethod == null) return false;
            try { return (bool)AllDestroyedMethod.Invoke(vein, null); }
            catch { return false; }
        }

        public string Label
        {
            get
            {
                if (Root == null) return "something";
                var n = Root.name.Replace("(Clone)", "");
                return n;
            }
        }

        /// <summary>Deals one swing worth of damage, or picks the thing if it is pickable.</summary>
        public void Work(Character worker, Humanoid workerHumanoid, WorkPower power)
        {
            if (Root == null) return;

            if (_pickable != null)
            {
                _pickable.Interact(workerHumanoid, false, false);
                return;
            }

            var hit = new HitData();
            hit.m_toolTier = (short)power.ToolTier;
            hit.m_point = AimPoint;
            hit.m_dir = (AimPoint - worker.transform.position).normalized;
            hit.m_hitCollider = ResolveCollider(worker.transform.position + Vector3.up);
            hit.SetAttacker(worker);

            if (Job == ThrallJob.Chop)
                hit.m_damage.m_chop = power.Chop;
            else
                hit.m_damage.m_pickaxe = power.Pickaxe;

            if (_tree != null) _tree.Damage(hit);
            else if (_log != null) _log.Damage(hit);
            else if (_vein != null) _vein.Damage(hit);
            else if (_destructible != null) _destructible.Damage(hit);
        }

        /// <summary>
        /// Ore veins split damage per collider, so aim at whatever part of the node
        /// the thrall can actually see rather than the collider we happened to find it by.
        /// </summary>
        private Collider ResolveCollider(Vector3 from)
        {
            if (_vein == null) return Collider;

            var dir = AimPoint - from;
            var dist = dir.magnitude;
            if (dist > 0.01f)
            {
                RaycastHit rh;
                if (Physics.Raycast(from, dir / dist, out rh, dist + 1f, Physics.DefaultRaycastLayers)
                    && rh.collider != null
                    && rh.collider.GetComponentInParent<MineRock5>() == _vein)
                {
                    return rh.collider;
                }
            }
            return Collider;
        }

        // ---------------------------------------------------------------- search

        /// <summary>Nearest workable node of the given job around <paramref name="center"/>.</summary>
        public static WorkNode FindNearest(ThrallJob job, Vector3 center, float radius,
            HashSet<int> ignore, int toolTier)
        {
            var seen = new HashSet<int>();
            var colliders = Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers);

            WorkNode best = null;
            var bestDist = float.MaxValue;
            WorkNode bestLog = null;
            var bestLogDist = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                var node = Classify(colliders[i], job, toolTier);
                if (node == null) continue;
                if (!seen.Add(node.Id)) continue;
                if (ignore != null && ignore.Contains(node.Id)) continue;
                if (!node.Alive) continue;

                var d = Vector3.Distance(center, node.Position);

                if (node.IsFelledLog)
                {
                    if (d < bestLogDist) { bestLogDist = d; bestLog = node; }
                }
                else if (d < bestDist)
                {
                    bestDist = d;
                    best = node;
                }
            }

            // Cut the trunk up before felling anything else.
            return bestLog ?? best;
        }

        /// <summary>Works out whether a collider is a valid target for the given job.</summary>
        public static WorkNode Classify(Collider col, ThrallJob job, int toolTier)
        {
            if (col == null) return null;

            // Farming harvests the same way gathering does; the difference is that a farmer
            // also sows, which happens outside the node system.
            if (job == ThrallJob.Gather || job == ThrallJob.Farm)
            {
                var pickable = col.GetComponentInParent<Pickable>();
                if (pickable == null) return null;
                return new WorkNode
                {
                    Root = pickable.gameObject,
                    Collider = col,
                    Job = job,
                    _pickable = pickable
                };
            }

            // Never let a thrall take an axe to the player's own buildings.
            if (col.GetComponentInParent<Piece>() != null) return null;
            if (col.GetComponentInParent<WearNTear>() != null) return null;

            var tier = toolTier;

            if (job == ThrallJob.Chop)
            {
                var tree = col.GetComponentInParent<TreeBase>();
                if (tree != null)
                    return tree.m_minToolTier > tier ? null
                        : new WorkNode { Root = tree.gameObject, Collider = col, Job = job, _tree = tree };

                var log = col.GetComponentInParent<TreeLog>();
                if (log != null)
                    return log.m_minToolTier > tier ? null
                        : new WorkNode { Root = log.gameObject, Collider = col, Job = job, _log = log };

                var stub = col.GetComponentInParent<Destructible>();
                if (stub != null && stub.GetDestructibleType() == DestructibleType.Tree)
                    return stub.m_minToolTier > tier ? null
                        : new WorkNode { Root = stub.gameObject, Collider = col, Job = job, _destructible = stub };

                return null;
            }

            if (job == ThrallJob.Mine)
            {
                var vein = col.GetComponentInParent<MineRock5>();
                if (vein != null)
                    return vein.m_minToolTier > tier ? null
                        : new WorkNode { Root = vein.gameObject, Collider = col, Job = job, _vein = vein };

                var rock = col.GetComponentInParent<Destructible>();
                if (rock != null && rock.GetDestructibleType() != DestructibleType.Tree && LooksMineable(rock.gameObject.name))
                    return rock.m_minToolTier > tier ? null
                        : new WorkNode { Root = rock.gameObject, Collider = col, Job = job, _destructible = rock };

                return null;
            }

            return null;
        }

        /// <summary>
        /// Job implied by whatever the player is pointing at. Anything growing on tilled
        /// soil - or the bare soil itself - reads as farm work rather than foraging.
        /// </summary>
        public static ThrallJob JobFor(Collider col, Vector3 point, int toolTier)
        {
            if (col == null) return ThrallJob.None;
            if (Classify(col, ThrallJob.Chop, toolTier) != null) return ThrallJob.Chop;
            if (Classify(col, ThrallJob.Mine, toolTier) != null) return ThrallJob.Mine;
            if (Classify(col, ThrallJob.Gather, toolTier) != null)
                return IsCultivated(point) ? ThrallJob.Farm : ThrallJob.Gather;

            // Anything the player built is upkeep work.
            if (col.GetComponentInParent<WearNTear>() != null) return ThrallJob.Repair;

            if (IsCultivated(point)) return ThrallJob.Farm;
            return ThrallJob.None;
        }

        public static bool IsCultivated(Vector3 point)
        {
            var heightmap = Heightmap.FindHeightmap(point);
            return heightmap != null && heightmap.IsCultivated(point);
        }

        private static bool LooksMineable(string name)
        {
            var lower = name.ToLowerInvariant();
            var fragments = ThrallConfig.ExtraMineableNames();
            for (int i = 0; i < fragments.Length; i++)
                if (lower.Contains(fragments[i])) return true;
            return false;
        }

        public static string JobName(ThrallJob job)
        {
            switch (job)
            {
                case ThrallJob.Chop: return "chopping wood";
                case ThrallJob.Mine: return "mining";
                case ThrallJob.Gather: return "gathering";
                case ThrallJob.Farm: return "farming";
                case ThrallJob.Repair: return "repairing";
                case ThrallJob.Follow: return "following you";
                default: return "idle";
            }
        }
    }
}
