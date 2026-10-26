using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RiftForge.Tests.PlayMode
{
    [Explicit("Background offscreen evidence only")]
    public sealed class RiftMobileCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureMobileStates()
        {
            string dir = Environment.GetEnvironmentVariable("RIFT_CAPTURE_DIR");
            Assert.That(dir, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(dir);
            var root = new GameObject("Mobile Capture");
            var game = root.AddComponent<RiftGame>();
            yield return null;
            yield return Capture(dir, "01-title");
            game.StartNewRun();
            yield return new WaitForSeconds(0.5f);
            yield return Capture(dir, "02-combat");
            game.SetPaused(true);
            yield return Capture(dir, "03-pause");
            game.SetPaused(false);
            foreach (var enemy in new System.Collections.Generic.List<RiftEnemy>(game.Enemies)) enemy.TakeHit(9999);
            float deadline = Time.realtimeSinceStartup + 8;
            while (game.Run.Phase != RunPhase.Forge && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge));
            yield return Capture(dir, "04-forge");
            game.ContinueFromForge();
            game.TryDamagePlayer(9999);
            yield return null;
            yield return Capture(dir, "05-defeat");
            game.ReturnToTitle(); game.SelectRoute(1); game.StartSelectedRun();
            yield return new WaitForSeconds(1f);
            yield return Capture(dir, "06-echo-trial");
            for(int wave=1;wave<=3;wave++)
            {
                foreach(var enemy in new System.Collections.Generic.List<RiftEnemy>(game.Enemies)) enemy.TakeHit(9999);
                deadline=Time.realtimeSinceStartup+8;
                var expected=wave==3?RunPhase.Victory:RunPhase.Forge;
                while(game.Run.Phase!=expected && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(game.Run.Phase,Is.EqualTo(expected));
                if(wave<3) game.ContinueFromForge();
            }
            yield return null;
            yield return Capture(dir, "07-victory");
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(game.Hud.gameObject);
            yield return null;
        }

        private static IEnumerator Capture(string dir, string label)
        {
            foreach (var size in new[] { new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(1366,600) })
            {
                RiftVisualCaptureTests.Capture(Path.Combine(dir, $"{label}-{size.x}x{size.y}.png"), size.x, size.y);
                yield return null;
            }
        }
    }
}
