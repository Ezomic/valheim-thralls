using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Builds the drifting motes above each altar in code.
    ///
    /// The particle material is lifted off a vanilla effect, so the sprites, blending and
    /// fog all match the game rather than looking like a foreign overlay. Only the
    /// behaviour and colour are ours.
    /// </summary>
    internal static class AltarEffects
    {
        private static Material _spark;
        private static bool _searched;

        /// <summary>Colour, rise speed, rate and spread for each altar's motes.</summary>
        private static bool Recipe(string key, out Color colour, out float rate,
            out float rise, out float radius, out float height)
        {
            switch (key)
            {
                case "shrine":
                    colour = new Color(1.00f, 0.86f, 0.45f); rate = 5f; rise = 0.16f;
                    radius = 0.7f; height = 1.1f; return true;

                // Eitr green rather than firelight: warm embers said forge, and the
                // bench is not a forge.
                case "worktable":
                    colour = new Color(0.45f, 0.92f, 0.78f); rate = 8f; rise = 0.14f;
                    radius = 1.1f; height = 1.4f; return true;

                case "plinth":
                    colour = new Color(0.95f, 0.93f, 0.80f); rate = 6f; rise = 0.12f;
                    radius = 1.5f; height = 1.6f; return true;

                case "dolmen":
                    colour = new Color(0.45f, 0.70f, 1.00f); rate = 8f; rise = 0.10f;
                    radius = 1.6f; height = 2.4f; return true;

                case "cairn":
                    colour = new Color(1.00f, 0.62f, 0.28f); rate = 10f; rise = 0.30f;
                    radius = 1.1f; height = 1.4f; return true;

                case "circle":
                    colour = new Color(0.55f, 0.95f, 0.45f); rate = 9f; rise = 0.08f;
                    radius = 2.1f; height = 1.5f; return true;

                case "barrow":
                    colour = new Color(0.45f, 1.00f, 0.55f); rate = 12f; rise = 0.06f;
                    radius = 2.0f; height = 1.2f; return true;

                // The bindstone inherits the bench's eitr green, drawn in tighter: it is
                // half the width, so the bench's spread left motes hanging in open air
                // either side of it.
                case "bindstone":
                    colour = new Color(0.45f, 0.92f, 0.78f); rate = 7f; rise = 0.14f;
                    radius = 0.7f; height = 1.3f; return true;

                // The upgrades get their own, thinner. Without a case here they were the
                // only pieces in the set standing dead at night, and the golem's crystal
                // is a bright material rather than a light - the eyes need this to read
                // once the sun is down.
                case "upgrade1":
                    colour = new Color(0.52f, 0.86f, 0.40f); rate = 4f; rise = 0.07f;
                    radius = 0.45f; height = 0.9f; return true;

                case "upgrade2":
                    colour = new Color(0.55f, 0.90f, 1.00f); rate = 4f; rise = 0.10f;
                    radius = 0.5f; height = 1.1f; return true;

                case "upgrade3":
                    colour = new Color(1.00f, 0.72f, 0.38f); rate = 4f; rise = 0.12f;
                    radius = 0.4f; height = 1.5f; return true;

                default:
                    colour = Color.white; rate = 0f; rise = 0f; radius = 0f; height = 0f;
                    return false;
            }
        }

        public static void Attach(Transform parent, string key)
        {
            if (!ThrallConfig.AltarEffects.Value) return;

            Color colour;
            float rate, rise, radius, height;
            if (!Recipe(key, out colour, out rate, out rise, out radius, out height)) return;

            var material = SparkMaterial();
            if (material == null)
            {
                ThrallsPlugin.Log.LogWarning("No particle material to borrow; skipping altar motes.");
                return;
            }

            var go = new GameObject("altar_motes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height * 0.35f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.4f, rise);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = colour;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.008f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = rate * Mathf.Clamp(ThrallConfig.AltarEffectStrength.Value, 0f, 4f);

            // Emitted from a flat disc around the stone so they drift up off the whole piece.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = radius;
            shape.donutRadius = radius * 0.45f;

            // Fade in and back out so nothing pops.
            var colourOverLife = ps.colorOverLifetime;
            colourOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f),
                    new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f)
                });
            colourOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            // A little sideways wander, so they do not rise in straight lines.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.15f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();

            AddLight(parent, colour, height);
        }

        /// <summary>
        /// A light of our own on the altar.
        ///
        /// The obvious route was to hang vanilla torches off it, but a prop is stripped
        /// of every component before it goes on - it has to be, or it would be a live
        /// fireplace welded to a piece - and a torch with its Fireplace gone never lights.
        /// The candles that do survive throw almost nothing. So the altar carries its own.
        /// </summary>
        private static void AddLight(Transform parent, Color colour, float height)
        {
            if (!ThrallConfig.AltarLight.Value) return;

            var go = new GameObject("altar_light");
            go.transform.SetParent(parent, false);

            // Well above the piece rather than inside it. A point light sitting a hand's
            // breadth off the bench top is a hand's breadth from the surface it is
            // lighting, and inverse-square does the rest: the boards blow out to a white
            // glare while everything past them stays dark. Hung overhead it lights the
            // altar instead of scorching it.
            go.transform.localPosition = new Vector3(0f, Mathf.Max(1.2f, height) + 1.3f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;

            // Pulled hard towards white. At full saturation the altar lit everything
            // around it in flat green, which looks like a bug rather than an atmosphere.
            light.color = Color.Lerp(colour, Color.white, 0.70f);
            light.range = Mathf.Max(1f, ThrallConfig.AltarLightRange.Value);

            // Measured against a real fire rather than picked out of the air. What an
            // intensity of "1.7" actually looks like depends on the game's tonemapping
            // and render settings, which is why guessing at it came out too dark twice.
            // A multiple of what Valheim itself gives a campfire is a unit that means
            // something.
            light.intensity = FireIntensity() * Mathf.Max(0f, ThrallConfig.AltarLightStrength.Value);

            // No shadows: one light per altar is cheap, but a shadow-casting point light
            // is six render passes, and several altars in a base would be felt.
            light.shadows = LightShadows.None;

            // Auto, not ForcePixel. Every light Valheim places is left for the engine to
            // rank, and forcing this one to the front of a budget of eight pixel lights
            // means shoving one of the game's own out of it.
            light.renderMode = LightRenderMode.Auto;

            // LightLod is how the game manages every one of its own lights: it fades
            // range in with distance and drops the light entirely past its budget.
            // Without one this light sits outside that system, always on at full range
            // no matter how many others are competing with it.
            var lod = go.AddComponent<LightLod>();
            lod.m_lightLod = true;
            lod.m_lightDistance = Mathf.Max(20f, light.range * 2.5f);
            lod.m_shadowLod = false;

            go.AddComponent<AltarGlow>();
        }

        private static bool _lightLearned;
        private static float _fireIntensity = 1.6f;

        /// <summary>
        /// What Valheim gives one of its own fires. Used as the unit the altar's light is
        /// measured in, so "brighter than a campfire" means the same thing here as it does
        /// in the game.
        /// </summary>
        private static float FireIntensity()
        {
            if (_lightLearned) return _fireIntensity;
            _lightLearned = true;

            foreach (var name in ThrallConfig.AltarEffectFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance != null
                    ? ZNetScene.instance.GetPrefab(name.Trim())
                    : null;
                if (donor == null) continue;

                foreach (var light in donor.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Point || light.intensity <= 0f) continue;

                    _fireIntensity = light.intensity;
                    ThrallsPlugin.Log.LogInfo(string.Format(
                        "Altar light measured against {0}: intensity {1}, range {2}. "
                        + "Pixel light budget is {3}.",
                        name.Trim(), light.intensity, light.range,
                        QualitySettings.pixelLightCount));
                    return _fireIntensity;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no vanilla fire light to measure against; "
                                         + "using " + _fireIntensity + ".");
            return _fireIntensity;
        }

        /// <summary>Borrows a particle material from a vanilla fire so blending matches.</summary>
        private static Material SparkMaterial()
        {
            if (_searched) return _spark;
            _searched = true;

            foreach (var name in ThrallConfig.AltarEffectFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance != null
                    ? ZNetScene.instance.GetPrefab(name.Trim())
                    : null;
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;
                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null) continue;

                    ThrallsPlugin.Log.LogInfo("Altar motes use " + material.name
                                              + " from " + name.Trim());
                    _spark = material;
                    return _spark;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Switches the altar's light and motes on and off on every altar in the scene, so
    /// which of them is responsible for something can be settled by looking rather than
    /// by a rebuild and a restart per guess.
    /// </summary>
    internal static class AltarDebug
    {
        private static int _state;

        private static readonly string[] Names =
        {
            "everything on",
            "light OFF",
            "motes OFF",
            "VANILLA STONE material",
            "vertex colours WHITE"
        };

        public static void Cycle()
        {
            _state = (_state + 1) % Names.Length;

            var wantLight = _state != 1;
            var wantMotes = _state != 2;

            SetMaterials(_state == 3);
            SetVertexColours(_state == 4);

            var lights = 0;
            var systems = 0;

            // Muted through the glow component rather than by switching the Light off:
            // LightLod runs a coroutine that turns lights back on as you approach, so
            // anything toggling 'enabled' would simply be undone a second later.
            foreach (var glow in Object.FindObjectsOfType<AltarGlow>())
            {
                glow.Muted = !wantLight;
                lights++;
            }

            foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
            {
                if (ps.gameObject.name != "altar_motes") continue;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = wantMotes;

                if (wantMotes) ps.Play(); else ps.Clear();
                systems++;
            }

            var report = "Altar test " + (_state + 1) + "/" + Names.Length + ": " + Names[_state];

            ThrallsPlugin.Log.LogInfo(report);
            if (Player.m_localPlayer != null)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, report, 0, null);

            // A photograph of each state, so the comparison can be made by looking at the
            // pictures rather than by asking someone to describe them.
            AltarShot.Capture(Names[_state]);
        }

        private static readonly Dictionary<Renderer, Material[]> Original =
            new Dictionary<Renderer, Material[]>();

        /// <summary>
        /// Swaps the altar onto the untouched vanilla stone material.
        ///
        /// This is the test that actually splits the problem in two. If the black facets
        /// survive a plain vanilla material, nothing about our textures, UVs or normal maps
        /// can be causing them and the fault is in the mesh itself. If they vanish, it is
        /// something we put on the material and the mesh is fine.
        /// </summary>
        private static void SetMaterials(bool vanilla)
        {
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.gameObject.name != "altar_model") continue;

                if (!Original.ContainsKey(renderer))
                    Original[renderer] = renderer.sharedMaterials;

                if (!vanilla)
                {
                    renderer.sharedMaterials = Original[renderer];
                    continue;
                }

                var donor = AltarPrefab.DonorMaterial;
                if (donor == null) continue;

                var swap = new Material[Original[renderer].Length];
                for (int i = 0; i < swap.Length; i++) swap[i] = donor;
                renderer.sharedMaterials = swap;
            }
        }

        private static readonly Dictionary<Mesh, Color[]> Baked = new Dictionary<Mesh, Color[]>();

        /// <summary>
        /// Blanks the vertex colours. Valheim's piece shader may use those channels for
        /// blending or wear rather than as a plain tint, in which case the occlusion baked
        /// into them is telling the shader something quite different from "darker here".
        /// </summary>
        private static void SetVertexColours(bool white)
        {
            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.gameObject.name != "altar_model") continue;

                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                if (!Baked.ContainsKey(mesh)) Baked[mesh] = mesh.colors;

                if (!white)
                {
                    if (Baked[mesh] != null && Baked[mesh].Length == mesh.vertexCount)
                        mesh.colors = Baked[mesh];
                    continue;
                }

                var blank = new Color[mesh.vertexCount];
                for (int i = 0; i < blank.Length; i++) blank[i] = Color.white;
                mesh.colors = blank;
            }
        }
    }

    /// <summary>
    /// Breathes the altar light in and out. A point light held at one intensity reads as
    /// a lamp; every light in Valheim that is meant to be fire moves.
    /// </summary>
    internal class AltarGlow : MonoBehaviour
    {
        private Light _light;
        private float _full;
        private float _seed;

        /// <summary>Held dark for the effects toggle, without fighting LightLod.</summary>
        public bool Muted;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) _full = _light.intensity;
            _seed = UnityEngine.Random.value * 128f;
        }

        private void Update()
        {
            if (_light == null) return;

            if (Muted)
            {
                _light.intensity = 0f;
                return;
            }

            // Two rates beaten together, so it wanders rather than pulsing on a timer.
            var slow = Mathf.PerlinNoise(_seed, Time.time * 0.7f);
            var fast = Mathf.PerlinNoise(_seed + 31f, Time.time * 2.9f);

            _light.intensity = _full * (0.80f + slow * 0.22f + fast * 0.10f);
        }
    }
}
