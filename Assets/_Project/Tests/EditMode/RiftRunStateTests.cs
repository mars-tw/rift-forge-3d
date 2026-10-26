using System;
using NUnit.Framework;

namespace RiftForge.Tests.EditMode
{
    public sealed class RiftRunStateTests
    {
        [Test]
        public void WaveOnlyEndsWhenEveryEnemyDropIsCollected()
        {
            var run = new RiftRunState();
            run.StartRun();
            Assert.That(run.EnemiesRemaining, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) run.EnemyDefeated();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Combat));
            for (int i = 0; i < 3; i++) run.CollectMaterial(3);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Forge));
            Assert.That(run.Material, Is.EqualTo(9));
        }

        [Test]
        public void ForgePurchasesScaleAndCannotOverspend()
        {
            var run = new RiftRunState();
            run.StartRun();
            for (int i = 0; i < 3; i++) run.CollectMaterial(run.EnemyDefeated());
            Assert.That(run.BuyUpgrade(ForgeUpgrade.Blade), Is.True);
            Assert.That(run.AttackDamage, Is.EqualTo(37));
            Assert.That(run.UpgradeCost(ForgeUpgrade.Blade), Is.EqualTo(5));
            Assert.That(run.BuyUpgrade(ForgeUpgrade.Blade), Is.True);
            Assert.That(run.Material, Is.EqualTo(1));
            Assert.That(run.BuyUpgrade(ForgeUpgrade.Vitality), Is.False);
            Assert.That(run.VitalityLevel, Is.Zero);
        }

        [Test]
        public void BossVictoryRequiresCollectionAndDefeatTakesPrecedence()
        {
            var run = new RiftRunState();
            run.StartRun();
            for (int wave = 1; wave <= 3; wave++)
            {
                for (int i = 0; i < RiftRunState.EnemyCountForWave(wave); i++)
                    run.CollectMaterial(run.EnemyDefeated());
                Assert.That(run.BeginNextWave(), Is.True);
            }
            Assert.That(run.IsBossWave, Is.True);
            int bossDrop = run.EnemyDefeated();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(run.DamagePlayer(999), Is.True);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Defeat));
            Assert.Throws<InvalidOperationException>(() => run.CollectMaterial(bossDrop));
            run.StartRun();
            Assert.That(run.Wave, Is.EqualTo(1));
            Assert.That(run.Material, Is.Zero);
            Assert.That(run.Health, Is.EqualTo(100));
        }

        [Test]
        public void CollectedBossDropWins()
        {
            var run = new RiftRunState();
            run.StartRun();
            for (int wave = 1; wave <= 4; wave++)
            {
                for (int i = 0; i < RiftRunState.EnemyCountForWave(wave); i++)
                    run.CollectMaterial(run.EnemyDefeated());
                if (wave < 4) run.BeginNextWave();
            }
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Victory));
            Assert.That(run.DamagePlayer(999), Is.False);
        }
    }
}
