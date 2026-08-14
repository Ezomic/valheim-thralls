using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// draws the sparkling line from an upgrade to the altar it belongs to, the way a
    /// chopping block draws one to its workbench.
    ///
    /// not a StationExtension, though the effect is lifted straight from one. That
    /// component resolves its target through CraftingStation.FindClosestStationInRange,
    /// so the altar would have to BE a CraftingStation - and CraftingStation implements
    /// Hoverable and Interactable both, which would take the altar's Hover text and its
    /// E key away from the thrall panel. The line is worth having; the crafting UI
    /// opening instead of the panel is not.
    ///
    /// the maths is StationExtension.StartConnectionEffect verbatim: Instantiate at the
    /// upgrade, look at the target, and stretch e to the Distance between them.
    /// </summary>
    internal class UpgradeLink : MonoBehaviour
    {
        private GameObject _line;
        private ZNetView _nview;
        private float _timer;

        private static GameObject _prefab;
        private static bool _looked;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();

            // straight away on placement, which is the moment the line is actually for -
            // it is what tells you the thing you just raised found its altar.
            Poke(2.5f);
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid()) return;

            // only while somebody is Close enough to see it, and only every few Seconds.
            // vanilla's continuous extensions re-Poke on a 4 second repeat; this matches
            // that rather than holding a line up permanently.
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 4f;

            var player = Player.m_localPlayer;
            if (player == null) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 14f) return;

            Poke(4.5f);
        }

        private void Poke(float timeout)
        {
            if (!ThrallConfig.UpgradeLinks.Value) return;

            var altar = NearestAltar();
            if (altar == null) return;

            var prefab = Effect();
            if (prefab == null) return;

            var from = transform.position + Vector3.up * ThrallConfig.UpgradeLinkHeight.Value;
            var to = altar.transform.position + Vector3.up * ThrallConfig.UpgradeLinkHeight.Value;
            var span = to - from;
            if (span.sqrMagnitude < 0.0001f) return;

            if (_line == null) _line = Instantiate(prefab, from, Quaternion.identity);

            _line.transform.position = from;
            _line.transform.rotation = Quaternion.LookRotation(span.normalized);
            _line.transform.localScale = new Vector3(1f, 1f, span.magnitude);

            CancelInvoke("Stop");
            Invoke("Stop", timeout);
        }

        private void Stop()
        {
            if (_line == null) return;
            Destroy(_line);
            _line = null;
        }

        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// the altar this upgrade answers to: the nearest one inside the same range the
        /// upgrade chain itself uses, so the line cannot claim a link the mod would not
        /// Count.
        /// </summary>
        private ThrallAltar NearestAltar()
        {
            var range = Mathf.Max(2f, ThrallConfig.SlotSearchRange.Value);
            var best = ThrallAltar.NearestTo(transform.position, range);
            return best;
        }

        private static GameObject Effect()
        {
            if (_looked) return _prefab;
            _looked = true;

            if (ZNetScene.instance == null)
            {
                _looked = false;
                return null;
            }

            foreach (var Name in (ThrallConfig.UpgradeLinkFrom.Value ?? "").Split(','))
            {
                var trimmed = Name.Trim();
                if (trimmed.Length == 0) continue;

                var found = ZNetScene.instance.GetPrefab(trimmed);
                if (found == null) continue;

                _prefab = found;
                ThrallsPlugin.Log.LogInfo("upgrade link drawn with " + trimmed + ".");
                return _prefab;
            }

            ThrallsPlugin.Log.LogWarning("No connection effect found; upgrades will not Draw a line.");
            return null;
        }
    }
}
