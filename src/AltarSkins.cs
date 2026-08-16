using System.Collections.Generic;
using HarmonyLib;
using System.IO;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// How the altar and the depot are dressed: the materials they borrow from the game,
    /// the texture sheets shipped beside the plugin, and the UV work that makes one sit on
    /// the other.
    ///
    /// Split out of ThrallAltar.cs, which had grown to 1,700 lines holding three unrelated
    /// jobs - the component that runs on a placed altar, the builder that assembles the
    /// prefabs, and this. It is a partial class rather than a new one on purpose: every
    /// member below was private to AltarPrefab and still is, so this is a move with no
    /// change of access and no new seam to keep in step.
    ///
    /// The two rules everything here exists to obey:
    ///
    ///   - never swap _MainTex on a borrowed material. The donor's normal map comes with
    ///     it and is then sampled through our UVs, which lights flat surfaces as black
    ///     shards. Ours are built on a copy, with the normal optionally flattened.
    ///   - Valheim's textures are atlases. A material uses a strip of a sheet, so UVs
    ///     running 0..1 sample the whole thing and pick up the neighbouring tiles. Hence
    ///     StoneUvRegion, measured off the donor's largest triangle, and FitUvs remapping
    ///     into it clamped rather than wrapped.
    /// </summary>
    internal static partial class AltarPrefab
    {
        private static Material _stone;
        private static Rect _stoneUv = new Rect(0f, 0f, 1f, 1f);
        private static bool _stoneUvKnown;

        /// <summary>
        /// Each altar wears its own stone. The texture is ours, so it owns the whole 0-1 UV
        /// square and none of the shared-atlas juggling applies. The Valheim material is
        /// still the base, which keeps the game's shader, lighting, wetness and snow.
        /// </summary>
        private static Material SkinFor(string modelFile, string group, Mesh model)
        {
            var key = modelFile + "|" + group;

            Material skin;
            if (Skins.TryGetValue(key, out skin)) return skin;

            // Wood groups are skinned off a wooden piece, everything else off stone, so a
            // group handed back its donor untouched gets the right donor to be handed.
            var wooden = !string.IsNullOrEmpty(group)
                         && ("," + (ThrallConfig.AltarVanillaWoodGroups.Value ?? "").Replace(" ", "") + ",")
                            .IndexOf("," + group + ",", System.StringComparison.OrdinalIgnoreCase) >= 0;

            var basis = wooden ? BorrowWoodMaterial() : BorrowStoneMaterial();
            if (basis == null)
            {
                ThrallsPlugin.Log.LogWarning("No " + (wooden ? "wood" : "stone")
                                             + " material to skin the bindstone with.");
                return null;
            }

            var dir = Path.GetDirectoryName(typeof(AltarPrefab).Assembly.Location);
            var stem = Path.GetFileNameWithoutExtension(modelFile);

            // Groups that are handed straight to the game's own material instead of one of
            // ours. Measured against Valheim's stone on the same mesh, a sheet of ours came
            // out at 84% of its brightness and 66% of its contrast, and the contrast half of
            // that gap is not closable here: the branch below flattens the normals, so the
            // normal map the vanilla stone gets its highlights from is switched off. Rather
            // than keep chasing that with albedo, stone can simply be the game's stone.
            Texture2D texture = null;
            var vanillaGroups = ThrallConfig.AltarVanillaGroups.Value ?? "";
            var useVanilla = wooden
                             || (!string.IsNullOrEmpty(group)
                                 && ("," + vanillaGroups.Replace(" ", "") + ",")
                                    .IndexOf("," + group + ",", System.StringComparison.OrdinalIgnoreCase) >= 0);

            // thrall_altar_worktable_iron.png for a named group, falling back to the one
            // sheet the single-material altars use.
            if (!useVanilla)
            {
                if (!string.IsNullOrEmpty(group))
                    texture = LoadTexture(Path.Combine(dir, stem + "_" + group + ".png"));

                if (texture == null) texture = LoadTexture(Path.Combine(dir, stem + ".png"));
            }

            if (texture == null)
            {
                // No texture of our own, so fall back to the borrowed material and squeeze
                // the UVs into whatever patch of its atlas the donor actually uses.
                //
                // Only the first group to get here does the fitting - the UVs belong to the
                // mesh, not the submesh, so stone and wood cannot each have their own patch.
                // Whichever is listed first in the OBJ wins and the other samples through
                // it, which is survivable while both donors use most of their sheet.
                var rect = wooden ? _woodUv : _stoneUv;
                var rectKnown = wooden ? _woodUvKnown : _stoneUvKnown;

                if (rectKnown && Fitted.Add(model))
                    FitUvs(model, rect, Mathf.Max(0.01f, ThrallConfig.AltarUvScale.Value));

                Skins[key] = basis;
                return basis;
            }

            skin = new Material(basis)
            {
                name = "thrall_" + stem + (string.IsNullOrEmpty(group) ? "" : "_" + group)
            };
            skin.SetTexture("_MainTex", texture);

            if (ThrallConfig.AltarFlattenNormals.Value) FlattenNormals(skin);

            // Guarded on the mesh, not the material: the UVs are shared by every group,
            // so tiling them once per group would compound the scale.
            if (Fitted.Add(model))
                TileUvs(model, Mathf.Max(0.01f, ThrallConfig.AltarUvScale.Value));

            Skins[key] = skin;
            ThrallsPlugin.Log.LogInfo("Skin built for " + key
                                      + " (" + texture.width + "x" + texture.height + ")");
            return skin;
        }

        private static Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!DecodePng(texture, File.ReadAllBytes(path))) return null;

                texture.wrapMode = TextureWrapMode.Repeat;

                // Point sampling, so texels stay square instead of being blurred into
                // each other. Bilinear on an already low resolution sheet gives a soft,
                // smeared surface, which is the opposite of the crisp blocky look the
                // game's own props have close up.
                texture.filterMode = ThrallConfig.AltarTexturePoint.Value
                    ? FilterMode.Point
                    : FilterMode.Bilinear;

                texture.anisoLevel = ThrallConfig.AltarTexturePoint.Value ? 0 : 4;

                // Mipmaps stay on regardless: without them a point sampled texture
                // shimmers badly the moment you walk away from it.
                texture.Apply(true, false);
                return texture;
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        private static System.Reflection.MethodInfo _loadImage;

        /// <summary>
        /// ImageConversion.LoadImage, reached by reflection: its assembly targets a newer
        /// netstandard than this one and cannot be referenced at compile time.
        /// </summary>
        private static bool DecodePng(Texture2D texture, byte[] bytes)
        {
            if (_loadImage == null)
            {
                var type = System.Type.GetType(
                    "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");

                if (type == null)
                {
                    ThrallsPlugin.Log.LogWarning("No image decoder available in this build.");
                    return false;
                }

                _loadImage = AccessTools.Method(type, "LoadImage",
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            }
            if (_loadImage == null) return false;

            var result = _loadImage.Invoke(null, new object[] { texture, bytes, false });
            return result is bool && (bool)result;
        }

        /// <summary>
        /// Valheim's shaders do not all call the normal map "_BumpMap" - Custom/Piece uses
        /// its own names. Only checking that one name meant that on this shader the switch
        /// silently did nothing and the donor's ATLAS normal map stayed on the material,
        /// sampled with our own tiled UVs. That gives every face a normal taken from an
        /// unrelated patch of somebody else's texture, which is what lights flat surfaces
        /// as black shards with hard highlights across them.
        ///
        /// So every name the game might be using gets a flat map, and what was actually
        /// found is logged rather than assumed.
        /// </summary>
        private static readonly string[] NormalProperties =
        {
            "_BumpMap", "_MainNormal", "_NormalMap", "_Normal", "_BumpMap2",
            "_MainBump", "_NormalTex", "_MainTexNormal", "_DetailNormalMap"
        };

        private static bool _normalsLogged;

        private static void FlattenNormals(Material skin)
        {
            var found = new List<string>();
            var all = new List<string>();

            // Ask the shader what it actually has rather than guessing at names. This is
            // the whole reason the first attempt did nothing: "_BumpMap" is the Standard
            // shader's name for it, and Custom/Piece is not the Standard shader.
            if (!Enumerate(skin, found, all))
            {
                for (int i = 0; i < NormalProperties.Length; i++)
                {
                    if (!skin.HasProperty(NormalProperties[i])) continue;
                    skin.SetTexture(NormalProperties[i], FlatNormal());
                    found.Add(NormalProperties[i]);
                }
            }

            if (_normalsLogged) return;
            _normalsLogged = true;

            ThrallsPlugin.Log.LogInfo("Shader " + skin.shader.name + " textures: ["
                                      + string.Join(", ", all.ToArray()) + "]");

            ThrallsPlugin.Log.LogInfo(found.Count > 0
                ? "Flattened: " + string.Join(", ", found.ToArray())
                : "No normal map property found - the donor's own normals are still in "
                  + "place and will be sampled with our UVs.");
        }

        /// <summary>
        /// Walks the shader's own property list, flattening every texture that is a normal
        /// map. Returns false if this build of Unity will not tell us, so the guessed list
        /// can be used instead.
        /// </summary>
        private static bool Enumerate(Material skin, List<string> found, List<string> all)
        {
            var shader = skin.shader;
            if (shader == null) return false;

            try
            {
                var count = shader.GetPropertyCount();

                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                        continue;

                    var name = shader.GetPropertyName(i);
                    all.Add(name);

                    // Unity flags normal maps on the property itself, which is exact.
                    // The name check is a safety net for shaders that do not set it.
                    var flags = shader.GetPropertyFlags(i);
                    var isNormal =
                        (flags & UnityEngine.Rendering.ShaderPropertyFlags.Normal) != 0
                        || name.IndexOf("ormal", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("ump", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isNormal) continue;

                    skin.SetTexture(name, FlatNormal());
                    found.Add(name);
                }

                return true;
            }
            catch (System.Exception e)
            {
                ThrallsPlugin.Log.LogWarning("Cannot read shader properties: " + e.Message);
                return false;
            }
        }

        private static Texture2D _flatNormal;

        private static Texture2D FlatNormal()
        {
            if (_flatNormal != null) return _flatNormal;

            _flatNormal = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            // Alpha 0.5, not 1. Unity has two normal-map encodings and a shader may use
            // either: plain RGB reads the normal from r,g,b, while DXT5nm reads x from
            // ALPHA and y from green. With alpha at 1 the second reading gives x = 1,
            // y = 0, z = 0 - a tangent normal lying flat along the surface instead of
            // standing up out of it. Every face is then lit as though it points 90 degrees
            // away from where it actually points, which turns flat walls black and throws
            // hard specular streaks across them.
            //
            // (0.5, 0.5, 1, 0.5) decodes to straight up under both readings.
            var flat = new Color(0.5f, 0.5f, 1f, 0.5f);
            _flatNormal.SetPixels(new[] { flat, flat, flat, flat });
            _flatNormal.Apply();
            return _flatNormal;
        }

        /// <summary>Plain repeat across our own texture, scaled to taste.</summary>
        private static void TileUvs(Mesh mesh, float scale)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            for (int i = 0; i < uv.Length; i++) uv[i] *= scale;
            mesh.uv = uv;
        }

        /// <summary>
        /// Valheim's stone pieces share one atlas: the stone occupies a patch of it and the
        /// rest is other material entirely. UVs that run across the whole sheet therefore
        /// land half on stone and half on blank, which is the checkerboard. Reading the
        /// donor mesh's own UV bounds tells us which patch is actually the stone.
        /// </summary>
        private static Rect StoneUvRegion(Renderer donorRenderer)
        {
            var configured = ThrallConfig.AltarUvRegion.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                var f = configured.Split(',');
                if (f.Length == 4)
                {
                    return new Rect(ParseFloat(f[0], 0f), ParseFloat(f[1], 0f),
                        ParseFloat(f[2], 1f), ParseFloat(f[3], 1f));
                }
            }

            var filter = donorRenderer != null ? donorRenderer.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return new Rect(0f, 0f, 1f, 1f);

            Vector2[] uv;
            try
            {
                // Imported meshes are frequently upload-only; reading them then throws.
                if (!mesh.isReadable) return new Rect(0f, 0f, 1f, 1f);
                uv = mesh.uv;
            }
            catch { return new Rect(0f, 0f, 1f, 1f); }

            if (uv == null || uv.Length == 0) return new Rect(0f, 0f, 1f, 1f);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            var size = max - min;
            if (size.x <= 0.001f || size.y <= 0.001f) return new Rect(0f, 0f, 1f, 1f);

            return new Rect(min.x, min.y, size.x, size.y);
        }

        /// <summary>Folds the mesh's UVs into the stone patch, tiling within it.</summary>
        private static void FitUvs(Mesh mesh, Rect region, float scale)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            for (int i = 0; i < uv.Length; i++)
            {
                // Repeat inside the patch rather than across the whole sheet.
                var u = Mathf.Repeat(uv[i].x * scale, 1f);
                var v = Mathf.Repeat(uv[i].y * scale, 1f);

                uv[i] = new Vector2(region.x + u * region.width, region.y + v * region.height);
            }

            mesh.uv = uv;
        }

        /// <summary>
        /// Lifts a material off a real stone piece. Picks the renderer with the most
        /// submeshes-worth of texture rather than the first one found, because the first
        /// child of a piece is often a tiny detail mesh with a flat material on it.
        /// </summary>
        /// <summary>The untouched vanilla material, for comparing ours against.</summary>
        public static Material DonorMaterial { get { return BorrowStoneMaterial(); } }

        private static Material BorrowStoneMaterial()
        {
            if (_stone != null) return _stone;

            foreach (var name in ThrallConfig.AltarMaterialFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance.GetPrefab(name.Trim());
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    // Skip anything with no albedo to give us - those render flat.
                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
                    {
                        ThrallsPlugin.Log.LogInfo(string.Format(
                            "Skipping {0}/{1} (shader {2}, no albedo)",
                            name.Trim(), material.name, material.shader.name));
                        continue;
                    }

                    _stoneUv = StoneUvRegion(renderer);
                    _stoneUvKnown = true;

                    ThrallsPlugin.Log.LogInfo(string.Format(
                        "Bindstone skinned with {0} from {1} (shader {2}), atlas patch {3}",
                        material.name, name.Trim(), material.shader.name, _stoneUv));

                    _stone = material;
                    return _stone;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no textured stone material to borrow.");
            return null;
        }

        private static Material _wood;
        private static Rect _woodUv = new Rect(0f, 0f, 1f, 1f);
        private static bool _woodUvKnown;

        /// <summary>
        /// The same trick as the stone, off a wooden piece instead.
        ///
        /// Our own timber sheet is both darker than the game's wood and, because every
        /// custom skin has its normals flattened, carries none of the grain highlights that
        /// make a vanilla board read as a board. Side by side with a workbench the poles
        /// came out nearly black. Borrowing the wood outright fixes both at once, the same
        /// way borrowing the stone did.
        /// </summary>
        private static Material BorrowWoodMaterial()
        {
            if (_wood != null) return _wood;

            foreach (var name in ThrallConfig.AltarWoodMaterialFrom.Value.Split(','))
            {
                var donor = ZNetScene.instance.GetPrefab(name.Trim());
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
                        continue;

                    _woodUv = StoneUvRegion(renderer);
                    _woodUvKnown = true;

                    ThrallsPlugin.Log.LogInfo(string.Format(
                        "Bindstone wood borrowed from {0}/{1} (shader {2}), atlas patch {3}",
                        name.Trim(), material.name, material.shader.name, _woodUv));

                    _wood = material;
                    return _wood;
                }
            }

            ThrallsPlugin.Log.LogWarning("Found no textured wood material to borrow.");
            return null;
        }

    }
}
