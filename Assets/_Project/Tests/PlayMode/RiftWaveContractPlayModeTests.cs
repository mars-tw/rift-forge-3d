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
    public sealed class RiftWaveContractPlayModeTests
    {
        private GameObject root;
        private RiftGame game;
        private MemoryCheckpointStore checkpoints;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            checkpoints = new MemoryCheckpointStore();
            RiftGame.CheckpointStoreOverride = checkpoints;
            RiftGame.ProgressOverride = new RiftProgress();
            Create();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RiftGame.CheckpointStoreOverride = null;
            RiftGame.ProgressOverride = null;
            Time.timeScale = 1f;
            if (game != null && game.Hud != null) Object.Destroy(game.Hud.gameObject);
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        private void Create()
        {
            root = new GameObject("R7 Wave Contract Tests");
            game = root.AddComponent<RiftGame>();
        }

        private IEnumerator ReachForge()
        {
            game.StartNewRun();
            yield return null;
            foreach (RiftEnemy enemy in new List<RiftEnemy>(game.Enemies)) enemy.TakeHit(9999);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (game.Run.Phase != RunPhase.Forge && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge));
        }

        private void Select(ForgeContract contract)
        {
            int guard = 0;
            while (game.Run.NextContract != contract && guard++ < 4) game.CycleContract();
            Assert.That(game.Run.NextContract, Is.EqualTo(contract));
        }

        private IEnumerator RecreateAndResume()
        {
            Object.Destroy(game.Hud.gameObject);
            Object.Destroy(root);
            yield return null;
            Create();
            yield return null;
            Button resume = game.Hud.GetComponentsInChildren<Button>(true)
                .First(b => b.GetComponentInChildren<Text>(true)?.text == "波間續玩");
            resume.onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForgeSelectionPersistsAndDoesNotRestrictExistingMultiBuy()
        {
            yield return ReachForge();
            Select(ForgeContract.Counter);
            Assert.That(game.Purchase(ForgeUpgrade.Blade), Is.True);
            Assert.That(game.Purchase(ForgeUpgrade.Dodge), Is.True, "R7 contracts must not narrow the existing forge economy.");
            int material = game.Run.Material;
            yield return RecreateAndResume();
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge));
            Assert.That(game.Run.NextContract, Is.EqualTo(ForgeContract.Counter));
            Assert.That(game.Run.BladeLevel, Is.EqualTo(1));
            Assert.That(game.Run.DodgeLevel, Is.EqualTo(1));
            Assert.That(game.Run.Material, Is.EqualTo(material));
            game.ContinueFromForge();
            Assert.That(game.Run.ActiveContract, Is.EqualTo(ForgeContract.Counter));
            Assert.That(game.Run.NextContract, Is.EqualTo(ForgeContract.None));
            Assert.That(game.Run.ContractProgress, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CounterAndFinisherProgressOnlyOnRealStrikeImpactFrames()
        {
            yield return ReachForge();
            Select(ForgeContract.Counter);
            game.ContinueFromForge();
            yield return new WaitForSeconds(2.2f);
            RiftEnemy target = game.Enemies.First(e => e != null);
            target.ConfigureInitiationPartner();
            target.transform.position = game.PlayerPosition + Vector3.forward * 1.2f;

            game.Attack();
            yield return new WaitForSeconds(.2f);
            Assert.That(game.Run.ContractProgress, Is.Zero, "A normal strike must not satisfy the counter contract.");
            yield return new WaitForSeconds(.35f);
            game.Rhythm.RegisterPerfectDodge();
            int before = target.Health;
            game.Attack();
            yield return new WaitForSeconds(.2f);
            Assert.That(target.Health, Is.LessThan(before));
            Assert.That(game.Run.ContractProgress, Is.EqualTo(1));

            foreach (RiftEnemy enemy in new List<RiftEnemy>(game.Enemies)) enemy.TakeHit(9999);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (game.Run.Phase != RunPhase.Forge && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Forge));
            Select(ForgeContract.Finisher);
            game.ContinueFromForge();
            yield return new WaitForSeconds(2.2f);
            target = game.Enemies.First(e => e != null);
            target.ConfigureInitiationPartner();
            target.transform.position = game.PlayerPosition + Vector3.forward * 1.2f;
            for (int strike = 0; strike < 6; strike++)
            {
                game.Attack();
                yield return new WaitForSeconds(.55f);
            }
            Assert.That(game.Run.ContractProgress, Is.EqualTo(2), "Two separate real Finisher impacts must satisfy the contract.");
        }

        [UnityTest]
        public IEnumerator ImprintContractCountsRemoteHealthLossAndRejectsEmptyGround()
        {
            yield return ReachForge();
            Select(ForgeContract.Imprint);
            game.ContinueFromForge();
            yield return new WaitForSeconds(2.2f);

            game.Imprints.Add(new Vector3(50f, 0f, 50f));
            for (int i = 0; i < 3; i++) game.Rhythm.RegisterPerfectDodge();
            game.Attack();
            yield return new WaitForSeconds(.25f);
            Assert.That(game.Run.ContractProgress, Is.Zero, "An empty imprint detonation must not count.");

            RiftEnemy[] targets = game.Enemies.Where(e => e != null).Take(2).ToArray();
            Assert.That(targets.Length, Is.EqualTo(2));
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].ConfigureInitiationPartner();
                targets[i].transform.position = new Vector3(6f + i * 3f, 0f, 1f);
                game.Imprints.Add(targets[i].transform.position);
            }
            int[] before = targets.Select(e => e.Health).ToArray();
            for (int i = 0; i < 3; i++) game.Rhythm.RegisterPerfectDodge();
            game.Attack();
            yield return new WaitForSeconds(.25f);
            Assert.That(targets.Select((e, i) => e.Health < before[i]).All(x => x), Is.True);
            Assert.That(game.Run.ContractProgress, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ContractControlAndExistingForgeControlsRemainReachableAtFourViewports()
        {
            yield return ReachForge();
            Select(ForgeContract.Counter);
            Assert.That(game.Hud.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("反擊命中 1 次")), Is.True);

            Canvas canvas = game.Hud.Canvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            GameObject cameraObject = new GameObject("R7 Contract UI Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -100); camera.farClipPlane = 200;
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            canvasRect.position = Vector3.zero; canvasRect.rotation = Quaternion.identity; canvasRect.localScale = Vector3.one;

            foreach (Vector2Int size in new[] { new Vector2Int(320,568), new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(1280,640) })
            {
                canvasRect.sizeDelta = size;
                camera.aspect = (float)size.x / size.y; camera.orthographicSize = size.y / 2f;
                game.Hud.SafeRoot.anchorMin = Vector2.zero; game.Hud.SafeRoot.anchorMax = Vector2.one;
                Canvas.ForceUpdateCanvases(); game.Hud.ApplyMobileLayout(); Canvas.ForceUpdateCanvases();
                yield return null;

                Button[] controls = game.Hud.GetComponentsInChildren<Button>()
                    .Where(b => b.name == "Wave Contract Button" || b.name.StartsWith("Forge Upgrade") ||
                        b.GetComponentInChildren<Text>()?.text == "開啟下一波" || b.GetComponentInChildren<Text>()?.text == "返回標題")
                    .ToArray();
                Assert.That(controls.Length, Is.EqualTo(6));
                var boxes = new List<Rect>();
                foreach (Button control in controls)
                {
                    RectTransform target = (RectTransform)control.transform;
                    Rect box = Bounds(canvasRect, target); boxes.Add(box);
                    Assert.That(Mathf.Min(box.width, box.height), Is.GreaterThanOrEqualTo(44f), $"{size} {control.name}");
                    var pointer = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(target.TransformPoint(target.rect.center)) };
                    var hits = new List<RaycastResult>(); canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0));
                    Assert.That(hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target), Is.True, $"{size} {control.name}");
                }
                for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.That(boxes[i].Overlaps(boxes[j]), Is.False, $"{size} forge controls overlap");
            }

            game.Hud.SafeRoot.anchorMin = Vector2.zero; game.Hud.SafeRoot.anchorMax = Vector2.one;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            Object.Destroy(cameraObject);
            yield return null;
        }

        private static Rect Bounds(RectTransform rootRect, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = rootRect.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
