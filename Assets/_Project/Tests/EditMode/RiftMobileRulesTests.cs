using System;
using NUnit.Framework;

namespace RiftForge.Tests.EditMode
{
    public sealed class RiftMobileRulesTests
    {
        [TestCase(RiftRoute.Campaign,4)] [TestCase(RiftRoute.Echo,3)]
        [TestCase(RiftRoute.Hunt,3)] [TestCase(RiftRoute.Crucible,3)]
        public void EveryRouteReachesVictoryOnlyAfterCollectingFinalDrops(RiftRoute route,int waves)
        {
            var run=new RiftRunState(); run.StartRun(route);
            for(int wave=1;wave<=waves;wave++)
            {
                Assert.That(run.Wave,Is.EqualTo(wave));
                int count=run.CurrentEnemyCount;
                for(int i=0;i<count;i++)
                {
                    var kind=run.CurrentKind(i);
                    Assert.That(Enum.IsDefined(typeof(EnemyKind),kind),Is.True);
                    int value=run.EnemyDefeated();
                    Assert.That(run.Phase,Is.EqualTo(RunPhase.Combat));
                    run.CollectMaterial(value);
                }
                Assert.That(run.Phase,Is.EqualTo(wave==waves?RunPhase.Victory:RunPhase.Forge));
                if(wave<waves) Assert.That(run.BeginNextWave(),Is.True);
            }
        }
        [Test]
        public void TrialsHaveDistinctCompositionAndLockedTiers()
        {
            Assert.That(RiftRoutes.Kind(RiftRoute.Echo,1,0),Is.EqualTo(EnemyKind.Stalker));
            Assert.That(RiftRoutes.Kind(RiftRoute.Hunt,1,0),Is.EqualTo(EnemyKind.Lancer));
            Assert.That(RiftRoutes.Count(RiftRoute.Crucible,3),Is.EqualTo(4));
            var p=new RiftProgress();
            Assert.That(p.IsUnlocked(RiftRoute.Echo),Is.True);
            Assert.That(p.IsUnlocked(RiftRoute.Hunt),Is.False);
            p.RegisterVictory(); Assert.That(p.IsUnlocked(RiftRoute.Hunt),Is.True);
            Assert.That(p.IsUnlocked(RiftRoute.Crucible),Is.False);
            p.RegisterVictory(); p.RegisterVictory(); Assert.That(p.IsUnlocked(RiftRoute.Crucible),Is.True);
        }
        [Test]
        public void DailyRewardPersistsAndCannotBeClaimedTwiceOrByRollingClockBack()
        {
            var date=new DateTime(2026,10,15);
            string saved="";
            var p=new RiftProgress(save:json=>saved=json,clock:()=>date);
            Assert.That(p.RegisterForge(),Is.False); Assert.That(p.RegisterForge(),Is.True);
            Assert.That(p.Seals,Is.EqualTo(1)); Assert.That(p.RegisterForge(),Is.False);
            p=new RiftProgress(saved,clock:()=>date);
            Assert.That(p.Seals,Is.EqualTo(1)); Assert.That(p.RegisterForge(),Is.False);
            date=date.AddDays(-1); Assert.That(p.RegisterForge(),Is.False);
            date=date.AddDays(2); Assert.That(p.RegisterForge(),Is.False);
            Assert.That(p.RegisterForge(),Is.True); Assert.That(p.Seals,Is.EqualTo(2));
        }
        [Test]
        public void AchievementsSurviveReloadAndDoNotGrantRepeatedRewards()
        {
            var p=new RiftProgress(); p.RegisterPerfects(2); p.RegisterBurst(2);
            Assert.That(p.AchievementCount,Is.Zero);
            p.RegisterPerfects(3); p.RegisterBurst(3); p.RegisterVictory();
            p=new RiftProgress(p.Snapshot());
            Assert.That(p.AchievementCount,Is.EqualTo(3));
            p.RegisterPerfects(30); p.RegisterBurst(30); Assert.That(p.Seals,Is.EqualTo(1));
        }
        [Test]
        public void InvalidProgressAndCatalogIndicesAreBounded()
        {
            var p=new RiftProgress("{\"seals\":-20,\"dailyForges\":500,\"achievements\":255}");
            Assert.That(p.Seals,Is.Zero); Assert.That(p.AchievementCount,Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>RiftRoutes.Count(RiftRoute.Echo,0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>RiftRoutes.Kind(RiftRoute.Hunt,1,99));
        }
    }
}
