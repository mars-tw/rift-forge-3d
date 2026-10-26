using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RiftForge.Tests.PlayMode
{
    public sealed class RiftR4PlayModeTests
    {
        private GameObject root;
        private RiftGame game;
        private MemoryRecordStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryRecordStore();
            RiftGame.RecordStoreOverride = store;
            root = new GameObject("R4 Test Game");
            game = root.AddComponent<RiftGame>();
        }

        [TearDown]
        public void TearDown()
        {
            RiftGame.RecordStoreOverride = null;
            Time.timeScale = 1f;
            if (game != null && game.Hud != null) Object.Destroy(game.Hud.gameObject);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator DamageLandsOnTheImpactFrameAndWhiffsDealNothing()
        {
            game.StartNewRun();
            yield return WaitUntilAllEmerged(3f);
            RiftEnemy target = game.Enemies[0];
            HoldEnemiesAway(except: null);
            int before = target.Health;

            game.Attack(); // Nothing in reach: a full whiff.
            Assert.That(game.IsStrikePending, Is.True, "Anticipation frames must precede the impact.");
            yield return WaitSeconds(0.2f);
            Assert.That(game.IsStrikePending, Is.False);
            Assert.That(target.Health, Is.EqualTo(before), "A whiff must not deal damage.");
            Assert.That(game.Rhythm.HitStreak, Is.Zero);

            yield return WaitSeconds(0.5f);
            PlaceInFront(target, 1.4f);
            game.Attack();
            Assert.That(target.Health, Is.EqualTo(before), "Damage must wait for the impact frame.");
            yield return WaitSeconds(0.2f);
            Assert.That(target.Health, Is.LessThan(before));
            Assert.That(game.Rhythm.HitStreak, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DodgingDuringAnticipationCancelsTheStrike()
        {
            game.StartNewRun();
            yield return WaitUntilAllEmerged(3f);
            RiftEnemy target = game.Enemies[0];
            HoldEnemiesAway(except: target);
            PlaceInFront(target, 1.4f);
            int before = target.Health;
            game.Attack();
            game.Dodge();
            Assert.That(game.IsStrikePending, Is.False);
            yield return WaitSeconds(0.2f);
            Assert.That(target.Health, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator LateDodgeInsideATelegraphIsAPerfectCounter()
        {
            game.StartNewRun();
            yield return WaitUntilAllEmerged(3f);
            RiftEnemy stalker = game.Enemies[0];
            HoldEnemiesAway(except: stalker);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!(stalker.IsTelegraphing && stalker.SecondsUntilStrike <= 0.3f) && Time.realtimeSinceStartup < deadline)
            {
                if (!stalker.IsTelegraphing) PlaceInFront(stalker, 1.2f);
                HoldEnemiesAway(except: stalker);
                yield return null;
            }
            Assert.That(stalker.IsTelegraphing, Is.True, "The stalker never committed to a strike.");
            Assert.That(stalker.ThreatContains(game.PlayerPosition), Is.True);
            int health = game.Run.Health;
            game.Dodge();
            Assert.That(game.Rhythm.PerfectDodges, Is.EqualTo(1));
            Assert.That(game.Rhythm.CounterReady, Is.True);
            yield return WaitSeconds(0.8f);
            Assert.That(game.Run.Health, Is.EqualTo(health), "A perfect dodge must avoid the strike.");
        }

        [UnityTest]
        public IEnumerator ForgeBurstHitsEnemiesOnBothSidesOfThePlayer()
        {
            game.StartNewRun();
            yield return WaitUntilAllEmerged(3f);
            RiftEnemy front = game.Enemies[0];
            RiftEnemy back = game.Enemies[1];
            HoldEnemiesAway(except: front);
            back.transform.position = game.PlayerPosition + new Vector3(0f, -game.PlayerPosition.y, -2.6f);
            front.transform.position = game.PlayerPosition + new Vector3(0f, -game.PlayerPosition.y, 2.4f);
            for (int i = 0; i < 3; i++) game.Rhythm.RegisterPerfectDodge();
            Assert.That(game.Rhythm.BurstReady, Is.True);
            int frontBefore = front.Health, backBefore = back.Health;
            game.Attack();
            yield return WaitSeconds(0.3f);
            Assert.That(front.Health, Is.LessThan(frontBefore));
            Assert.That(back.Health, Is.LessThan(backBefore), "A Forge Burst must hit all around the player.");
            Assert.That(game.Rhythm.BurstReady, Is.False);
        }

        [UnityTest]
        public IEnumerator WaveTwoIntroducesALineChargingLancer()
        {
            game.StartNewRun();
            yield return null;
            KillAll();
            yield return WaitForPhase(RunPhase.Forge, 6f);
            game.ContinueFromForge();
            int lancers = 0;
            foreach (RiftEnemy enemy in game.Enemies) if (enemy.Kind == EnemyKind.Lancer) lancers++;
            Assert.That(game.ActiveEnemyCount, Is.EqualTo(4));
            Assert.That(lancers, Is.EqualTo(1));
            StringAssert.Contains("RIFT 2", game.Hud.BannerText);
        }

        [UnityTest]
        public IEnumerator TheWardenEnragesAtHalfHealth()
        {
            yield return ReachBossWave();
            RiftEnemy boss = game.Boss;
            Assert.That(boss, Is.Not.Null);
            boss.TakeHit(boss.MaxHealth / 2 + 1);
            Assert.That(boss.Enraged, Is.True);
            StringAssert.Contains("ENRAGES", game.Hud.BannerText);
            boss.TakeHit(9999);
            yield return WaitForPhase(RunPhase.Victory, 6f);
        }

        [UnityTest]
        public IEnumerator VictoryProducesAGradedScoreAndAPersonalBest()
        {
            yield return ReachBossWave();
            KillAll();
            yield return WaitForPhase(RunPhase.Victory, 6f);
            Assert.That(game.LastScore.HasValue, Is.True);
            RunScore score = game.LastScore.Value;
            Assert.That(score.Victory, Is.True);
            Assert.That(score.Kills, Is.EqualTo(13));
            Assert.That(RunScore.GradeRank(score.Grade), Is.GreaterThan(0));
            Assert.That(game.LastScoreIsBest, Is.True);
            Assert.That(store.BestPoints, Is.EqualTo(score.Points));

            game.StartNewRun();
            Assert.That(game.LastScore.HasValue, Is.False);
            game.TryDamagePlayer(999);
            yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.That(game.LastScoreIsBest, Is.False, "A wave-1 defeat must not replace a victory record.");
        }

        [UnityTest]
        public IEnumerator FirstRunsTeachTheComboThenTheHintClears()
        {
            game.StartNewRun();
            yield return null;
            StringAssert.Contains("FINISHER", game.CurrentHint);
            game.Attack();
            yield return WaitSeconds(0.5f);
            game.Attack();
            yield return WaitSeconds(0.5f);
            game.Attack(); // Third strike in the chain is the Finisher.
            Assert.That(game.CurrentHint, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ParticleFxAndSynthAudioAreReady()
        {
            yield return null;
            Assert.That(game.Fx, Is.Not.Null);
            Assert.That(game.Fx.HasParticles, Is.True, "Additive particle material must load from Resources.");
            Assert.That(game.Audio.ClipCount, Is.EqualTo(System.Enum.GetValues(typeof(RiftSfx)).Length));
        }

        /// <summary>
        /// Control reachability gate: on three viewports and every phase, each button and the move pad
        /// sits inside the safe canvas, does not overlap another control, and is at least 88 canvas
        /// units (about 44 pt at the 1080p reference) on its short side.
        /// </summary>
        [UnityTest]
        public IEnumerator ControlsAreReachableOnEveryViewportAndPhase()
        {
            Vector2Int[] viewports = { new Vector2Int(1280, 720), new Vector2Int(2400, 1080), new Vector2Int(1024, 768), new Vector2Int(1366, 600) };
            yield return null;
            CheckAll(viewports, "title");
            game.StartNewRun();
            yield return null;
            CheckAll(viewports, "combat");
            game.SetPaused(true);
            yield return null;
            CheckAll(viewports, "pause");
            game.SetPaused(false);
            KillAll();
            yield return WaitForPhase(RunPhase.Forge, 6f);
            game.Hud.Refresh();
            CheckAll(viewports, "forge");
            game.ContinueFromForge();
            game.TryDamagePlayer(999);
            yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Defeat));
            game.Hud.Refresh();
            CheckAll(viewports, "defeat");
        }

        // ------------------------------------------------------------ helpers

        private void CheckAll(Vector2Int[] viewports, string phase)
        {
            Canvas canvas = game.Hud.Canvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            RenderMode mode = canvas.renderMode;
            Vector2 originalSize = canvasRect.sizeDelta;
            Vector3 originalScale = canvasRect.localScale;
            try
            {
                foreach (Vector2Int viewport in viewports)
                {
                    float scale = Mathf.Exp(Mathf.Lerp(Mathf.Log(viewport.x / 1920f), Mathf.Log(viewport.y / 1080f), 0.6f));
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvasRect.localScale = Vector3.one;
                    canvasRect.sizeDelta = new Vector2(viewport.x / scale, viewport.y / scale);
                    Canvas.ForceUpdateCanvases();
                    CheckControls(canvasRect, $"{phase} @ {viewport.x}x{viewport.y}");
                }
            }
            finally
            {
                canvas.renderMode = mode;
                canvasRect.sizeDelta = originalSize;
                canvasRect.localScale = originalScale;
            }
        }

        private static void CheckControls(RectTransform canvasRect, string label)
        {
            var rects = new List<KeyValuePair<string, Rect>>();
            foreach (Button button in canvasRect.GetComponentsInChildren<Button>(false))
                if (button.isActiveAndEnabled) rects.Add(new KeyValuePair<string, Rect>(button.name, CanvasRect(canvasRect, (RectTransform)button.transform)));
            foreach (TouchMoveArea pad in canvasRect.GetComponentsInChildren<TouchMoveArea>(false))
                rects.Add(new KeyValuePair<string, Rect>("Move Pad", CanvasRect(canvasRect, (RectTransform)pad.transform)));
            Assert.That(rects.Count, Is.GreaterThan(0), label + ": no controls found.");
            Rect bounds = canvasRect.rect;
            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i].Value;
                Assert.That(r.xMin >= bounds.xMin - 0.5f && r.xMax <= bounds.xMax + 0.5f &&
                            r.yMin >= bounds.yMin - 0.5f && r.yMax <= bounds.yMax + 0.5f, Is.True,
                    $"{label}: {rects[i].Key} {r} leaves the canvas {bounds}.");
                Assert.That(Mathf.Min(r.width, r.height), Is.GreaterThanOrEqualTo(88f), $"{label}: {rects[i].Key} is too small.");
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.That(r.Overlaps(rects[j].Value), Is.False, $"{label}: {rects[i].Key} overlaps {rects[j].Key}.");
            }
        }

        private static Rect CanvasRect(RectTransform canvasRect, RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = canvasRect.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private IEnumerator ReachBossWave()
        {
            game.StartNewRun();
            for (int wave = 1; wave < RiftRunState.FinalWave; wave++)
            {
                yield return null;
                KillAll();
                yield return WaitForPhase(RunPhase.Forge, 6f);
                game.ContinueFromForge();
            }
            Assert.That(game.Run.IsBossWave, Is.True);
            yield return null;
        }

        private void KillAll()
        {
            foreach (RiftEnemy enemy in new List<RiftEnemy>(game.Enemies))
                if (enemy != null && enemy.Health > 0) enemy.TakeHit(9999);
        }

        private IEnumerator WaitUntilAllEmerged(float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool emerging = false;
                foreach (RiftEnemy enemy in game.Enemies) emerging |= enemy.IsEmerging;
                if (!emerging) yield break;
                yield return null;
            }
            Assert.Fail("Enemies did not finish emerging.");
        }

        private void HoldEnemiesAway(RiftEnemy except)
        {
            int i = 0;
            foreach (RiftEnemy enemy in game.Enemies)
            {
                if (enemy == except) continue;
                float angle = i++ * 1.3f;
                enemy.transform.position = new Vector3(Mathf.Cos(angle) * 8f, 0f, 6f + Mathf.Sin(angle));
            }
        }

        private void PlaceInFront(RiftEnemy enemy, float distance)
        {
            Vector3 forward = game.transform.Find("Arena/Forged Sentinel").forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 p = game.PlayerPosition + forward * distance;
            enemy.transform.position = new Vector3(p.x, 0f, p.z);
        }

        private IEnumerator WaitForPhase(RunPhase phase, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (game.Run.Phase != phase && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(phase));
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
