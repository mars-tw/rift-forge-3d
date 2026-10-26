using UnityEngine;

namespace RiftForge
{
    /// <summary>Local personal-best storage. Offline only; nothing leaves the device.</summary>
    public interface IRiftRecordStore
    {
        int BestPoints { get; }
        string BestGrade { get; }
        int BestWave { get; }
        int RunsStarted { get; }
        void IncrementRunsStarted();
        /// <summary>Stores the run if it beats the best points; returns true when it is a new best.</summary>
        bool Submit(RunScore score);
    }

    public sealed class MemoryRecordStore : IRiftRecordStore
    {
        public int BestPoints { get; private set; }
        public string BestGrade { get; private set; } = "";
        public int BestWave { get; private set; }
        public int RunsStarted { get; private set; }

        public void IncrementRunsStarted() => RunsStarted++;

        public bool Submit(RunScore score)
        {
            if (score.WaveReached > BestWave) BestWave = score.WaveReached;
            if (!score.Beats(BestPoints)) return false;
            BestPoints = score.Points;
            BestGrade = score.Grade;
            return true;
        }
    }

    public sealed class PlayerPrefsRecordStore : IRiftRecordStore
    {
        private const string PointsKey = "riftforge.best.points";
        private const string GradeKey = "riftforge.best.grade";
        private const string WaveKey = "riftforge.best.wave";
        private const string RunsKey = "riftforge.runs";

        public int BestPoints => PlayerPrefs.GetInt(PointsKey, 0);
        public string BestGrade => PlayerPrefs.GetString(GradeKey, "");
        public int BestWave => PlayerPrefs.GetInt(WaveKey, 0);
        public int RunsStarted => PlayerPrefs.GetInt(RunsKey, 0);

        public void IncrementRunsStarted()
        {
            PlayerPrefs.SetInt(RunsKey, RunsStarted + 1);
            PlayerPrefs.Save();
        }

        public bool Submit(RunScore score)
        {
            if (score.WaveReached > BestWave) PlayerPrefs.SetInt(WaveKey, score.WaveReached);
            bool best = score.Beats(BestPoints);
            if (best)
            {
                PlayerPrefs.SetInt(PointsKey, score.Points);
                PlayerPrefs.SetString(GradeKey, score.Grade);
            }
            PlayerPrefs.Save();
            return best;
        }
    }
}
