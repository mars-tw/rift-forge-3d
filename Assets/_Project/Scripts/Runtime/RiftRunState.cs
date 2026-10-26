using System;

namespace RiftForge
{
    public enum RunPhase { Title, Combat, Forge, Victory, Defeat }
    public enum ForgeUpgrade { Blade, Vitality, Dodge }
    public enum EnemyKind { Stalker, Lancer, Warden }
    public enum ForgeContract { None, Counter, Finisher, Imprint }

    /// <summary>Pure, deterministic rules for a single offline run. A drop must be collected before a wave ends.</summary>
    public sealed class RiftRunState
    {
        public const int FinalWave = 4;
        public RunPhase Phase { get; private set; } = RunPhase.Title;
        public int Wave { get; private set; }
        public int EnemiesRemaining { get; private set; }
        public int DropsRemaining { get; private set; }
        public int Material { get; private set; }
        public int Health { get; private set; }
        public int BladeLevel { get; private set; }
        public int VitalityLevel { get; private set; }
        public int DodgeLevel { get; private set; }
        public ForgeContract NextContract { get; private set; }
        public ForgeContract ActiveContract { get; private set; }
        public int ContractProgress { get; private set; }
        public int ContractTarget => ContractTargetFor(ActiveContract);
        public int ContractReward { get; private set; }
        public int MaximumHealth => 100 + 25 * VitalityLevel;
        public int AttackDamage => 28 + 9 * BladeLevel;
        public float DodgeCooldown => Math.Max(0.55f, 2f - 0.32f * DodgeLevel);
        public RiftRoute Route { get; private set; }
        public int WaveLimit => RiftRoutes.Waves(Route);
        public bool IsBossWave => Wave == WaveLimit;
        public int CurrentEnemyCount => RiftRoutes.Count(Route, Wave);
        public EnemyKind CurrentKind(int index) => RiftRoutes.Kind(Route, Wave, index);
        public void ReturnToTitle() { Phase = RunPhase.Title; }

        public RiftCheckpoint Checkpoint() => new RiftCheckpoint { route = (int)Route, wave = Wave,
            health = Health, material = Material, blade = BladeLevel, vitality = VitalityLevel,
            dodge = DodgeLevel, forge = Phase == RunPhase.Forge, contractSchema = 1,
            nextContract = Phase == RunPhase.Forge ? (int)NextContract : (int)ForgeContract.None,
            activeContract = (int)ActiveContract,
            contractProgress = Phase == RunPhase.Forge ? ContractProgress : 0,
            contractReward = Phase == RunPhase.Forge ? ContractReward : 0 };
        public void Restore(RiftCheckpoint c)
        {
            if (c == null || !c.IsValid) throw new ArgumentException("Invalid checkpoint.");
            Route = (RiftRoute)c.route; Wave = c.wave; Health = c.health; Material = c.material;
            BladeLevel = c.blade; VitalityLevel = c.vitality; DodgeLevel = c.dodge;
            bool hasContracts = c.contractSchema == 1;
            NextContract = hasContracts && c.forge ? (ForgeContract)c.nextContract : ForgeContract.None;
            ActiveContract = hasContracts ? (ForgeContract)c.activeContract : ForgeContract.None;
            ContractProgress = hasContracts && c.forge ? c.contractProgress : 0;
            ContractReward = hasContracts && c.forge ? c.contractReward : 0;
            Phase = c.forge ? RunPhase.Forge : RunPhase.Combat;
            EnemiesRemaining = c.forge ? 0 : CurrentEnemyCount; DropsRemaining = 0;
        }

        public static int EnemyCountForWave(int wave)
        {
            if (wave < 1 || wave > FinalWave) throw new ArgumentOutOfRangeException(nameof(wave));
            return wave == FinalWave ? 1 : wave + 2;
        }

        public static int ContractTargetFor(ForgeContract contract)
        {
            switch (contract)
            {
                case ForgeContract.None: return 0;
                case ForgeContract.Counter: return 1;
                case ForgeContract.Finisher:
                case ForgeContract.Imprint: return 2;
                default: throw new ArgumentOutOfRangeException(nameof(contract));
            }
        }

        public static int ContractRewardFor(ForgeContract contract)
        {
            switch (contract)
            {
                case ForgeContract.None: return 0;
                case ForgeContract.Counter: return 4;
                case ForgeContract.Finisher: return 5;
                case ForgeContract.Imprint: return 6;
                default: throw new ArgumentOutOfRangeException(nameof(contract));
            }
        }

        /// <summary>
        /// Wave composition: wave 1 teaches the ring telegraph with Stalkers only, waves 2-3 mix in
        /// line-charging Lancers (one, then two), and the final wave is the Warden alone.
        /// </summary>
        public static EnemyKind KindForSpawn(int wave, int index)
        {
            int count = EnemyCountForWave(wave);
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            if (wave == FinalWave) return EnemyKind.Warden;
            int lancers = wave - 1;
            return index >= count - lancers ? EnemyKind.Lancer : EnemyKind.Stalker;
        }

