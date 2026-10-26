using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace RiftForge.Tests.PlayMode
{
    public sealed class RiftGamePlayModeTests
    {
        [UnityTest]
        public IEnumerator DisablingTouchMoveAreaReleasesCapturedFinger()
        {
            GameObject areaObject = new GameObject("Test Move Area", typeof(RectTransform), typeof(TouchMoveArea));
            TouchMoveArea area = areaObject.GetComponent<TouchMoveArea>();
            try
            {
                Assert.That(area.Source.TryBeginPointer(7), Is.True);
                Assert.That(area.Source.TryMovePointer(7, Vector2.up), Is.True);
                areaObject.SendMessage("OnApplicationPause", true);
                Assert.That(area.Source.ActivePointerId, Is.Null);
                Assert.That(area.Source.ReadMove(), Is.EqualTo(Vector2.zero));
                areaObject.SendMessage("OnApplicationPause", false);
                Assert.That(area.Source.TryBeginPointer(8), Is.True);
                areaObject.SendMessage("OnApplicationFocus", false);
                Assert.That(area.Source.ActivePointerId, Is.Null);
                areaObject.SendMessage("OnApplicationFocus", true);
                Assert.That(area.Source.TryBeginPointer(9), Is.True);
                area.enabled = false;
                yield return null;
                Assert.That(area.Source.ActivePointerId, Is.Null);
                Assert.That(area.Source.ReadMove(), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.Destroy(areaObject);
            }
        }

        [UnityTest]
        public IEnumerator DodgeInvulnerabilityAndPauseProtectThePlayer()
        {
            GameObject root = new GameObject("Test Defensive Actions");
            RiftGame game = root.AddComponent<RiftGame>();
            try
            {
                game.StartNewRun();
                game.Dodge();
                Assert.That(game.TryDamagePlayer(20), Is.False);
                yield return new WaitForSeconds(0.5f);
                Assert.That(game.TryDamagePlayer(20), Is.True);
                int health = game.Run.Health;
                game.SetPaused(true);
                Assert.That(game.IsPaused, Is.True);
                Assert.That(game.TryDamagePlayer(20), Is.False);
                yield return new WaitForSeconds(0.5f);
                Assert.That(game.Run.Health, Is.EqualTo(health));
                game.SetPaused(false);
                Assert.That(game.IsPaused, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator ARunMovesFromCombatToForgeAndCanBeRestartedAfterDefeat()
        {
            GameObject root = new GameObject("Test Rift Game");
            RiftGame game = root.AddComponent<RiftGame>();
            try
            {
                Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Title));
                game.StartNewRun();
                Assert.That(game.ActiveEnemyCount, Is.EqualTo(3));
                foreach (RiftEnemy enemy in Object.FindObjectsByType<RiftEnemy>())
                    if (enemy != null && enemy.Health > 0 && enemy.transform.IsChildOf(root.transform))
                        enemy.TakeHit(999);
                Assert.That(game.Run.EnemiesRemaining, Is.Zero);
                Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Combat));
                yield return WaitForPhase(game, RunPhase.Forge, 5f);
                Assert.That(game.Run.Material, Is.EqualTo(9));
                Assert.That(game.Purchase(ForgeUpgrade.Blade), Is.True);
                Assert.That(game.Run.AttackDamage, Is.EqualTo(37));
                game.ContinueFromForge();
                Assert.That(game.Run.Wave, Is.EqualTo(2));
                Assert.That(game.TryDamagePlayer(999), Is.True);
                Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Defeat));
                game.StartNewRun();
                Assert.That(game.Run.Wave, Is.EqualTo(1));
                Assert.That(game.Run.Health, Is.EqualTo(100));
                Assert.That(game.Run.BladeLevel, Is.Zero);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator FourEncountersTransitionWithInjectedLethalHits()
        {
            GameObject root = new GameObject("Test Boss Run");
            RiftGame game = root.AddComponent<RiftGame>();
            try
            {
                game.StartNewRun();
                for (int wave = 1; wave <= RiftRunState.FinalWave; wave++)
                {
                    yield return null;
                    foreach (RiftEnemy enemy in Object.FindObjectsByType<RiftEnemy>())
                        if (enemy != null && enemy.Health > 0 && enemy.transform.IsChildOf(root.transform))
                            enemy.TakeHit(999);
                    yield return WaitForPhase(game, wave == RiftRunState.FinalWave ? RunPhase.Victory : RunPhase.Forge, 5f);
                    if (wave < RiftRunState.FinalWave) game.ContinueFromForge();
                }
                Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Victory));
                Assert.That(game.ActiveEnemyCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator PlayerControlsAndForgePurchasesCanSealTheWarden()
        {
            GameObject root = new GameObject("Test Playable Full Run");
            RiftGame game = root.AddComponent<RiftGame>();
            TouchMoveArea moveArea = null;
            bool dodgedTheWarden = false;

            try
            {
                game.StartNewRun();
                moveArea = Object.FindAnyObjectByType<TouchMoveArea>();
                Assert.That(moveArea, Is.Not.Null, "The mobile movement pad must be present during combat.");
                TouchPlayerInputSource movement = moveArea.Source;
                for (int wave = 1; wave <= RiftRunState.FinalWave; wave++)
                {
                    Assert.That(game.Run.Wave, Is.EqualTo(wave));
                    Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Combat));
                    Assert.That(movement.TryBeginPointer(900 + wave), Is.True);
                    float waveStarted = Time.time;
                    float deadline = Time.realtimeSinceStartup + 30f;

                    while (game.Run.Phase == RunPhase.Combat && Time.realtimeSinceStartup < deadline)
                    {
                        RiftEnemy nearest = null;
                        float nearestDistanceSquared = float.MaxValue;
                        foreach (RiftEnemy enemy in Object.FindObjectsByType<RiftEnemy>())
                        {
                            if (enemy == null || enemy.Health <= 0 || !enemy.transform.IsChildOf(root.transform))
                                continue;
                            Vector3 offset = enemy.transform.position - game.PlayerPosition;
                            offset.y = 0f;
                            if (offset.sqrMagnitude >= nearestDistanceSquared) continue;
                            nearest = enemy;
                            nearestDistanceSquared = offset.sqrMagnitude;
                        }

                        if (nearest != null && nearestDistanceSquared > 2.2f * 2.2f)
                        {
                            Vector3 offset = nearest.transform.position - game.PlayerPosition;
                            movement.TryMovePointer(900 + wave, new Vector2(offset.x, offset.z).normalized);
                        }
                        else movement.TryMovePointer(900 + wave, Vector2.zero);

                        if (game.AttackCooldownRemaining <= 0f) game.Attack();
                        if (wave == RiftRunState.FinalWave && !dodgedTheWarden && Time.time - waveStarted >= 0.95f)
                        {
                            game.Dodge();
                            dodgedTheWarden = true;
                        }
                        yield return null;
                    }

                    movement.CancelPointer(900 + wave);
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"Rift {wave} timed out.");
                    Assert.That(game.Run.Phase,
                        Is.EqualTo(wave == RiftRunState.FinalWave ? RunPhase.Victory : RunPhase.Forge),
                        $"Rift {wave} ended with {game.Run.Health} core and {game.Run.EnemiesRemaining} hostiles.");

                    if (wave == 1)
                    {
                        Assert.That(game.Purchase(ForgeUpgrade.Blade), Is.True);
                        Assert.That(game.Purchase(ForgeUpgrade.Vitality), Is.True);
                    }
                    else if (wave == 2)
                    {
                        Assert.That(game.Purchase(ForgeUpgrade.Blade), Is.True);
                        Assert.That(game.Purchase(ForgeUpgrade.Blade), Is.True);
                        Assert.That(game.Purchase(ForgeUpgrade.Vitality), Is.True);
                    }
                    else if (wave == 3)
                    {
                        Assert.That(game.Purchase(ForgeUpgrade.Vitality), Is.True);
                        Assert.That(game.Run.AttackDamage, Is.EqualTo(55));
                        Assert.That(game.Run.Health, Is.GreaterThan(28),
                            "The final Vitality heal must leave enough core to survive one Warden strike.");
                    }

                    if (wave < RiftRunState.FinalWave) game.ContinueFromForge();
                }

                Assert.That(dodgedTheWarden, Is.True);
                Assert.That(game.Run.Phase, Is.EqualTo(RunPhase.Victory));
                Assert.That(game.Run.DropsRemaining, Is.Zero,
                    "The Warden's shard must be collected before victory.");
            }
            finally
            {
                Object.Destroy(root);
                if (moveArea != null) Object.Destroy(moveArea.transform.root.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator HoldingStrikeRepeatsOnlyAtTheNormalCooldownAndStopsOnRelease()
        {
            GameObject root = new GameObject("Test Hold Strike");
            RiftGame game = root.AddComponent<RiftGame>();
            HoldStrikeButton strike = FindStrikeBoundTo(game);
            try
            {
                game.StartNewRun();
                Assert.That(strike, Is.Not.Null);
                var touch = new PointerEventData(EventSystem.current)
                {
                    pointerId = 71,
                    button = PointerEventData.InputButton.Left
                };

                ExecuteEvents.Execute(strike.gameObject, touch, ExecuteEvents.pointerDownHandler);
                Assert.That(strike.IsHeld, Is.True);
                Assert.That(game.AttackCooldownRemaining, Is.GreaterThan(0.44f),
                    "The first strike should happen when the finger goes down.");

                yield return new WaitForSeconds(0.2f);
                Assert.That(game.AttackCooldownRemaining, Is.LessThan(0.4f),
                    "Holding must not bypass the existing 0.46-second cooldown.");

                yield return new WaitForSeconds(0.8f);
                Assert.That(game.AttackCooldownRemaining, Is.GreaterThan(0.05f),
                    "A held finger should cause a later strike after the cooldown expires.");

                ExecuteEvents.Execute(strike.gameObject, touch, ExecuteEvents.pointerUpHandler);
                Assert.That(strike.IsHeld, Is.False);
                float cooldownOnRelease = game.AttackCooldownRemaining;
                ExecuteEvents.Execute(strike.gameObject, touch, ExecuteEvents.pointerClickHandler);
                Assert.That(game.AttackCooldownRemaining, Is.EqualTo(cooldownOnRelease).Within(0.02f));

                yield return new WaitForSeconds(0.6f);
                Assert.That(game.AttackCooldownRemaining, Is.Zero.Within(0.01f),
                    "Repeating must stop after the finger is released.");
                strike.OnClick(); // Button activation without a pointer still works.
                Assert.That(game.AttackCooldownRemaining, Is.GreaterThan(0.44f));
            }
            finally
            {
                Object.Destroy(root);
                if (strike != null) Object.Destroy(strike.transform.root.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator StrikeHoldIgnoresOtherFingersAndCancelsOnExitOrPause()
        {
            GameObject root = new GameObject("Test Hold Strike Cancellation");
            RiftGame game = root.AddComponent<RiftGame>();
            HoldStrikeButton strike = FindStrikeBoundTo(game);
            try
            {
                game.StartNewRun();
                Assert.That(strike, Is.Not.Null);
                var first = new PointerEventData(EventSystem.current)
                {
                    pointerId = 81,
                    button = PointerEventData.InputButton.Left
                };
                var other = new PointerEventData(EventSystem.current)
                {
                    pointerId = 82,
                    button = PointerEventData.InputButton.Left
                };

                strike.OnPointerDown(first);
                strike.OnPointerDown(other);
                strike.OnPointerUp(other);
                Assert.That(strike.IsHeld, Is.True, "A second finger must not replace or release the first.");
                strike.OnPointerExit(first);
                Assert.That(strike.IsHeld, Is.False);

                strike.OnPointerDown(first);
                Assert.That(strike.IsHeld, Is.True);
                game.SetPaused(true);
                Assert.That(strike.IsHeld, Is.False, "Hiding the combat controls must cancel the hold.");
                game.SetPaused(false);
                yield return new WaitForSeconds(0.6f);
                Assert.That(game.AttackCooldownRemaining, Is.Zero.Within(0.01f));
            }
            finally
            {
                Object.Destroy(root);
                if (strike != null) Object.Destroy(strike.transform.root.gameObject);
            }
        }

        private static HoldStrikeButton FindStrikeBoundTo(RiftGame game)
        {
            foreach (HoldStrikeButton strike in Object.FindObjectsByType<HoldStrikeButton>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (strike.Owner == game) return strike;
            return null;
        }

        private static IEnumerator WaitForPhase(RiftGame game, RunPhase expected, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (game.Run.Phase != expected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(game.Run.Phase, Is.EqualTo(expected), "Encounter did not settle in time.");
        }
    }
}
