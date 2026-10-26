using System;
using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    public sealed class CombatRhythmTests
    {
        [Test]
        public void ComboChainsSlashCrossFinisherThenRestarts()
        {
            var rhythm = new CombatRhythm();
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Slash));
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Cross));
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Finisher));
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Slash));
        }

        [Test]
        public void ComboResetsWhenTheWindowExpires()
        {
            var rhythm = new CombatRhythm();
            rhythm.NextStrike();
            rhythm.NextStrike();
            rhythm.Tick(CombatRhythm.ComboWindow + 0.01f);
            Assert.That(rhythm.ComboStep, Is.Zero);
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Slash));
        }

        [Test]
        public void FinisherHasWiderArcMoreReachAndDamage()
        {
            Assert.That(CombatRhythm.DamageMultiplier(StrikeKind.Finisher), Is.GreaterThan(CombatRhythm.DamageMultiplier(StrikeKind.Slash)));
            Assert.That(CombatRhythm.Reach(StrikeKind.Finisher), Is.GreaterThan(CombatRhythm.Reach(StrikeKind.Slash)));
            Assert.That(CombatRhythm.ArcDot(StrikeKind.Finisher), Is.LessThan(CombatRhythm.ArcDot(StrikeKind.Slash)));
            Assert.That(CombatRhythm.ArcDot(StrikeKind.Burst), Is.LessThan(-1f), "Burst must hit all around the player.");
        }

        [Test]
        public void HitsFillChargeAndAFullMeterTurnsTheNextStrikeIntoBurst()
        {
            var rhythm = new CombatRhythm();
            int guard = 0;
            while (!rhythm.BurstReady && guard++ < 100) rhythm.RegisterImpact(StrikeKind.Slash, 1);
            Assert.That(rhythm.BurstReady, Is.True);
            Assert.That(guard, Is.InRange(10, 20), "Burst should take a meaningful but reachable number of hits.");
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Burst));
            Assert.That(rhythm.Charge, Is.Zero);
            Assert.That(rhythm.BurstReady, Is.False);
        }

        [Test]
        public void WhiffsDoNotBuildChargeOrStreak()
        {
            var rhythm = new CombatRhythm();
            rhythm.RegisterImpact(StrikeKind.Finisher, 0);
            Assert.That(rhythm.Charge, Is.Zero);
            Assert.That(rhythm.HitStreak, Is.Zero);
        }

        [Test]
        public void PerfectDodgeWindowOnlyAcceptsLateDodgesInsideTheThreat()
        {
            Assert.That(CombatRhythm.IsPerfectDodge(true, 0.2f), Is.True);
            Assert.That(CombatRhythm.IsPerfectDodge(true, CombatRhythm.PerfectDodgeWindow), Is.True);
            Assert.That(CombatRhythm.IsPerfectDodge(true, CombatRhythm.PerfectDodgeWindow + 0.05f), Is.False, "Too early.");
            Assert.That(CombatRhythm.IsPerfectDodge(false, 0.2f), Is.False, "Not in danger.");
            Assert.That(CombatRhythm.IsPerfectDodge(true, -1f), Is.False, "Not telegraphing.");
        }

        [Test]
        public void PerfectDodgeArmsACounterThatExpires()
        {
            var rhythm = new CombatRhythm();
            rhythm.RegisterPerfectDodge();
            Assert.That(rhythm.CounterReady, Is.True);
            Assert.That(rhythm.PerfectDodges, Is.EqualTo(1));
            Assert.That(rhythm.Charge, Is.EqualTo(CombatRhythm.ChargePerPerfectDodge));
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Counter));
            Assert.That(rhythm.CounterReady, Is.False);

            rhythm.RegisterPerfectDodge();
            rhythm.Tick(CombatRhythm.CounterWindow + 0.01f);
            Assert.That(rhythm.CounterReady, Is.False);
            Assert.That(rhythm.NextStrike(), Is.EqualTo(StrikeKind.Slash));
        }

        [Test]
        public void TakingDamageBreaksTheStreakButKeepsTheBest()
        {
            var rhythm = new CombatRhythm();
            rhythm.RegisterImpact(StrikeKind.Slash, 3);
            rhythm.RegisterPlayerHurt();
            Assert.That(rhythm.HitStreak, Is.Zero);
            Assert.That(rhythm.BestHitStreak, Is.EqualTo(3));
        }
    }

    public sealed class RunScoreTests
    {
        [Test]
        public void DefeatGradesByProgress()
        {
            Assert.That(new RunScore(false, 1, 30f, 2, 3, 0, 100).Grade, Is.EqualTo("D"));
            Assert.That(new RunScore(false, 3, 90f, 8, 6, 1, 125).Grade, Is.EqualTo("C"));
        }

        [Test]
        public void SkillfulFastVictoryOutranksSloppySlowVictory()
        {
            var sloppy = new RunScore(true, 4, 230f, 13, 4, 0, 180);
            var skilled = new RunScore(true, 4, 120f, 13, 16, 4, 30);
            Assert.That(skilled.Points, Is.GreaterThan(sloppy.Points));
            Assert.That(RunScore.GradeRank(skilled.Grade), Is.GreaterThan(RunScore.GradeRank(sloppy.Grade)));
            Assert.That(skilled.Grade, Is.EqualTo("S"));
        }

        [Test]
        public void PointsNeverNegativeAndBestComparisonIsStrict()
        {
            var awful = new RunScore(false, 1, 999f, 0, 0, 0, 5000);
            Assert.That(awful.Points, Is.Zero);
            var score = new RunScore(true, 4, 100f, 13, 10, 2, 50);
            Assert.That(score.Beats(score.Points - 1), Is.True);
            Assert.That(score.Beats(score.Points), Is.False);
        }

        [Test]
        public void MemoryRecordStoreKeepsOnlyBetterRuns()
        {
            var store = new MemoryRecordStore();
            var first = new RunScore(true, 4, 200f, 13, 4, 0, 100);
            var worse = new RunScore(false, 2, 60f, 3, 2, 0, 100);
            Assert.That(store.Submit(first), Is.True);
            Assert.That(store.Submit(worse), Is.False);
            Assert.That(store.BestPoints, Is.EqualTo(first.Points));
            Assert.That(store.BestGrade, Is.EqualTo(first.Grade));
            Assert.That(store.BestWave, Is.EqualTo(4));
        }
    }

    public sealed class WaveCompositionTests
    {
        [Test]
        public void LancersAreIntroducedGraduallyAndTheWardenIsAlone()
        {
            Assert.That(CountKind(1, EnemyKind.Lancer), Is.Zero, "Wave 1 teaches the ring telegraph only.");
            Assert.That(CountKind(2, EnemyKind.Lancer), Is.EqualTo(1));
            Assert.That(CountKind(3, EnemyKind.Lancer), Is.EqualTo(2));
            Assert.That(CountKind(4, EnemyKind.Warden), Is.EqualTo(1));
            Assert.That(RiftRunState.EnemyCountForWave(4), Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RiftRunState.KindForSpawn(1, 3));
        }

        private static int CountKind(int wave, EnemyKind kind)
        {
            int count = 0;
            for (int i = 0; i < RiftRunState.EnemyCountForWave(wave); i++)
                if (RiftRunState.KindForSpawn(wave, i) == kind) count++;
            return count;
        }
    }

    public sealed class RiftAudioSynthTests
    {
        [Test]
        public void EverySoundIsAudibleBoundedAndEndsSilently()
        {
            foreach (RiftSfx sfx in Enum.GetValues(typeof(RiftSfx)))
            {
                float[] samples = RiftAudioSynth.Render(sfx);
                Assert.That(samples.Length, Is.EqualTo(Mathf.RoundToInt(RiftAudioSynth.Duration(sfx) * RiftAudioSynth.SampleRate)), sfx.ToString());
                float peak = 0f;
                double energy = 0;
                foreach (float s in samples)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                    energy += s * s;
                }
                Assert.That(peak, Is.LessThanOrEqualTo(0.9f), sfx + " clips.");
                Assert.That(Math.Sqrt(energy / samples.Length), Is.GreaterThan(0.004), sfx + " is silent.");
                Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.02f), sfx + " ends with a click.");
                Assert.That(Mathf.Abs(samples[0]), Is.LessThan(0.02f), sfx + " starts with a click.");
            }
        }

        [Test]
        public void SynthesisIsDeterministic()
        {
            Assert.That(RiftAudioSynth.Render(RiftSfx.Hit), Is.EqualTo(RiftAudioSynth.Render(RiftSfx.Hit)));
        }
    }

    public sealed class CameraShakeTests
    {
        [Test]
        public void TraumaDecaysLinearlyAndClamps()
        {
            Assert.That(CameraRig.DecayTrauma(1f, 0.25f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(CameraRig.DecayTrauma(0.1f, 1f), Is.Zero);
            Assert.That(CameraRig.DecayTrauma(2f, 0f), Is.EqualTo(1f));
        }
    }
}
