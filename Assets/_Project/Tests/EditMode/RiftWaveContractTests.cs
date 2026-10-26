using NUnit.Framework;

namespace RiftForge.Tests.EditMode
{
    public sealed class RiftWaveContractTests
    {
        static void FinishWave(RiftRunState run)
        {
            while (run.EnemiesRemaining > 0) run.EnemyDefeated();
            while (run.DropsRemaining > 0) run.CollectMaterial(3);
        }

        static RiftRunState EnterWaveTwo(ForgeContract contract)
        {
            var run = new RiftRunState(); run.StartRun(); FinishWave(run);
            Assert.That(run.SelectContract(contract), Is.True); Assert.That(run.BeginNextWave(), Is.True);
            return run;
        }

        [TestCase(ForgeContract.Counter, StrikeKind.Counter, 1, 0, 1, 4)]
        [TestCase(ForgeContract.Finisher, StrikeKind.Finisher, 1, 0, 2, 5)]
        [TestCase(ForgeContract.Imprint, StrikeKind.Burst, 0, 2, 2, 6)]
        public void ContractsUseRealImpactsAndPayOnceAfterCollection(ForgeContract contract, StrikeKind kind, int hits, int imprints, int target, int reward)
        {
            var run = EnterWaveTwo(contract);
            run.RegisterContractImpact(kind, hits, imprints);
            if (run.ContractProgress < target) run.RegisterContractImpact(kind, hits, imprints);
            Assert.That(run.ContractProgress, Is.EqualTo(target));
            int before = run.Material; while (run.EnemiesRemaining > 0) run.EnemyDefeated();
            Assert.That(run.ContractReward, Is.Zero);
            while (run.DropsRemaining > 0) run.CollectMaterial(3);
            Assert.That(run.ContractReward, Is.EqualTo(reward));
            Assert.That(run.Material, Is.EqualTo(before + RiftRoutes.Count(run.Route, run.Wave) * 3 + reward));
        }

        [Test]
        public void MissesWrongPhaseNoneAndDefeatNeverProgressOrPay()
        {
            var run = new RiftRunState(); run.StartRun();
            run.RegisterContractImpact(StrikeKind.Counter, 4, 0); Assert.That(run.ContractProgress, Is.Zero);
            FinishWave(run); Assert.That(run.SelectContract(ForgeContract.Counter), Is.True);
            run.RegisterContractImpact(StrikeKind.Counter, 4, 0); Assert.That(run.ContractProgress, Is.Zero);
            run.BeginNextWave(); run.RegisterContractImpact(StrikeKind.Counter, 0, 0); run.RegisterContractImpact(StrikeKind.Slash, 3, 0);
            Assert.That(run.ContractProgress, Is.Zero); run.DamagePlayer(999); Assert.That(run.ContractReward, Is.Zero);
        }

        [Test]
        public void ForgeSelectionPaysNothingAndDoesNotLimitMultiplePurchases()
        {
            var run = new RiftRunState(); run.StartRun(); FinishWave(run); int before = run.Material;
            Assert.That(run.SelectContract(ForgeContract.Counter), Is.True); Assert.That(run.SelectContract(ForgeContract.Finisher), Is.True);
            Assert.That(run.Material, Is.EqualTo(before)); Assert.That(run.BuyUpgrade(ForgeUpgrade.Blade), Is.True); Assert.That(run.BuyUpgrade(ForgeUpgrade.Blade), Is.True);
        }

        [Test]
        public void ProgressCapsInvalidSelectionFailsAndStartRunResetsAllContractState()
        {
            var run = EnterWaveTwo(ForgeContract.Imprint);
            run.RegisterContractImpact(StrikeKind.Burst, 0, 99); Assert.That(run.ContractProgress, Is.EqualTo(2));
            FinishWave(run); Assert.That(run.SelectContract((ForgeContract)99), Is.False);
            run.SelectContract(ForgeContract.Counter); run.StartRun();
            Assert.That(run.NextContract, Is.EqualTo(ForgeContract.None)); Assert.That(run.ActiveContract, Is.EqualTo(ForgeContract.None));
            Assert.That(run.ContractProgress, Is.Zero); Assert.That(run.ContractReward, Is.Zero);
        }

        [Test]
        public void CheckpointsPreserveForgeSettlementButCombatRestartsWholeWave()
        {
            var run = EnterWaveTwo(ForgeContract.Finisher); run.RegisterContractImpact(StrikeKind.Finisher, 1, 0);
            var combat = run.Checkpoint(); Assert.That(combat.activeContract, Is.EqualTo((int)ForgeContract.Finisher)); Assert.That(combat.contractProgress, Is.Zero); Assert.That(combat.nextContract, Is.Zero);
            var restarted = new RiftRunState(); restarted.Restore(combat); Assert.That(restarted.ActiveContract, Is.EqualTo(ForgeContract.Finisher)); Assert.That(restarted.ContractProgress, Is.Zero);
            restarted.RegisterContractImpact(StrikeKind.Finisher, 1, 0); restarted.RegisterContractImpact(StrikeKind.Finisher, 1, 0); FinishWave(restarted); restarted.SelectContract(ForgeContract.Imprint);
            var forge = restarted.Checkpoint(); var restored = new RiftRunState(); restored.Restore(forge);
            Assert.That(restored.NextContract, Is.EqualTo(ForgeContract.Imprint)); Assert.That(restored.ContractReward, Is.EqualTo(5)); Assert.That(restored.Material, Is.EqualTo(restarted.Material));
        }

        [Test]
        public void LegacyAndMalformedExtensionsAreHandledWithoutInventedRewards()
        {
            var legacy = new RiftCheckpoint { route=0,wave=1,health=100,material=0,forge=false };
            Assert.That(legacy.IsValid, Is.True); var run = new RiftRunState(); run.Restore(legacy); Assert.That(run.ActiveContract, Is.EqualTo(ForgeContract.None)); Assert.That(run.ContractReward, Is.Zero);
            legacy.contractProgress=1; Assert.That(legacy.IsValid, Is.False);
            var malformed = new RiftCheckpoint { route=0,wave=1,health=100,contractSchema=1,activeContract=(int)ForgeContract.Counter,contractProgress=0,contractReward=4 };
            Assert.That(malformed.IsValid, Is.False);
        }
    }
}
