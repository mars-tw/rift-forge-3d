using System;
using UnityEngine;

namespace RiftForge
{
    public interface IRiftCheckpointStore { string Read(); void Write(string value); }
    public sealed class MemoryCheckpointStore : IRiftCheckpointStore
    {
        private string json = "";
        public string Read() => json;
        public void Write(string value) => json = value;
    }
    public sealed class LocalCheckpointStore : IRiftCheckpointStore
    {
        private const string Key = "riftforge.checkpoint.v1";
        public string Read() => PlayerPrefs.GetString(Key, "");
        public void Write(string value) { PlayerPrefs.SetString(Key, value); PlayerPrefs.Save(); }
    }
    [Serializable]
    public sealed class RiftCheckpoint
    {
        public int schema = 1, route, wave, health, material, blade, vitality, dodge, kills, damage;
        public float seconds;
        public bool forge, forgeCounted;
        public int contractSchema, nextContract, activeContract, contractProgress, contractReward;
        public bool IsValid => schema == 1 && route >= 0 && route <= 3 && wave >= 1 && wave <= RiftRoutes.Waves((RiftRoute)route)
            && (!forge || wave < RiftRoutes.Waves((RiftRoute)route)) && blade >= 0 && blade <= 3 && vitality >= 0 && vitality <= 3
            && dodge >= 0 && dodge <= 3 && health > 0 && health <= 100 + vitality * 25 && material >= 0 && material <= 1000
            && kills >= 0 && kills <= 100 && damage >= 0 && damage <= 10000 && seconds >= 0 && seconds <= 86400 && !float.IsNaN(seconds)
            && ContractExtensionIsValid;
        private bool ContractExtensionIsValid
        {
            get
            {
                if (contractSchema == 0) return nextContract == 0 && activeContract == 0 && contractProgress == 0 && contractReward == 0;
                if (contractSchema != 1 || nextContract < 0 || nextContract > 3 || activeContract < 0 || activeContract > 3) return false;
                var active = (ForgeContract)activeContract;
                int target = RiftRunState.ContractTargetFor(active), reward = RiftRunState.ContractRewardFor(active);
                if (contractProgress < 0 || contractProgress > target || contractReward < 0) return false;
                if (contractReward != 0 && (active == ForgeContract.None || contractProgress != target || contractReward != reward)) return false;
                if (!forge && (nextContract != 0 || contractProgress != 0 || contractReward != 0)) return false;
                return true;
            }
        }
        public static RiftCheckpoint Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { var value = JsonUtility.FromJson<RiftCheckpoint>(json); return value != null && value.IsValid ? value : null; }
            catch (ArgumentException) { return null; }
        }
    }
    public sealed partial class RiftGame
    {
        public static IRiftCheckpointStore CheckpointStoreOverride;
        private IRiftCheckpointStore checkpoints;
        public bool HasCheckpoint => RiftCheckpoint.Parse(checkpoints?.Read()) != null;
        private void SaveCheckpoint()
        {
            if (IsInitiation || run.Phase != RunPhase.Combat && run.Phase != RunPhase.Forge) return;
            var c = run.Checkpoint(); c.seconds = runSeconds; c.kills = kills; c.damage = damageTaken; c.forgeCounted = forgeCounted;
            checkpoints.Write(JsonUtility.ToJson(c));
        }
        public bool ResumeCheckpoint()
        {
            if (run.Phase != RunPhase.Title) return false;
            var c = RiftCheckpoint.Parse(checkpoints.Read()); if (c == null) return false;
            ClearEncounter(); Initiation = InitiationStep.None; run.Restore(c); rhythm.Reset();
            SelectedRoute = run.Route; runSeconds = c.seconds; kills = c.kills; damageTaken = c.damage;
            forgeCounted = c.forgeCounted; DailyRewardThisRun = false; paused = false;
            attackCooldown = dodgeCooldown = dodgeTime = invulnerableTime = windup = hitStop = slowMotion = 0;
            strikePending = false; Time.timeScale = 1; facing = Vector3.forward;
            player.position = new Vector3(0, 1, -2.4f); player.rotation = Quaternion.identity;
            RefreshPlayerUpgradeLook(); lastPhase = run.Phase;
            playerController.enabled = run.Phase == RunPhase.Combat;
            moveSource?.SetApplicationPaused(run.Phase != RunPhase.Combat);
            audioFx.SetMusicIntensity(run.Phase == RunPhase.Combat ? 1 : .35f);
            if (run.Phase == RunPhase.Combat) SpawnWave(); hud.Refresh(); return true;
        }
    }
}
