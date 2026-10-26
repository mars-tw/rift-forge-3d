using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    public sealed class RiftStoreRulesTests
    {
        [Test] public void ImprintsKeepThreeLatestGroundPointsAndConsumeOnlyOnce()
        {
            var marks = new PhaseImprints();
            for (int i = 0; i < 4; i++) marks.Add(new Vector3(i, 10, i));
            Assert.That(marks.Count, Is.EqualTo(3));
            Assert.That(marks.Points[0], Is.EqualTo(new Vector3(1, 0, 1)));
            Assert.That(marks.Consume().Length, Is.EqualTo(3)); Assert.That(marks.Consume(), Is.Empty);
        }
        [Test] public void ForgeCheckpointRoundTripsPurchasedUpgradeAndRejectsInvalidData()
        {
            var run = new RiftRunState(); run.StartRun();
            while (run.EnemiesRemaining > 0) { int amount = run.EnemyDefeated(); run.CollectMaterial(amount); }
            Assert.That(run.BuyUpgrade(ForgeUpgrade.Blade), Is.True);
            var parsed = RiftCheckpoint.Parse(JsonUtility.ToJson(run.Checkpoint()));
            var loaded = new RiftRunState(); loaded.Restore(parsed);
            Assert.That(loaded.Phase, Is.EqualTo(RunPhase.Forge)); Assert.That(loaded.BladeLevel, Is.EqualTo(1));
            Assert.That(loaded.Material, Is.EqualTo(run.Material));
            parsed.wave = 99; Assert.That(parsed.IsValid, Is.False);
            Assert.That(RiftCheckpoint.Parse("broken"), Is.Null);
            Assert.That(RiftCheckpoint.Parse("{}"), Is.Null);
        }
        [Test] public void InitiationCompletionIsPersistentAndGrantsNoSeal()
        {
            string json = null; var progress = new RiftProgress(save: s => json = s);
            progress.CompleteInitiation(); var loaded = new RiftProgress(json);
            Assert.That(loaded.initiationComplete, Is.True); Assert.That(loaded.Seals, Is.Zero);
        }
    }
}
