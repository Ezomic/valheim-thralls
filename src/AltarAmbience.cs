using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// A looping sound on the altar.
    ///
    /// The clip is lifted off a vanilla piece rather than shipped, for the same reason the
    /// stone and the wood are: anything of our own would be the one sound in earshot that
    /// was not mixed with the rest of the game. Borrowing means it sits at the world's own
    /// level, reverb and falloff for free, and it costs nothing in the plugin.
    /// </summary>
    internal static class AltarAmbience
    {
        private static AudioClip _clip;
        private static bool _searched;

        public static void Attach(Transform parent)
        {
            if (!ThrallConfig.AltarAmbience.Value) return;

            var clip = Clip();
            if (clip == null) return;

            var go = new GameObject("altar_ambience");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;

            // Fully positional, so it fades with distance instead of sitting in both ears
            // wherever you stand. Logarithmic rolloff is what the game's own emitters use.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 24f;
            source.volume = Mathf.Clamp(ThrallConfig.AltarAmbienceVolume.Value, 0f, 1f);

            // Onto the game's own mixer group where one is available, or the sound ignores
            // the player's effects-volume slider and cannot be turned down.
            var donor = Object.FindObjectOfType<AudioSource>();
            if (donor != null && donor.outputAudioMixerGroup != null)
                source.outputAudioMixerGroup = donor.outputAudioMixerGroup;

            source.Play();
        }

        private static AudioClip Clip()
        {
            if (_searched) return _clip;
            _searched = true;

            if (ZNetScene.instance == null) return null;

            foreach (var name in ThrallConfig.AltarAmbienceFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance.GetPrefab(name.Trim());
                if (donor == null) continue;

                foreach (var source in donor.GetComponentsInChildren<AudioSource>(true))
                {
                    // Looping sources only. A one-shot - a door, a hit, an unlock chime -
                    // would fire once and leave the altar silent again.
                    if (source.clip == null || !source.loop) continue;

                    _clip = source.clip;
                    ThrallsPlugin.Log.LogInfo("Altar ambience borrowed from " + name.Trim()
                                              + "/" + _clip.name);
                    return _clip;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no looping sound to borrow for the altar.");
            return null;
        }
    }
}
