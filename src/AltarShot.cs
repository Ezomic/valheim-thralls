using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Photographs the altar from inside the game and writes the picture to disk.
    ///
    /// This exists because the altar has been built and rebuilt against Blender previews,
    /// which are a different renderer entirely - so nobody working on the model has ever
    /// actually seen what Valheim's own shader does with it, and every fix has been a
    /// guess checked by asking someone else to look. A camera pointed at the piece,
    /// rendered through the game's own pipeline and saved as a PNG, ends that.
    /// </summary>
    internal static class AltarShot
    {
        private const int Width = 900;
        private const int Height = 700;

        private static int _index;

        public static string Capture(string label) { return Capture(label, true); }

        public static string Capture(string label, bool allAngles)
        {
            var altar = ThrallAltar.Current;
            if (altar == null)
            {
                ThrallsPlugin.Log.LogWarning("No bindstone nearby to photograph.");
                return null;
            }

            var main = Utils.GetMainCamera();
            if (main == null)
            {
                ThrallsPlugin.Log.LogWarning("No main camera to copy settings from.");
                return null;
            }

            RenderTexture target = null;
            GameObject rig = null;
            var previous = RenderTexture.active;

            try
            {
                // Cloned from the game's camera object rather than built from scratch.
                //
                // This used to be a bare GameObject with a Camera on it and a
                // CopyFrom(main), and the comment claimed that carried the post
                // processing. It does not: CopyFrom copies the Camera component's own
                // fields - clear flags, culling mask, projection - and nothing else, and
                // Valheim's grading, bloom and tonemapping live in separate components
                // hanging off the same object. So every shot this took came out lighter
                // and flatter than the game, and three rounds of a darkness bug got spent
                // arguing with evidence that was wrong. Instantiating the object brings
                // those components along.
                rig = UnityEngine.Object.Instantiate(main.gameObject);
                rig.name = "thralls_shot";

                // An audio listener would fight the player's own.
                var ears = rig.GetComponentsInChildren<AudioListener>(true);
                for (int i = 0; i < ears.Length; i++) UnityEngine.Object.Destroy(ears[i]);

                // Anything that would drive this camera back to following the player.
                //
                // Cloning the main camera steals GameCamera.instance and then throws it
                // away. Its Awake is an unguarded "m_instance = this", so the copy takes
                // the singleton the moment Instantiate runs; its OnDestroy is
                // "if (m_instance == this) m_instance = null", so tearing the copy down
                // leaves the static null - and the real camera's Awake ran at load and
                // never runs again to put it back.
                //
                // The game then has no GameCamera.instance at all. Everything that
                // raycasts from the camera to work out what you are pointing at stops
                // working, which shows up as tools that no longer do anything: you keep
                // the picture and lose the game.
                //
                // So: remember the real one, destroy the copies immediately rather than
                // at end of frame, and put the static back.
                var realCamera = GameCamera.instance;

                var drivers = rig.GetComponentsInChildren<GameCamera>(true);
                for (int i = 0; i < drivers.Length; i++)
                    UnityEngine.Object.DestroyImmediate(drivers[i]);

                if (realCamera != null && GameCamera.instance != realCamera)
                {
                    AccessTools.Field(typeof(GameCamera), "m_instance")
                               .SetValue(null, realCamera);
                    ThrallsPlugin.Log.LogInfo("Restored GameCamera.instance after cloning the camera.");
                }

                var cam = rig.GetComponent<Camera>();
                if (cam == null) cam = rig.AddComponent<Camera>();

                cam.enabled = false;
                cam.targetTexture = target = new RenderTexture(Width, Height, 24);

                cam.fieldOfView = 50f;
                cam.nearClipPlane = 0.05f;

                var centre = altar.transform.position + Vector3.up * 1.1f;
                var dir = Path.GetDirectoryName(typeof(AltarShot).Assembly.Location);
                var safe = string.IsNullOrEmpty(label) ? "shot" : Sanitise(label);
                var group = _index++;
                string first = null;

                // Four sides. Which way a placed piece faces is the player's choice, so
                // one fixed angle is a coin flip on whether the front is even visible -
                // the first attempt came out looking at the back of the rune board.
                // One angle is enough when the point is comparing a series of shots to
                // each other; four is for finding out which way the piece is facing.
                var yaws = allAngles ? new[] { 0f, 90f, 180f, 270f } : new[] { 180f };

                for (int step = 0; step < yaws.Length; step++)
                {
                    var yaw = yaws[step];
                    var offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.7f, -3.3f);

                    cam.transform.position = centre + offset;
                    cam.transform.rotation = Quaternion.LookRotation(-offset.normalized);

                    cam.Render();

                    RenderTexture.active = target;
                    var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    shot.Apply();

                    var bytes = EncodePng(shot);
                    UnityEngine.Object.Destroy(shot);

                    if (bytes == null || bytes.Length == 0) continue;

                    var path = Path.Combine(dir,
                        string.Format("altar_{0}_{1}_{2}.png", group, safe, (int)yaw));

                    File.WriteAllBytes(path, bytes);
                    ThrallsPlugin.Log.LogInfo("ALTAR SHOT " + path);

                    if (first == null) first = path;
                }

                return first;
            }
            catch (Exception e)
            {
                ThrallsPlugin.Log.LogError("Bindstone shot failed: " + e);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rig != null) UnityEngine.Object.Destroy(rig);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
            }
        }

        private static string Sanitise(string text)
        {
            var clean = text.ToLowerInvariant()
                .Replace(' ', '_').Replace(',', '_').Replace('/', '_');

            foreach (var bad in Path.GetInvalidFileNameChars())
                clean = clean.Replace(bad, '_');

            return clean;
        }

        private static System.Reflection.MethodInfo _encode;

        /// <summary>
        /// ImageConversion.EncodeToPNG by reflection, for the same reason LoadImage is:
        /// its assembly targets a newer netstandard than this one and cannot be referenced.
        /// </summary>
        private static byte[] EncodePng(Texture2D texture)
        {
            if (_encode == null)
            {
                var type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                if (type == null) return null;

                _encode = AccessTools.Method(type, "EncodeToPNG", new[] { typeof(Texture2D) });
            }

            return _encode == null ? null : _encode.Invoke(null, new object[] { texture }) as byte[];
        }
    }
}
