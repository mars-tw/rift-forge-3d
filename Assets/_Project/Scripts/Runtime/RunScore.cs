using System;

namespace RiftForge
{
    /// <summary>Pure end-of-run scoring: grade, points and personal-best comparison.</summary>
    public readonly struct RunScore
    {
        public readonly bool Victory;
        public readonly int WaveReached;
        public readonly float Seconds;
        public readonly int Kills;
        public readonly int BestStreak;
        public readonly int PerfectDodges;
        public readonly int DamageTaken;

        public RunScore(bool victory, int waveReached, float seconds, int kills, int bestStreak,
            int perfectDodges, int damageTaken)
        {
            Victory = victory;
            WaveReached = Math.Max(0, waveReached);
            Seconds = Math.Max(0f, seconds);
            Kills = Math.Max(0, kills);
            BestStreak = Math.Max(0, bestStreak);
            PerfectDodges = Math.Max(0, perfectDodges);
            DamageTaken = Math.Max(0, damageTaken);
        }

        /// <summary>Points reward clearing, speed, skill (streak and perfect dodges) and taking little damage.</summary>
        public int Points
        {
            get
            {
                int points = WaveReached * 1000 + Kills * 120 + BestStreak * 40 + PerfectDodges * 250;
                if (Victory)
                {
                    points += 3000;
                    points += Math.Max(0, (int)((240f - Seconds) * 10f));
                }
                points -= DamageTaken * 6;
                return Math.Max(0, points);
            }
        }

        public string Grade
        {
            get
            {
                if (!Victory) return WaveReached >= 3 ? "C" : "D";
                int p = Points;
                if (p >= 10500) return "S";
                if (p >= 9200) return "A";
                if (p >= 7600) return "B";
                return "C";
            }
        }

        public static int GradeRank(string grade)
        {
            switch (grade)
            {
                case "S": return 5;
                case "A": return 4;
                case "B": return 3;
                case "C": return 2;
                case "D": return 1;
                default: return 0;
            }
        }

        public bool Beats(int previousBestPoints) => Points > previousBestPoints;
    }
}
