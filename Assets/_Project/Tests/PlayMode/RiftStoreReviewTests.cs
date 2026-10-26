using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RiftForge.Tests.PlayMode
{
    public sealed class RiftStoreReviewTests
    {
        private GameObject root;
        private RiftGame game;
        private MemoryCheckpointStore checkpoints;
        private string progressJson;
        [UnitySetUp] public IEnumerator Setup()
        {
            checkpoints = new MemoryCheckpointStore(); RiftGame.CheckpointStoreOverride = checkpoints;
            RiftGame.ProgressOverride = new RiftProgress(save: s => progressJson = s);
            Create(); yield return null;
        }
        private void Create() { root = new GameObject("R6 Review Path"); game = root.AddComponent<RiftGame>(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            RiftGame.CheckpointStoreOverride = null; RiftGame.ProgressOverride = null; Time.timeScale = 1;
            if (game != null) Object.Destroy(game.Hud.gameObject); Object.Destroy(root); yield return null;
        }
        private void Click(string label) => game.Hud.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text == label).onClick.Invoke();
        private void Press<T>() where T : Component, IPointerDownHandler, IPointerUpHandler
        {
            var control = game.Hud.GetComponentInChildren<T>();
            var e = new PointerEventData(EventSystem.current) { pointerId = 31, button = PointerEventData.InputButton.Left };
            control.OnPointerDown(e); control.OnPointerUp(e);
        }

        [UnityTest] public IEnumerator ColdLaunchLearnSignatureCompletePauseReturnAndReopen()
        {
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Title)); Click("初鍛引導");
            float deadline = Time.realtimeSinceStartup + 175;
            while (game.Initiation != InitiationStep.Complete && Time.realtimeSinceStartup < deadline)
            {
                var move = game.Hud.MoveSource;
                if (!move.ActivePointerId.HasValue) move.TryBeginPointer(9);
                var enemy = game.Enemies.FirstOrDefault(e => e != null && !e.IsEmerging);
                if (enemy == null) { yield return null; continue; }
                Vector3 toward = enemy.transform.position - game.PlayerPosition; toward.y = 0;
                bool counter = game.Initiation == InitiationStep.Counter;
                Vector3 direction = toward.magnitude > (counter ? 1.3f : 1f) ? toward.normalized : Vector3.zero;
                move.TryMovePointer(9, new Vector2(direction.x, direction.z));
                if (counter && game.Rhythm.CounterReady && toward.magnitude < 2.8f && game.AttackCooldownRemaining <= 0 && game.DodgeCooldownRemaining < 1.7f)
                    Press<HoldStrikeButton>();
                else if (game.Initiation == InitiationStep.Burst && game.Rhythm.BurstReady && game.AttackCooldownRemaining <= 0 && game.DodgeCooldownRemaining < 1.7f)
                    Press<HoldStrikeButton>();
                else if ((!counter || !game.Rhythm.CounterReady) && game.DodgeCooldownRemaining <= 0 && game.Enemies.Any(e => e.ThreatContains(game.PlayerPosition) && e.SecondsUntilStrike >= 0 && e.SecondsUntilStrike <= .28f))
                    Press<DodgeTouchButton>();
                yield return null;
            }
            Assert.That(game.Initiation, Is.EqualTo(InitiationStep.Complete), "Touch-only lesson must finish inside three minutes.");
            Assert.That(game.Imprints.Count, Is.Zero, "Burst must consume spatial imprints.");
            Assert.That(game.Progress.initiationComplete, Is.True);
            Click("暫停"); Click("返回標題");
            Object.Destroy(game.Hud.gameObject); Object.Destroy(root); yield return null;
            RiftGame.ProgressOverride = new RiftProgress(progressJson); Create(); yield return null;
            Assert.That(game.Progress.initiationComplete, Is.True); Assert.That(game.Progress.Seals, Is.Zero);
            Assert.That(game.Hud.GetComponentsInChildren<Text>().Any(t => t.text == "重練初鍛"), Is.True);
        }

        [UnityTest] public IEnumerator ForgePurchaseSurvivesRecreationAndBackgroundReleasesControls()
        {
            game.StartNewRun(); yield return null;
            foreach (var enemy in new List<RiftEnemy>(game.Enemies)) enemy.TakeHit(9999);
            float until = Time.realtimeSinceStartup + 8;
            while (game.Run.Phase != RunPhase.Forge && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge)); game.Purchase(ForgeUpgrade.Blade);
            int shards = game.Run.Material;
            Object.Destroy(game.Hud.gameObject); Object.Destroy(root); yield return null; Create(); yield return null;
            Click("波間續玩"); Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge));
            Assert.That(game.Run.BladeLevel, Is.EqualTo(1)); Assert.That(game.Run.Material, Is.EqualTo(shards));
            Click("開啟下一波"); game.Hud.MoveSource.TryBeginPointer(4);
            game.SendMessage("OnApplicationPause", true); Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Hud.MoveSource.ActivePointerId, Is.Null);
            game.SendMessage("OnApplicationPause", false); Assert.That(game.IsPaused, Is.True);
            Click("繼續戰鬥"); Assert.That(game.CanSimulate, Is.True);
        }

        [UnityTest] public IEnumerator LicensedCharactersHaveSkinBonesFacesAndChangingSkeletalPoses()
        {
            game.StartNewRun(); yield return null;
            foreach (var character in game.GetComponentsInChildren<RiftCharacter>())
            {
                var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(skin, Is.Not.Null); Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(500));
                Assert.That(skin.bones.Length, Is.GreaterThan(15)); Assert.That(character.ClipCount, Is.GreaterThan(5));
                Assert.That(skin.bones.Any(b => b.name.ToLowerInvariant().Contains("head")), Is.True);
                var colors = skin.sharedMaterials.Select(m => m.GetColor("_BaseColor")).ToArray();
                if (character.name.StartsWith("Sentinel")) Assert.That(colors.Any(c => c.g > c.r * 1.2f && c.g > c.b), Is.True, "Sentinel must retain its copper-green material.");
                if (character.name.StartsWith("Lancer")) Assert.That(colors.Any(c => c.r > c.g * 1.4f && c.r > c.b), Is.True, "Lancer must retain its red material.");
                var before = skin.bones.Select(b => b.localRotation).ToArray(); character.SampleAt("Run", .3f);
                Assert.That(skin.bones.Where((b, i) => Quaternion.Angle(b.localRotation, before[i]) > 1f).Count(), Is.GreaterThan(3));
            }
        }

        [UnityTest] public IEnumerator BurstDetonatesDistantImprintOnceBeyondNormalReach()
        {
            game.StartNewRun(); yield return new WaitForSeconds(2.2f);
            var target = game.Enemies[0];
            target.transform.position = new Vector3(7, 0, 0); int before = target.Health;
            game.Imprints.Add(target.transform.position);
            for (int i=0;i<3;i++) game.Rhythm.RegisterPerfectDodge();
            Assert.That(Vector3.Distance(target.transform.position, game.PlayerPosition), Is.GreaterThan(CombatRhythm.Reach(StrikeKind.Burst)+1));
            game.Attack(); yield return new WaitForSeconds(.3f);
            Assert.That(target.Health, Is.EqualTo(before-game.Run.AttackDamage)); Assert.That(game.Imprints.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator StoreViewportControlsAndTextAreReachable()
        {
            yield return CheckSizes("title"); game.StartInitiation(); yield return CheckSizes("lesson");
            game.SetPaused(true); yield return CheckSizes("pause"); game.SetPaused(false); game.ReturnToTitle(); game.StartNewRun();
            foreach (var e in new List<RiftEnemy>(game.Enemies)) e.TakeHit(9999);
            float until = Time.realtimeSinceStartup + 8;
            while (game.Run.Phase != RunPhase.Forge && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge)); yield return CheckSizes("forge");
            game.ContinueFromForge(); Assert.That(game.TryDamagePlayer(9999), Is.True); yield return null;
            yield return CheckSizes("defeat"); game.StartNewRun();
            for (int wave = 1; wave <= RiftRunState.FinalWave; wave++)
            {
                foreach (var e in new List<RiftEnemy>(game.Enemies)) e.TakeHit(9999);
                until = Time.realtimeSinceStartup + 8;
                var expected = wave == RiftRunState.FinalWave ? RunPhase.Victory : RunPhase.Forge;
                while (game.Run.Phase != expected && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(game.Run.Phase, Is.EqualTo(expected));
                if (wave < RiftRunState.FinalWave) game.ContinueFromForge();
            }
            yield return CheckSizes("victory");
        }
        private IEnumerator CheckSizes(string phase)
        {
            var canvas = game.Hud.Canvas; var rect = (RectTransform)canvas.transform;
            var cameraObject = new GameObject("Review UI Camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -100); camera.farClipPlane = 200;
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; rect.position = Vector3.zero; rect.rotation = Quaternion.identity; rect.localScale = Vector3.one;
            foreach (var size in new[] { new Vector2Int(375,667),new Vector2Int(667,375),new Vector2Int(440,956),new Vector2Int(1024,1366),new Vector2Int(1366,1024),new Vector2Int(1290,2796),new Vector2Int(2064,2752),new Vector2Int(2752,2064) })
            foreach (bool notch in new[] { false, true })
            {
                rect.sizeDelta = size; camera.aspect = (float)size.x / size.y; camera.orthographicSize = size.y / 2f;
                var safe = game.Hud.SafeRoot; safe.anchorMin = notch ? new Vector2(16f/size.x, 12f/size.y) : Vector2.zero;
                safe.anchorMax = notch ? new Vector2(1-16f/size.x,1-24f/size.y) : Vector2.one;
                Canvas.ForceUpdateCanvases(); game.Hud.ApplyMobileLayout(); Canvas.ForceUpdateCanvases(); yield return null;
                var controls = canvas.GetComponentsInChildren<Button>().Select(b => (RectTransform)b.transform).Concat(canvas.GetComponentsInChildren<TouchMoveArea>().Select(b => (RectTransform)b.transform));
                var boxes = new List<Rect>(); Rect boundary = Bounds(rect, safe);
                foreach (var target in controls)
                {
                    Rect box = Bounds(rect, target); boxes.Add(box);
                    Assert.That(Mathf.Min(box.width,box.height), Is.GreaterThanOrEqualTo(44), $"{phase} {size} {target.name}");
                    Assert.That(box.xMin,Is.GreaterThanOrEqualTo(boundary.xMin-.1f)); Assert.That(box.xMax,Is.LessThanOrEqualTo(boundary.xMax+.1f));
                    Assert.That(box.yMin,Is.GreaterThanOrEqualTo(boundary.yMin-.1f)); Assert.That(box.yMax,Is.LessThanOrEqualTo(boundary.yMax+.1f));
                    var pointer = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(target.TransformPoint(target.rect.center)) };
                    var hits = new List<RaycastResult>(); canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    Assert.That(hits.Count, Is.GreaterThan(0)); Assert.That(hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target), Is.True);
                }
                for(int i=0;i<boxes.Count;i++) for(int j=i+1;j<boxes.Count;j++) Assert.That(boxes[i].Overlaps(boxes[j]), Is.False, $"{phase} {size} overlap");
                foreach(var text in canvas.GetComponentsInChildren<Text>())
                {
                    if(string.IsNullOrEmpty(text.text) || text.transform.IsChildOf(canvas.transform.Find("Safe Area/Floating Text"))) continue;
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height+2), $"{phase} {size} {text.text}");
                    foreach(char c in text.text) if(!char.IsWhiteSpace(c)) Assert.That(text.font.HasCharacter(c), Is.True, $"Missing {c}");
                }
            }
            game.Hud.SafeRoot.anchorMin = Vector2.zero; game.Hud.SafeRoot.anchorMax = Vector2.one; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            Object.Destroy(cameraObject); yield return null;
        }
        private static Rect Bounds(RectTransform root, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners); Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach(var corner in corners) { Vector2 p=root.InverseTransformPoint(corner); min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
