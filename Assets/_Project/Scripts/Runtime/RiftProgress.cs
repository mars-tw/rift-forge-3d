using System;
using UnityEngine;

namespace RiftForge
{
    public enum RiftRoute { Campaign, Echo, Hunt, Crucible }

    public static class RiftRoutes
    {
        public static string Name(RiftRoute route) => new[] { "裂隙巡行", "回聲試煉", "獵陣試煉", "熔核試煉" }[(int)route];
        public static string Description(RiftRoute route) => new[] {
            "四波遠征，擊倒守門者。每波後鍛造。",
            "追擊者連砸兩次；離開第一個紅圈後繼續移動。",
            "衝鋒者交錯進攻；橫向閃避，抓住收招空檔。",
            "守門者帶著護衛登場；先拆衝鋒，再找反擊。" }[(int)route];
        public static string Story(RiftRoute route, int wave) => route == RiftRoute.Campaign
            ? new[] { "爐火尚存。從第一道裂縫帶回碎片。", "裂隙學會了衝鋒。別沿著槍尖逃。", "更深處的回音，正在呼喚守門者。", "最後一道門，需要你親手封上。" }[wave - 1]
            : wave == 3 ? "終點近了。把這次鍛造留在裂隙裡。" : "每一道落點都留下回聲。看清，再出手。";
        public static int RequiredSeals(RiftRoute route) => route == RiftRoute.Hunt ? 1 : route == RiftRoute.Crucible ? 3 : 0;
        public static int Waves(RiftRoute route) => route == RiftRoute.Campaign ? 4 : 3;
        public static int Count(RiftRoute route, int wave)
        {
            if (wave < 1 || wave > Waves(route)) throw new ArgumentOutOfRangeException(nameof(wave));
            if (route == RiftRoute.Campaign) return RiftRunState.EnemyCountForWave(wave);
            if (wave == 3) return route == RiftRoute.Echo ? 1 : route == RiftRoute.Hunt ? 3 : 4;
            return route == RiftRoute.Echo ? wave + 1 : wave + 2;
        }
        public static EnemyKind Kind(RiftRoute route, int wave, int index)
        {
            if (index < 0 || index >= Count(route, wave)) throw new ArgumentOutOfRangeException(nameof(index));
            if (route == RiftRoute.Campaign) return RiftRunState.KindForSpawn(wave, index);
            if (wave == 3 && index == 0) return EnemyKind.Warden;
            if (route == RiftRoute.Echo) return EnemyKind.Stalker;
            return route == RiftRoute.Hunt || index % 2 == 1 ? EnemyKind.Lancer : EnemyKind.Stalker;
        }
    }

    /// <summary>Offline progress. Date is the device's local date, without a server or anti-cheat claim.</summary>
    [Serializable]
    public sealed class RiftProgress
    {
        public int seals;
        public int achievements;
        public string dailyDate = "";
        public int dailyForges;
        public bool dailyClaimed;
        public bool initiationComplete;
        public void CompleteInitiation() { initiationComplete = true; Persist(); }
        private readonly Action<string> save;
        private readonly Func<DateTime> clock;
        public int Seals => seals;
        public int AchievementCount => ((achievements & 1) != 0 ? 1 : 0) + ((achievements & 2) != 0 ? 1 : 0) + ((achievements & 4) != 0 ? 1 : 0);
        public bool IsUnlocked(RiftRoute route) => seals >= RiftRoutes.RequiredSeals(route);
        public string DailyText { get { RollDate(); return dailyClaimed ? "今日鍛造已完成　印記已領取" : $"每日目標：鍛造兩次（{dailyForges}/2）　獎勵 1 印記"; } }
        public string AchievementText => $"封印{Mark(1)}　完美閃避三次{Mark(2)}　爆發命中三敵{Mark(4)}";
        private string Mark(int bit) => (achievements & bit) != 0 ? "✓" : "○";

        public RiftProgress(string json = null, Action<string> save = null, Func<DateTime> clock = null)
        {
            this.save = save;
            this.clock = clock ?? (() => DateTime.Now);
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, this); } catch (ArgumentException) { }
            }
            seals = Math.Max(0, seals);
            dailyForges = Math.Max(0, Math.Min(2, dailyForges));
            achievements &= 7;
            RollDate();
        }
        public static RiftProgress LoadLocal() => new RiftProgress(PlayerPrefs.GetString("riftforge.progress.v1", ""), json => {
            PlayerPrefs.SetString("riftforge.progress.v1", json); PlayerPrefs.Save();
        });
        public string Snapshot() => JsonUtility.ToJson(this);
        private void Persist() => save?.Invoke(Snapshot());
        private void RollDate()
        {
            string date = clock().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            // A clock rollback cannot reopen an already claimed earlier day.
            if (string.CompareOrdinal(date, dailyDate) <= 0) return;
            dailyDate = date; dailyForges = 0; dailyClaimed = false; Persist();
        }
        public bool RegisterForge()
        {
            RollDate();
            if (dailyClaimed) return false;
            dailyForges = Math.Min(2, dailyForges + 1);
            if (dailyForges >= 2) { dailyClaimed = true; seals++; }
            Persist();
            return dailyClaimed;
        }
        public void RegisterVictory() { seals++; achievements |= 1; Persist(); }
        public void RegisterPerfects(int count) { if (count >= 3 && (achievements & 2) == 0) { achievements |= 2; Persist(); } }
        public void RegisterBurst(int hits) { if (hits >= 3 && (achievements & 4) == 0) { achievements |= 4; Persist(); } }
    }
}
