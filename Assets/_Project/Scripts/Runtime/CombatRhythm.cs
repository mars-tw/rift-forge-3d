using System;

namespace RiftForge
{
    /// <summary>Kind of strike produced by <see cref="CombatRhythm.NextStrike"/>.</summary>
    public enum StrikeKind { Slash, Cross, Finisher, Counter, Burst }

    /// <summary>
    /// Pure combat-skill rules: three-step combo chain, Forge charge meter, the perfect-dodge
    /// (Phase Counter) window and the guaranteed counter strike that follows it.
    /// Independent of Unity so it can be tested deterministically.
    /// </summary>
    public sealed class CombatRhythm
    {
        public const float ComboWindow = 0.75f;
        public const float ChargeMax = 100f;
        public const float ChargePerHit = 7f;
        public const float ChargePerFinisherHit = 12f;
        public const float ChargePerPerfectDodge = 35f;
        public const float PerfectDodgeWindow = 0.4f;
        public const float CounterWindow = 1.6f;

        private float comboTimer;
        private float counterTimer;

        /// <summary>Strikes already chained in the current combo (0 when the chain is idle).</summary>
        public int ComboStep { get; private set; }
        /// <summary>Consecutive enemy hits without taking damage or whiffing a long pause.</summary>
        public int HitStreak { get; private set; }
        public int BestHitStreak { get; private set; }
        public float Charge { get; private set; }
        public bool BurstReady => Charge >= ChargeMax;
        public bool CounterReady => counterTimer > 0f;
        public int PerfectDodges { get; private set; }

        public void Reset()
        {
            comboTimer = counterTimer = 0f;
            ComboStep = HitStreak = BestHitStreak = PerfectDodges = 0;
            Charge = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (comboTimer > 0f)
            {
                comboTimer = Math.Max(0f, comboTimer - deltaTime);
                if (comboTimer == 0f) ComboStep = 0;
            }
            if (counterTimer > 0f) counterTimer = Math.Max(0f, counterTimer - deltaTime);
        }

        /// <summary>Decide the kind of the strike being started and advance the chain.</summary>
        public StrikeKind NextStrike()
        {
            if (BurstReady)
            {
                Charge = 0f;
                ComboStep = 0;
                comboTimer = 0f;
                counterTimer = 0f;
                return StrikeKind.Burst;
            }
            if (counterTimer > 0f)
            {
                counterTimer = 0f;
                ComboStep = 0;
                comboTimer = 0f;
                return StrikeKind.Counter;
            }
            ComboStep = ComboStep >= 3 ? 1 : ComboStep + 1;
            comboTimer = ComboWindow;
            StrikeKind kind = ComboStep == 1 ? StrikeKind.Slash : ComboStep == 2 ? StrikeKind.Cross : StrikeKind.Finisher;
            if (ComboStep == 3)
            {
                ComboStep = 0;
                comboTimer = 0f;
            }
            return kind;
        }

        public static float DamageMultiplier(StrikeKind kind)
        {
            switch (kind)
            {
                case StrikeKind.Finisher: return 1.6f;
                case StrikeKind.Counter: return 2f;
                case StrikeKind.Burst: return 2.2f;
                default: return 1f;
            }
        }

        public static float Reach(StrikeKind kind)
        {
            switch (kind)
            {
                case StrikeKind.Finisher: return 3.2f;
                case StrikeKind.Counter: return 3.1f;
                case StrikeKind.Burst: return 4.2f;
                default: return 2.8f;
            }
        }

        /// <summary>Minimum dot product between facing and target; -1 means a full circle.</summary>
        public static float ArcDot(StrikeKind kind)
        {
            switch (kind)
            {
                case StrikeKind.Finisher: return -0.35f;
                case StrikeKind.Burst: return -1.01f;
                default: return -0.06f;
            }
        }

        /// <summary>Register the result of a strike's impact frame.</summary>
        public void RegisterImpact(StrikeKind kind, int enemiesHit)
        {
            if (enemiesHit <= 0) return;
            HitStreak += enemiesHit;
            if (HitStreak > BestHitStreak) BestHitStreak = HitStreak;
            if (kind == StrikeKind.Burst) return;
            float perHit = kind == StrikeKind.Finisher ? ChargePerFinisherHit : ChargePerHit;
            Charge = Math.Min(ChargeMax, Charge + perHit * enemiesHit);
        }

        public void RegisterPlayerHurt()
        {
            HitStreak = 0;
            ComboStep = 0;
            comboTimer = 0f;
        }

        /// <summary>A dodge is perfect if the player is inside a strike zone that lands within the window.</summary>
        public static bool IsPerfectDodge(bool insideThreat, float secondsUntilStrike)
        {
            return insideThreat && secondsUntilStrike >= 0f && secondsUntilStrike <= PerfectDodgeWindow;
        }

        public void RegisterPerfectDodge()
        {
            PerfectDodges++;
            counterTimer = CounterWindow;
            Charge = Math.Min(ChargeMax, Charge + ChargePerPerfectDodge);
        }
    }
}
