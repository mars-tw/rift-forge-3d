using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace RiftForge.Tests.PlayMode
{
    /// <summary>
    /// Offscreen evidence capture. Explicit: runs only when selected with
    /// -testFilter RiftForge.Tests.PlayMode.RiftVisualCaptureTests in a batchmode Editor
    /// started without -nographics. It never opens a window. Output folder comes from the
    /// RIFT_CAPTURE_DIR environment variable.
    /// </summary>
    [Explicit("Offscreen evidence capture; run explicitly in batchmode with graphics.")]
    [Category("VisualQA")]
    public sealed class RiftVisualCaptureTests
    {
        private static readonly Vector2Int[] Viewports =
        {
            new Vector2Int(1280, 720),   // desktop / laptop 16:9
            new Vector2Int(2400, 1080),  // modern phone 20:9 landscape
            new Vector2Int(1024, 768),   // tablet 4:3
        };

        [UnityTest]
        public IEnumerator CaptureRunStates()
        {
            string dir = Environment.GetEnvironmentVariable("RIFT_CAPTURE_DIR");
            Assume.That(string.IsNullOrEmpty(dir), Is.False, "Set RIFT_CAPTURE_DIR to capture.");
            Directory.CreateDirectory(dir);
            Assume.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Offscreen capture needs a graphics device (omit -nographics).");

            GameObject root = new GameObject("Capture Rift Game");
            RiftGame game = root.AddComponent<RiftGame>();
            try
            {
                yield return Settle(1.2f);
                yield return CaptureAll(dir, "01-title");

                game.StartNewRun();
                yield return Settle(2.2f);
                yield return CaptureAll(dir, "02-combat-wave1");

                game.SetPaused(true);
                yield return Settle(0.2f);
                yield return CaptureAll(dir, "03-pause");
                game.SetPaused(false);

                KillAll(root);
                yield return WaitForPhase(game, RunPhase.Forge, 6f);
                yield return Settle(0.4f);
                yield return CaptureAll(dir, "04-forge");

                for (int wave = 2; wave <= RiftRunState.FinalWave; wave++)
                {
                    game.ContinueFromForge();
                    if (wave == RiftRunState.FinalWave) break;
                    if (wave == 2)
                    {
                        yield return Settle(2.4f);
                        yield return CaptureAll(dir, "05-combat-wave2");
                    }
                    else yield return null;
                    KillAll(root);
                    yield return WaitForPhase(game, RunPhase.Forge, 6f);
                }
                yield return Settle(2.6f);
                yield return CaptureAll(dir, "06-combat-boss");

                KillAll(root);
                yield return WaitForPhase(game, RunPhase.Victory, 6f);
                yield return Settle(0.8f);
                yield return CaptureAll(dir, "07-victory");

                game.StartNewRun();
                yield return Settle(0.5f);
                game.TryDamagePlayer(999);
                yield return Settle(0.8f);
                yield return CaptureAll(dir, "08-defeat");
            }
            finally
            {
                Time.timeScale = 1f;
                UnityEngine.Object.Destroy(root);
                foreach (RiftHud hud in UnityEngine.Object.FindObjectsByType<RiftHud>())
                    UnityEngine.Object.Destroy(hud.gameObject);
            }
        }

        /// <summary>
        /// Editor batchmode performance proxy: wave-3 combat (5 enemies) with auto strikes and extra
        /// particle bursts. Three runs per quality level. Frame time is unscaled CPU frame time with
        /// vsync/target frame rate disabled; render time is one URP offscreen 2400x1080 render plus a
        /// 1-pixel readback (forces GPU completion), sampled every 5th frame. Not a device measurement.
        /// </summary>
        [UnityTest]
        public IEnumerator MeasureCombatFrameTimes()
        {
            string dir = Environment.GetEnvironmentVariable("RIFT_CAPTURE_DIR");
            Assume.That(string.IsNullOrEmpty(dir), Is.False, "Set RIFT_CAPTURE_DIR.");
            Assume.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Directory.CreateDirectory(dir);
            int previousQuality = QualitySettings.GetQualityLevel();
            int previousFps = Application.targetFrameRate;
            int previousVsync = QualitySettings.vSyncCount;
            var json = new StringBuilder();
            json.Append("{\n  \"device\": \"").Append(SystemInfo.graphicsDeviceName).Append("\",\n  \"cpu\": \"")
                .Append(SystemInfo.processorType).Append("\",\n  \"runs\": [\n");
            bool first = true;
            GameObject root = new GameObject("Perf Rift Game");
            RiftGame game = root.AddComponent<RiftGame>();
            RenderTexture target = new RenderTexture(2400, 1080, 24, RenderTextureFormat.ARGB32);
            target.Create();
            Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                string[] names = QualitySettings.names;
                for (int quality = 0; quality < names.Length; quality++)
                {
                    QualitySettings.SetQualityLevel(quality, true);
                    for (int runIndex = 0; runIndex < 3; runIndex++)
                    {
                        QualitySettings.vSyncCount = 0;
                        Application.targetFrameRate = -1;
                        game.StartNewRun();
                        for (int wave = 1; wave < 3; wave++)
                        {
                            yield return null;
                            KillAll(root);
                            yield return WaitForPhase(game, RunPhase.Forge, 6f);
                            game.ContinueFromForge();
                        }
                        Application.targetFrameRate = -1;
                        yield return Settle(1.5f);
                        var frames = new List<double>();
                        var renders = new List<double>();
                        Camera camera = Camera.main;
                        for (int frame = 0; frame < 400 && game.Run.Phase == RunPhase.Combat; frame++)
                        {
                            if (game.AttackCooldownRemaining <= 0f) game.Attack();
                            if (frame % 12 == 0) game.Fx.Sparks(game.PlayerPosition + Vector3.up, Palette.Gold, 40, 8f);
                            if (frame % 5 == 0)
                            {
                                Stopwatch watch = Stopwatch.StartNew();
                                RenderTexture previous = camera.targetTexture;
                                camera.targetTexture = target;
                                var request = new RenderPipeline.StandardRequest { destination = target };
                                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                                else camera.Render();
                                camera.targetTexture = previous;
                                RenderTexture active = RenderTexture.active;
                                RenderTexture.active = target;
                                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                                RenderTexture.active = active;
                                renders.Add(watch.Elapsed.TotalMilliseconds);
                            }
                            yield return null;
                            frames.Add(Time.unscaledDeltaTime * 1000.0);
                        }
                        if (!first) json.Append(",\n");
                        first = false;
                        json.Append("    {\"quality\": \"").Append(names[quality]).Append("\", \"run\": ").Append(runIndex + 1)
                            .Append(", \"frames\": ").Append(frames.Count)
                            .Append(", \"frameP50Ms\": ").Append(Percentile(frames, 0.5).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                            .Append(", \"frameP95Ms\": ").Append(Percentile(frames, 0.95).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                            .Append(", \"render2400x1080P50Ms\": ").Append(Percentile(renders, 0.5).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                            .Append(", \"render2400x1080P95Ms\": ").Append(Percentile(renders, 0.95).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                            .Append(", \"endPhase\": \"").Append(game.Run.Phase).Append("\"}");
                    }
                }
                json.Append("\n  ]\n}\n");
                File.WriteAllText(Path.Combine(dir, "perf-r4.json"), json.ToString());
            }
            finally
            {
                QualitySettings.SetQualityLevel(previousQuality, true);
                Application.targetFrameRate = previousFps;
                QualitySettings.vSyncCount = previousVsync;
                Time.timeScale = 1f;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pixel);
                UnityEngine.Object.Destroy(root);
                foreach (RiftHud hud in UnityEngine.Object.FindObjectsByType<RiftHud>())
                    UnityEngine.Object.Destroy(hud.gameObject);
            }
        }

        private static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0) return -1;
            var sorted = new List<double>(values);
            sorted.Sort();
            int index = Mathf.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        private static IEnumerator Settle(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static void KillAll(GameObject root)
        {
            foreach (RiftEnemy enemy in UnityEngine.Object.FindObjectsByType<RiftEnemy>())
                if (enemy != null && enemy.Health > 0 && enemy.transform.IsChildOf(root.transform))
                    enemy.TakeHit(9999);
        }

        private static IEnumerator WaitForPhase(RiftGame game, RunPhase phase, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (game.Run.Phase != phase && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(phase));
        }

        private static IEnumerator CaptureAll(string dir, string label)
        {
            foreach (Vector2Int size in Viewports)
            {
                Capture(Path.Combine(dir, $"{label}-{size.x}x{size.y}.png"), size.x, size.y);
                yield return null;
            }
        }

        /// <summary>Render the main camera and the HUD (temporarily in camera space) into a PNG.</summary>
        public static void Capture(string path, int width, int height)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
            var modes = new RenderMode[canvases.Length];
            var cams = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    modes[i] = canvases[i].renderMode;
                    cams[i] = canvases[i].worldCamera;
                    distances[i] = canvases[i].planeDistance;
                    if (!canvases[i].isRootCanvas) continue;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = Mathf.Max(camera.nearClipPlane + 0.05f, 0.4f);
                }
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = target;
                GL.Clear(true, true, Color.black);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null || !canvases[i].isRootCanvas) continue;
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cams[i];
                    canvases[i].planeDistance = distances[i];
                }
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }
    }
}