        public int UpgradeLevel(ForgeUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ForgeUpgrade.Blade: return BladeLevel;
                case ForgeUpgrade.Vitality: return VitalityLevel;
                case ForgeUpgrade.Dodge: return DodgeLevel;
                default: throw new ArgumentOutOfRangeException(nameof(upgrade));
            }
        }

        public int UpgradeCost(ForgeUpgrade upgrade)
        {
            int level = UpgradeLevel(upgrade);
            switch (upgrade)
            {
                case ForgeUpgrade.Blade: return 3 + 2 * level;
                case ForgeUpgrade.Vitality: return 4 + 2 * level;
                case ForgeUpgrade.Dodge: return 3 + 2 * level;
                default: throw new ArgumentOutOfRangeException(nameof(upgrade));
            }
        }

        public void StartRun(RiftRoute route = RiftRoute.Campaign)
        {
            Route = route;
            Wave = 1;
            Health = 100;
            Material = 0;
            BladeLevel = VitalityLevel = DodgeLevel = 0;
            NextContract = ActiveContract = ForgeContract.None;
            ContractProgress = ContractReward = 0;
            DropsRemaining = 0;
            EnemiesRemaining = CurrentEnemyCount;
            Phase = RunPhase.Combat;
        }

        public bool DamagePlayer(int amount)
        {
            if (Phase != RunPhase.Combat || amount <= 0) return false;
            Health = Math.Max(0, Health - amount);
            if (Health == 0) Phase = RunPhase.Defeat;
            return true;
        }

        /// <summary>Register a slain enemy. Returns its material value for the pickup visual.</summary>
        public int EnemyDefeated()
        {
            if (Phase != RunPhase.Combat || EnemiesRemaining <= 0)
                throw new InvalidOperationException("No active enemy to defeat.");
            EnemiesRemaining--;
            DropsRemaining++;
            return IsBossWave ? 8 : 2 + Wave;
        }

        public void CollectMaterial(int amount)
        {
            if (Phase != RunPhase.Combat || DropsRemaining <= 0 || amount <= 0)
                throw new InvalidOperationException("No valid material drop to collect.");
            Material += amount;
            DropsRemaining--;
            if (EnemiesRemaining == 0 && DropsRemaining == 0)
            {
                if (ContractReward == 0 && ActiveContract != ForgeContract.None && ContractProgress >= ContractTarget)
                {
                    ContractReward = ContractRewardFor(ActiveContract);
                    Material += ContractReward;
                }
                Phase = IsBossWave ? RunPhase.Victory : RunPhase.Forge;
            }
        }

        public bool SelectContract(ForgeContract contract)
        {
            if (Phase != RunPhase.Forge || !Enum.IsDefined(typeof(ForgeContract), contract)) return false;
            NextContract = contract;
            return true;
        }

        public void RegisterContractImpact(StrikeKind kind, int actualStrikeHits, int actualImprintHits)
        {
            if (Phase != RunPhase.Combat || ActiveContract == ForgeContract.None) return;
            int gain = 0;
            if (ActiveContract == ForgeContract.Counter && kind == StrikeKind.Counter && actualStrikeHits > 0) gain = 1;
            else if (ActiveContract == ForgeContract.Finisher && kind == StrikeKind.Finisher && actualStrikeHits > 0) gain = 1;
            else if (ActiveContract == ForgeContract.Imprint && kind == StrikeKind.Burst && actualImprintHits > 0) gain = actualImprintHits;
            ContractProgress = Math.Min(ContractTarget, ContractProgress + gain);
        }

        public bool BuyUpgrade(ForgeUpgrade upgrade)
        {
            if (Phase != RunPhase.Forge || UpgradeLevel(upgrade) >= 3) return false;
            int cost = UpgradeCost(upgrade);
            if (Material < cost) return false;
            Material -= cost;
            switch (upgrade)
            {
                case ForgeUpgrade.Blade: BladeLevel++; break;
                case ForgeUpgrade.Vitality:
                    VitalityLevel++;
                    Health = Math.Min(MaximumHealth, Health + 35);
                    break;
                case ForgeUpgrade.Dodge: DodgeLevel++; break;
            }
            return true;
        }

        public bool BeginNextWave()
        {
            if (Phase != RunPhase.Forge || Wave >= WaveLimit) return false;
            Wave++;
            ActiveContract = NextContract;
            NextContract = ForgeContract.None;
            ContractProgress = ContractReward = 0;
            EnemiesRemaining = CurrentEnemyCount;
            DropsRemaining = 0;
            Phase = RunPhase.Combat;
            return true;
        }
    }
}
