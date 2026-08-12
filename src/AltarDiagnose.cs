using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Forces midday, waits for the sky to catch up, photographs the altar, then puts the
    /// time back.
    ///
    /// Colour and texel size cannot be judged in a night shot - two rounds of this were
    /// spent squinting at a bench lit by three candles. Setting the time and rendering in
    /// the same frame does not work either: EnvMan moves the sun in its own Update, so
    /// the light is still last frame's until a few have passed.
    /// </summary>
    internal class AltarDaylightShot : MonoBehaviour
    {
        private float _wait = 1.6f;
        private bool _set;
        private bool _wasDebug;
        private float _wasTime;

        private void Update()
        {
            var env = EnvMan.instance;

            if (!_set)
            {
                _set = true;
                if (env != null)
                {
                    _wasDebug = env.m_debugTimeOfDay;
                    _wasTime = env.m_debugTime;
                    env.m_debugTimeOfDay = true;
                    env.m_debugTime = 0.5f;
                }
                return;
            }

            _wait -= Time.deltaTime;
            if (_wait > 0f) return;

            AltarShot.Capture("daylight");

            if (env != null)
            {
                env.m_debugTimeOfDay = _wasDebug;
                env.m_debugTime = _wasTime;
            }

            Destroy(this);
        }
    }

    /// <summary>
    /// Runs the whole bisection by itself and photographs every step.
    ///
    /// The altar comes up wrapped in a large translucent box that no preview shows and no
    /// renderer listing accounts for. Rather than ask someone to press a key, look, and
    /// describe what changed - once per hypothesis - this switches each suspect off in
    /// turn, takes a picture of each, and leaves the set on disk to be compared.
    /// </summary>
    internal class AltarDiagnose : MonoBehaviour
    {
        private const float Step = 0.6f;

        private float _timer = 2f;
        private int _stage;

        private static readonly Dictionary<Renderer, Material[]> Saved =
            new Dictionary<Renderer, Material[]>();

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Step;

            switch (_stage)
            {
                case 0:
                    AltarShot.Capture("1_normal", false);
                    break;

                // The decisive one. With our own mesh switched off, anything still drawn
                // around the altar cannot be coming from the model.
                case 1:
                    Toggle("altar_model", false);
                    AltarShot.Capture("2_no_model", false);
                    break;

                case 2:
                    Toggle("altar_model", true);
                    Toggle("altar_motes", false);
                    AltarShot.Capture("3_no_motes", false);
                    break;

                case 3:
                    Toggle("altar_motes", true);
                    Vanilla(true);
                    AltarShot.Capture("4_vanilla_material", false);
                    break;

                case 4:
                    Vanilla(false);
                    HideProps(true);
                    AltarShot.Capture("5_no_props", false);
                    break;

                // Correct geometry, correct materials, dark hard-edged planes that only
                // exist while the mesh is drawn. That is the signature of the model
                // shadowing itself: two hundred intersecting boxes give the shadow map
                // enormous depth complexity, and the acne comes out as flat dark facets.
                case 5:
                    HideProps(false);
                    Shadows(false);
                    AltarShot.Capture("6_no_shadows", false);
                    break;

                default:
                    Shadows(true);
                    HideProps(false);
                    ThrallsPlugin.Log.LogInfo("Altar diagnosis complete.");
                    Destroy(this);
                    return;
            }

            _stage++;
        }

        private void Shadows(bool on)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name != "altar_model") continue;

                renderer.shadowCastingMode = on
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = on;
            }
        }

        private void Toggle(string objectName, bool on)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name == objectName) renderer.enabled = on;
        }

        private void Vanilla(bool on)
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.name != "altar_model") continue;

                if (!Saved.ContainsKey(renderer)) Saved[renderer] = renderer.sharedMaterials;

                if (!on)
                {
                    renderer.sharedMaterials = Saved[renderer];
                    continue;
                }

                var donor = AltarPrefab.DonorMaterial;
                if (donor == null) continue;

                var swap = new Material[Saved[renderer].Length];
                for (int i = 0; i < swap.Length; i++) swap[i] = donor;
                renderer.sharedMaterials = swap;
            }
        }

        /// <summary>
        /// Everything dressed onto the altar from vanilla prefabs. A prop is stripped of
        /// its components before it goes on, and a stripped prefab can still be carrying
        /// a mesh nobody asked for.
        /// </summary>
        private void HideProps(bool hide)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var name = renderer.gameObject.name;
                if (name == "altar_model" || name == "altar_motes") continue;

                renderer.enabled = !hide;
            }
        }
    }
}
