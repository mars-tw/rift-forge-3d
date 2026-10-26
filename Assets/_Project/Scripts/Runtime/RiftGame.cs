using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RiftForge
{
    /// <summary>
    /// Drives a complete four-encounter offline run. Signature loop: read the red telegraph,
    /// dodge late for a Phase Counter, chain strikes into Forge charge, release a Forge Burst,
    /// then spend collected shards at the forge. World construction lives in RiftGame.World.cs.
    /// </summary>
    public sealed partial class RiftGame : MonoBehaviour
    {
        private sealed class Pickup
        {
            public GameObject Visual;
            public int Amount;
            public float Age;
        }

        private sealed class ShortEffect
        {
            public GameObject Visual;
            public float Remaining;
        }

        public const float AttackCooldownSeconds = 0.46f;
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        /// <summary>Tests may inject a store; batchmode defaults to memory so CI never writes PlayerPrefs.</summary>
        public static IRiftRecordStore RecordStoreOverride;
        public static RiftProgress ProgressOverride;
        public RiftProgress Progress { get; private set; }
        public RiftRoute SelectedRoute { get; private set; }
        public Vector3 LastDodgeDirection => dodgeDirection;
        private bool forgeCounted;
        public bool DailyRewardThisRun { get; private set; }

        public void SelectRoute(int delta)
        {
            if (run.Phase != RunPhase.Title) return;
            SelectedRoute = (RiftRoute)(((int)SelectedRoute + delta + 4) % 4);
            hud.Refresh();
        }
        public bool StartSelectedRun()
        {
            if (!Progress.IsUnlocked(SelectedRoute)) return false;
            StartRun(SelectedRoute); return true;
        }
        public void ReturnToTitle()
        {
            ClearEncounter(); paused = false; run.ReturnToTitle();
            playerController.enabled = false; moveSource?.SetApplicationPaused(true);
            Time.timeScale = 1f; hitStop = slowMotion = 0f;
            audioFx.SetMusicIntensity(0.35f); hud.Refresh();
        }

        private readonly List<RiftEnemy> enemies = new List<RiftEnemy>();
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<ShortEffect> effects = new List<ShortEffect>();
        private RiftRunState run;
        private CombatRhythm rhythm;
        private IRiftRecordStore records;
        private RiftHud hud;
        private RiftAudio audioFx;
        private RiftFx fx;
        private CameraRig rig;
        private Camera mainCamera;
        private CharacterController characterController;
        private PlayerController playerController;
        private TouchPlayerInputSource moveSource;
        private Vector3 facing = Vector3.forward;
        private Vector3 dodgeDirection;
        private float attackCooldown;
        private float dodgeCooldown;
        private float dodgeTime;
        private float invulnerableTime;
        private float windup;
        private StrikeKind pendingStrike;
        private bool strikePending;
        private float hitStop;
        private float slowMotion;
        private float hurtFlash;
        private float runSeconds;
        private int kills;
        private int damageTaken;
        private bool paused;
        private RunPhase lastPhase = RunPhase.Title;
        private int shardsCollectedThisWave;

        // Tutorial hints (first runs only, wave 1).
        private bool hintsEnabled;
        private bool finisherShown;
        private bool dodgeHintShown;
        private bool perfectHintShown;
        private bool burstHintShown;
        private string hint = "";
        private float hintTime;

        public RiftRunState Run => run;
        public CombatRhythm Rhythm => rhythm;
        public IRiftRecordStore Records => records;
        public int ActiveEnemyCount => enemies.Count;
        public IReadOnlyList<RiftEnemy> Enemies => enemies;
        public Vector3 PlayerPosition => player == null ? Vector3.zero : player.position;
        public bool CanSimulate => run != null && run.Phase == RunPhase.Combat && !paused;
        public bool IsPaused => paused;
        public float AttackCooldownRemaining => attackCooldown;
        public float DodgeCooldownRemaining => dodgeCooldown;
        public float DodgeCooldownTotal => run == null ? 1f : run.DodgeCooldown;
        public bool IsStrikePending => strikePending;
        public string CurrentHint => IsInitiation ? InitiationHint : hintTime > 0f ? hint : "";
        public RunScore? LastScore { get; private set; }
        public bool LastScoreIsBest { get; private set; }
        public float RunSeconds => runSeconds;
        public int Kills => kills;
        public int DamageTaken => damageTaken;
        public Material FlashMaterial => flashMaterial;
        public RiftAudio Audio => audioFx;
        public RiftFx Fx => fx;
        public RiftHud Hud => hud;
        public RiftEnemy Boss
        {
            get
            {
                foreach (RiftEnemy enemy in enemies) if (enemy != null && enemy.IsBoss) return enemy;
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnterMainScene()
        {
            if (SceneManager.GetActiveScene().path != MainScenePath || FindAnyObjectByType<RiftGame>() != null)
                return;
            new GameObject("Rift Forge Game").AddComponent<RiftGame>();
        }

        private void Awake()
        {
            run = new RiftRunState();
            checkpoints = CheckpointStoreOverride ?? (Application.isBatchMode ? (IRiftCheckpointStore)new MemoryCheckpointStore() : new LocalCheckpointStore());
            Progress = ProgressOverride ?? (Application.isBatchMode ? new RiftProgress() : RiftProgress.LoadLocal());
            rhythm = new CombatRhythm();
            records = RecordStoreOverride ?? (Application.isBatchMode
                ? (IRiftRecordStore)new MemoryRecordStore() : new PlayerPrefsRecordStore());
            CreateMaterials();
            CreateWorld();
            fx = new GameObject("Rift FX").AddComponent<RiftFx>();
            fx.transform.SetParent(transform, false);
            fx.Initialize(goldMaterial);
            audioFx = gameObject.AddComponent<RiftAudio>();
            audioFx.Initialize();
            audioFx.SetMusicIntensity(0.35f);
            hud = new GameObject("Rift HUD").AddComponent<RiftHud>();
            hud.Initialize(this, playerController);
            moveSource = hud.MoveSource;
            playerController.enabled = false;
            hud.Refresh();
        }

        private void Update()
        {
            if (run == null) return;
            float unscaled = Time.unscaledDeltaTime;
            UpdateTimeScale(unscaled);

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if (run.Phase == RunPhase.Combat &&
                ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.startButton.wasPressedThisFrame)))
                SetPaused(!paused);

            if (CanSimulate)
            {
                float dt = Time.deltaTime;
                runSeconds += dt;
                rhythm.Tick(dt);
                attackCooldown = Mathf.Max(0f, attackCooldown - dt);
                dodgeCooldown = Mathf.Max(0f, dodgeCooldown - dt);
                invulnerableTime = Mathf.Max(0f, invulnerableTime - dt);

                if (strikePending)
                {
                    windup -= dt;
                    if (windup <= 0f) ResolveStrike();
                }

                Vector2 input = moveSource != null ? moveSource.ReadMove() : Vector2.zero;
                if (input.sqrMagnitude > 0.03f)
                {
                    facing = new Vector3(input.x, 0f, input.y).normalized;
                    if (dodgeTime <= 0f && !strikePending)
                        player.rotation = Quaternion.Slerp(player.rotation,
                            Quaternion.LookRotation(facing), Mathf.Min(1f, 12f * dt));
                }

                if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame)) ||
                    (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
                    Attack();
                if ((keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame)) ||
                    (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
                    Dodge();

                if (dodgeTime > 0f)
                {
                    dodgeTime = Mathf.Max(0f, dodgeTime - dt);
                    characterController.Move(dodgeDirection * (13f * dt));
                    ClampPlayerToArena();
                    if (Time.frameCount % 2 == 0) fx.Glow(player.position, new Color(0.3f, 0.9f, 1f, 0.6f), 1, 0.2f, 0.9f);
                    if (dodgeTime == 0f) playerController.enabled = true;
                }

                for (int i = enemies.Count - 1; i >= 0; i--)
                    if (i < enemies.Count && enemies[i] != null) enemies[i].Tick(dt);
                UpdatePickups(dt);
                UpdateEffects(dt);
                if (hintTime > 0f) hintTime -= unscaled;
                if (!burstHintShown && rhythm.BurstReady)
                {
                    burstHintShown = true;
                    ShowHint("能量已滿！下一擊施放鍛造爆發", 3f, true);
                }
            }

            if (run.Phase != lastPhase) OnPhaseChanged(lastPhase, run.Phase);
            AnimateWorld(unscaled);
            AnimatePlayer(unscaled);
            UpdateCamera(unscaled);
            hud?.Refresh();
        }

        private void UpdateTimeScale(float unscaled)
        {
            hitStop = Mathf.Max(0f, hitStop - unscaled);
            slowMotion = Mathf.Max(0f, slowMotion - unscaled);
            float target = 1f;
            if (CanSimulate)
            {
                if (hitStop > 0f) target = 0.06f;
                else if (slowMotion > 0f) target = 0.3f;
            }
            Time.timeScale = target;
        }

        public void StartNewRun() => StartRun(RiftRoute.Campaign);

        private void StartRun(RiftRoute route, bool initiation = false)
        {
            ClearEncounter();
            Initiation = initiation ? InitiationStep.Dodge : InitiationStep.None;
            if (!initiation) checkpoints.Write("");
            run.StartRun(route);
            SelectedRoute = route;
            forgeCounted = false;
            DailyRewardThisRun = false;
            rhythm.Reset();
            if (!initiation) records.IncrementRunsStarted();
            hintsEnabled = records.RunsStarted <= 3;
            finisherShown = dodgeHintShown = perfectHintShown = burstHintShown = false;
            hintTime = 0f;
            LastScore = null;
            LastScoreIsBest = false;
            runSeconds = 0f;
            kills = damageTaken = 0;
            paused = false;
            hitStop = slowMotion = 0f;
            Time.timeScale = 1f;
            facing = Vector3.forward;
            player.position = new Vector3(0f, 1f, -2.4f);
            player.rotation = Quaternion.identity;
            attackCooldown = dodgeCooldown = dodgeTime = invulnerableTime = windup = 0f;
            strikePending = false;
            playerController.enabled = true;
            moveSource?.SetApplicationPaused(false);
            lastPhase = RunPhase.Combat;
            audioFx.SetMusicIntensity(1f);
            SpawnWave();
            SaveCheckpoint();
            if (hintsEnabled) ShowHint("TAP STRIKE 3x IN A ROW FOR A FORGE FINISHER", 6f);
            hud.Refresh();
        }

        public void ContinueFromForge()
        {
            if (!run.BeginNextWave()) return;
            forgeCounted = false;
            player.position = new Vector3(0f, 1f, -2.4f);
            playerController.enabled = true;
            moveSource?.SetApplicationPaused(false);
            lastPhase = RunPhase.Combat;
            audioFx.SetMusicIntensity(1f);
            audioFx.Play(RiftSfx.UiTap);
            SpawnWave();
            SaveCheckpoint();
            hud.Refresh();
        }

        /// <summary>Cycle the optional contract for the next wave. Contracts never replace forge purchases.</summary>
        public void CycleContract()
        {
            if (run.Phase != RunPhase.Forge || IsInitiation) return;
            ForgeContract next = (ForgeContract)(((int)run.NextContract + 1) % 4);
            if (!run.SelectContract(next)) return;
            audioFx.Play(RiftSfx.UiTap, 0.75f, 1.05f);
            SaveCheckpoint();
            hud.Refresh();
        }

        public bool Purchase(ForgeUpgrade upgrade)
        {
            bool bought = run.BuyUpgrade(upgrade);
            if (bought)
            {
                if (!forgeCounted) { forgeCounted = true; if (!IsInitiation) DailyRewardThisRun |= Progress.RegisterForge(); }
                Color color = upgrade == ForgeUpgrade.Blade ? Palette.Cyan : upgrade == ForgeUpgrade.Vitality ? Palette.Gold : Palette.Violet;
                fx.Sparks(player.position + Vector3.up * 0.4f, color, 26, 6f, 0.16f);
                fx.Glow(player.position + Vector3.up * 0.6f, color, 10, 0.5f, 1.1f);
                fx.Shockwave(player.position, 0.4f, 2.4f, 0.4f);
                audioFx.Play(RiftSfx.Forge, 1f, 0.95f + 0.08f * run.UpgradeLevel(upgrade));
                rig?.AddTrauma(0.2f);
                RefreshPlayerUpgradeLook();
                SaveCheckpoint();
                hud.Refresh();
            }
            else audioFx.Play(RiftSfx.UiTap, 0.6f, 0.7f);
            return bought;
        }

        public void SetPaused(bool value)
        {
            if (run.Phase != RunPhase.Combat) return;
            paused = value;
            if (value) Time.timeScale = 1f;
            playerController.enabled = !value && dodgeTime <= 0f;
            moveSource?.SetApplicationPaused(value);
            audioFx.SetMusicIntensity(value ? 0.25f : 1f);
            hud.Refresh();
        }

        public void Attack()
        {
            if (!CanSimulate || attackCooldown > 0f || dodgeTime > 0f) return;
            attackCooldown = AttackCooldownSeconds;
            StrikeKind kind = rhythm.NextStrike();
            pendingStrike = kind;
            strikePending = true;
            windup = kind == StrikeKind.Burst ? 0.12f : kind == StrikeKind.Finisher ? 0.08f : 0.06f;
            BeginSwingAnimation(kind, windup);

            RiftEnemy closest = null;
            float closestDistance = (CombatRhythm.Reach(kind) + 0.6f) * (CombatRhythm.Reach(kind) + 0.6f);
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;
                Vector3 delta = enemies[i].transform.position - player.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < closestDistance)
                {
                    closestDistance = delta.sqrMagnitude;
                    closest = enemies[i];
                }
            }
            if (closest != null)
            {
                Vector3 toward = closest.transform.position - player.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.0001f)
                {
                    facing = toward.normalized;
                    player.rotation = Quaternion.LookRotation(facing);
                }
            }
            float pitch = kind == StrikeKind.Cross ? 1.12f : kind == StrikeKind.Finisher ? 0.85f : 1f;
            audioFx.Play(RiftSfx.Swing, 0.8f, pitch);
            if (kind == StrikeKind.Finisher && hintsEnabled && !finisherShown)
            {
                finisherShown = true;
                hintTime = 0f;
            }
        }

        /// <summary>Impact frame: damage, knockback, hit-stop and feedback only happen here.</summary>
        private void ResolveStrike()
        {
            strikePending = false;
            windup = 0f;
            StrikeKind kind = pendingStrike;
            float reach = CombatRhythm.Reach(kind);
            float arc = CombatRhythm.ArcDot(kind);
            int damage = Mathf.RoundToInt(run.AttackDamage * CombatRhythm.DamageMultiplier(kind));
            float pushForce = kind == StrikeKind.Burst ? 11f : kind == StrikeKind.Finisher ? 9f : kind == StrikeKind.Counter ? 7f : 4f;
            bool stagger = kind == StrikeKind.Finisher || kind == StrikeKind.Counter || kind == StrikeKind.Burst;
            CreateSlash(kind);
            int hits = 0;
            int actualHits = 0;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count) continue;
                RiftEnemy enemy = enemies[i];
                if (enemy == null) continue;
                Vector3 delta = enemy.transform.position - player.position;
                delta.y = 0f;
                float hitReach = reach + (enemy.IsBoss ? 0.6f : 0f);
                if (delta.sqrMagnitude > hitReach * hitReach) continue;
                if (delta.sqrMagnitude >= 0.01f && Vector3.Dot(delta.normalized, facing) < arc) continue;
                Vector3 push = (delta.sqrMagnitude > 0.01f ? delta.normalized : facing) * pushForce;
                Vector3 numberAt = enemy.transform.position + Vector3.up * (enemy.IsBoss ? 3.2f : 2.1f);
                hits++;
                int healthBefore = enemy.Health;
                enemy.TakeHit(damage, push, stagger);
                if (enemy.Health < healthBefore) actualHits++;
                hud.ShowDamageNumber(numberAt, damage, kind);
                fx.Sparks(enemy != null ? enemy.transform.position + Vector3.up * 1.1f : numberAt,
                    kind == StrikeKind.Counter ? Palette.Pink : kind == StrikeKind.Burst || kind == StrikeKind.Finisher ? Palette.Gold : Palette.Cyan,
                    kind == StrikeKind.Slash || kind == StrikeKind.Cross ? 10 : 20, 7f);
            }
            rhythm.RegisterImpact(kind, hits);
            if (kind == StrikeKind.Counter && hits > 0 && Initiation == InitiationStep.Counter) Initiation = InitiationStep.Burst;
            int actualImprintHits = kind == StrikeKind.Burst ? DetonateImprints() : 0;
            if (!IsInitiation) run.RegisterContractImpact(kind, actualHits, actualImprintHits);
            if (kind == StrikeKind.Burst && !IsInitiation) Progress.RegisterBurst(hits);

            if (kind == StrikeKind.Burst)
            {
                fx.Shockwave(player.position, 0.6f, reach, 0.35f);
                fx.Glow(player.position + Vector3.up * 0.5f, Palette.Gold, 18, 1.2f, 1.4f);
                audioFx.Play(RiftSfx.Burst);
                rig?.AddTrauma(0.65f);
                rig?.KickFieldOfView(6f);
                hitStop = Mathf.Max(hitStop, 0.12f);
            }
            if (hits == 0) return;
            switch (kind)
            {
                case StrikeKind.Finisher:
                    audioFx.Play(RiftSfx.Finisher);
                    hitStop = Mathf.Max(hitStop, 0.085f);
                    rig?.AddTrauma(0.38f);
                    rig?.KickFieldOfView(3f);
                    break;
                case StrikeKind.Counter:
                    audioFx.Play(RiftSfx.Finisher, 1f, 1.25f);
                    hud.ShowPopup(player.position + Vector3.up * 2.4f, "反擊！", Palette.Pink);
                    hitStop = Mathf.Max(hitStop, 0.09f);
                    rig?.AddTrauma(0.4f);
                    rig?.KickFieldOfView(4f);
                    break;
                case StrikeKind.Burst:
                    break;
                default:
                    audioFx.Play(RiftSfx.Hit, 0.9f, Random.Range(0.92f, 1.08f));
                    hitStop = Mathf.Max(hitStop, 0.045f);
                    rig?.AddTrauma(0.16f);
                    break;
            }
        }

        public void Dodge()
        {
            if (!CanSimulate || dodgeCooldown > 0f || dodgeTime > 0f) return;
            // Dodging cancels a strike that has not reached its impact frame.
            strikePending = false;
            windup = 0f;
            dodgeCooldown = run.DodgeCooldown;
            dodgeTime = 0.25f;
            invulnerableTime = 0.4f;
            Vector2 dodgeInput = moveSource != null ? moveSource.ReadMove() : Vector2.zero;
            dodgeDirection = dodgeInput.sqrMagnitude > 0.01f ? new Vector3(dodgeInput.x, 0f, dodgeInput.y).normalized :
                facing.sqrMagnitude > 0.01f ? facing : Vector3.forward;
            playerController.enabled = false;
            fx.Glow(player.position, new Color(0.3f, 0.9f, 1f, 0.8f), 6, 0.3f, 1f);
            audioFx.Play(RiftSfx.Dodge);

            bool perfect = false;
            bool insideAnyThreat = false;
            foreach (RiftEnemy enemy in enemies)
            {
                if (enemy == null || !enemy.ThreatContains(player.position)) continue;
                insideAnyThreat = true;
                if (CombatRhythm.IsPerfectDodge(true, enemy.SecondsUntilStrike)) perfect = true;
            }
            if (perfect)
            {
                rhythm.RegisterPerfectDodge();
                AddImprint();
                if (!IsInitiation) Progress.RegisterPerfects(rhythm.PerfectDodges);
                invulnerableTime = 0.6f;
                slowMotion = 0.4f;
                audioFx.Play(RiftSfx.Perfect);
                hud.ShowPopup(player.position + Vector3.up * 2.4f, "完美閃避", Palette.Pink);
                fx.Glow(player.position + Vector3.up * 0.5f, Palette.Pink, 14, 0.7f, 1.2f);
                fx.Shockwave(player.position, 0.3f, 1.8f, 0.3f);
                rig?.KickFieldOfView(3f);
                PulseChromatic(0.5f);
                if (hintsEnabled && !perfectHintShown)
                {
                    perfectHintShown = true;
                    ShowHint("相位反擊！下一擊造成兩倍傷害", 2.6f, true);
                }
            }
            else if (insideAnyThreat && hintsEnabled && !perfectHintShown && run.Wave == 1)
            {
                perfectHintShown = true;
                ShowHint("閃得好！落點前再閃，可觸發完美反擊", 3.2f, true);
            }
        }

        public bool TryDamagePlayer(int damage) => TryDamagePlayer(damage, PlayerPosition);

        public bool TryDamagePlayer(int damage, Vector3 source)
        {
            if (!CanSimulate || IsInitiation || invulnerableTime > 0f || !run.DamagePlayer(damage)) return false;
            invulnerableTime = 0.55f;
            damageTaken += damage;
            rhythm.RegisterPlayerHurt();
            hurtFlash = 0.18f;
            audioFx.Play(RiftSfx.Hurt);
            rig?.AddTrauma(0.45f);
            hud.PulseHurt();
            PulseChromatic(0.8f);
            fx.Sparks(player.position + Vector3.up * 0.4f, Palette.Danger, 14, 6f);
            hud.ShowPopup(player.position + Vector3.up * 2.2f, "-" + damage, Palette.Danger);
            Vector3 away = player.position - source;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f && run.Phase == RunPhase.Combat)
            {
                characterController.Move(away.normalized * 0.45f);
                ClampPlayerToArena();
            }
            if (run.Phase == RunPhase.Defeat)
            {
                playerController.enabled = false;
                moveSource?.SetApplicationPaused(true);
            }
            return true;
        }

        public void EnemyKilled(RiftEnemy enemy)
        {
            if (!CanSimulate || !enemies.Remove(enemy)) return;
            int amount = run.EnemyDefeated();
            kills++;
            Vector3 at = enemy.transform.position;
            CreatePickup(at, amount);
            Color color = enemy.IsBoss ? Palette.Gold : enemy.Kind == EnemyKind.Lancer ? Palette.Pink : Palette.Violet;
            fx.Sparks(at + Vector3.up * 1f, color, enemy.IsBoss ? 70 : 28, enemy.IsBoss ? 12f : 8f, enemy.IsBoss ? 0.22f : 0.14f);
            fx.Glow(at + Vector3.up * 0.9f, color, enemy.IsBoss ? 30 : 10, enemy.IsBoss ? 1.4f : 0.6f, enemy.IsBoss ? 2f : 1.1f);
            fx.Shockwave(at, 0.3f, enemy.IsBoss ? 5f : 2f, enemy.IsBoss ? 0.6f : 0.3f);
            audioFx.Play(RiftSfx.Kill, enemy.IsBoss ? 1f : 0.8f, enemy.IsBoss ? 0.6f : Random.Range(0.95f, 1.1f));
            hitStop = Mathf.Max(hitStop, enemy.IsBoss ? 0.2f : 0.06f);
            rig?.AddTrauma(enemy.IsBoss ? 0.9f : 0.22f);
            rig?.KickFieldOfView(enemy.IsBoss ? 8f : 1.5f);
            if (run.EnemiesRemaining == 0)
            {
                slowMotion = enemy.IsBoss ? 1.1f : 0.45f;
                hud.ShowBanner(enemy.IsBoss ? "守門者已倒下" : "裂隙已清除",
                    enemy.IsBoss ? "收取核心碎片" : "收集碎片，準備鍛造", Palette.Gold);
            }
            enemy.PlayDeath();
        }

        public void SpawnHitFlash(Vector3 position)
        {
            fx.Glow(position, Palette.White, 2, 0.1f, 0.9f);
        }

        /// <summary>Legacy impact helper kept for callers outside the strike pipeline.</summary>
        public void SpawnImpact(Vector3 position, bool large)
        {
            fx.Sparks(position + Vector3.up * 0.2f, large ? Palette.Gold : Palette.Cyan, large ? 24 : 10, large ? 8f : 5f);
            fx.Shockwave(position, 0.2f, large ? 2.2f : 1.2f, 0.3f);
        }

        public void EnemyEmerging(RiftEnemy enemy)
        {
            Vector3 at = enemy.transform.position;
            at.y = 0.1f;
            fx.Glow(at, enemy.IsBoss ? Palette.Danger : Palette.Violet, enemy.IsBoss ? 24 : 8, enemy.IsBoss ? 1.2f : 0.5f, 1.2f);
            fx.Shockwave(at, 0.2f, enemy.IsBoss ? 3.5f : 1.4f, 0.45f);
            audioFx.Play(RiftSfx.Slam, enemy.IsBoss ? 0.9f : 0.35f, enemy.IsBoss ? 0.7f : 1.3f);
            if (enemy.IsBoss) rig?.AddTrauma(0.6f);
        }

        public void EnemyTelegraphed(RiftEnemy enemy)
        {
            audioFx.Play(RiftSfx.Telegraph, enemy.IsBoss ? 0.7f : 0.4f, enemy.IsBoss ? 0.8f : 1f);
            if (hintsEnabled && !dodgeHintShown && run.Wave == 1 && enemy.ThreatContains(player.position))
            {
                dodgeHintShown = true;
                ShowHint("紅圈出現！落點前按閃避離開", 2.2f, true);
            }
        }

        public void EnemyStrikeLanded(RiftEnemy enemy, Vector3 at, bool large)
        {
            if (enemy.Kind == EnemyKind.Lancer)
            {
                fx.Sparks(at + Vector3.up * 0.3f, Palette.Pink, 8, 5f);
                audioFx.Play(RiftSfx.Dodge, 0.7f, 0.6f);
                return;
            }
            fx.Sparks(at + Vector3.up * 0.15f, Palette.Danger, large ? 30 : 12, large ? 9f : 5f);
            fx.Shockwave(at, 0.3f, large ? 2.6f : 1.5f, 0.3f);
            audioFx.Play(RiftSfx.Slam, large ? 1f : 0.55f, large ? 0.8f : 1.15f);
            rig?.AddTrauma(large ? 0.42f : 0.12f);
        }

        public void BossEnraged(RiftEnemy boss)
        {
            hud.ShowBanner("THE WARDEN ENRAGES", "三連重砸，留意每一道紅圈", Palette.Danger);
            audioFx.Play(RiftSfx.Slam, 1f, 0.55f);
            rig?.AddTrauma(0.7f);
            fx.Glow(boss.transform.position + Vector3.up * 2f, Palette.Danger, 24, 1.4f, 1.8f);
            SetBossEnragedLook(boss);
        }

        private void ShowHint(string text, float seconds, bool force = false)
        {
            if (!force && !hintsEnabled) return;
            hint = text;
            hintTime = seconds;
        }

        private void OnPhaseChanged(RunPhase from, RunPhase to)
        {
            lastPhase = to;
            Time.timeScale = 1f;
            hitStop = slowMotion = 0f;
            strikePending = false;
            switch (to)
            {
                case RunPhase.Forge:
                    SaveCheckpoint();
                    audioFx.SetMusicIntensity(0.35f);
                    audioFx.Play(RiftSfx.Forge, 0.7f, 0.8f);
                    break;
                case RunPhase.Victory:
                case RunPhase.Defeat:
                    bool victory = to == RunPhase.Victory;
                    if (!IsInitiation) checkpoints.Write("");
                    var score = new RunScore(victory, run.Wave, runSeconds, kills, rhythm.BestHitStreak,
                        rhythm.PerfectDodges, damageTaken);
                    LastScore = score;
                    LastScoreIsBest = !IsInitiation && records.Submit(score);
                    if (victory && !IsInitiation) Progress.RegisterVictory();
                    audioFx.Play(victory ? RiftSfx.Victory : RiftSfx.Defeat);
                    audioFx.SetMusicIntensity(0.3f);
                    break;
            }
        }

        private void SpawnWave()
        {
            int count = run.CurrentEnemyCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count + run.Wave * 0.31f;
                float radius = run.IsBossWave ? 5.1f : 5.5f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius + 0.7f);
                EnemyKind kind = run.CurrentKind(i);
                float delay = run.IsBossWave ? 0.9f : 0.35f + 0.45f * i;
                CreateEnemy(position, kind, delay);
                if (run.Route == RiftRoute.Echo && kind == EnemyKind.Stalker)
                    enemies[enemies.Count - 1].EnableEcho();
            }
            shardsCollectedThisWave = 0;
            if (run.Route != RiftRoute.Campaign)
                hud.ShowBanner($"{RiftRoutes.Name(run.Route)}　{run.Wave}/3", RiftRoutes.Description(run.Route), Palette.Cyan);
            else if (run.IsBossWave)
                hud.ShowBanner("最後裂隙", "守門者甦醒", Palette.Danger);
            else
            {
                int lancers = run.Wave - 1;
                hud.ShowBanner($"RIFT {run.Wave}",
                    lancers > 0 ? $"{count} 隻敵人，含 {lancers} 隻直線衝鋒者" : $"{count} 隻追擊者",
                    Palette.Cyan);
            }
            audioFx.Play(RiftSfx.WaveStart, 0.8f, run.IsBossWave ? 0.75f : 1f);
            rig?.AddTrauma(run.IsBossWave ? 0.3f : 0.1f);
        }

        private void CreatePickup(Vector3 position, int amount)
        {
            position.y = Mathf.Max(0f, position.y);
            GameObject shard = MakePrimitive(PrimitiveType.Cube, "Forge Shard", worldRoot,
                position + Vector3.up * 0.75f, Vector3.one * 0.38f, goldMaterial, true);
            shard.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            NoShadow(shard);
            pickups.Add(new Pickup { Visual = shard, Amount = amount });
        }

        private void UpdatePickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = pickups[i];
                pickup.Age += dt;
                if (pickup.Visual == null) { pickups.RemoveAt(i); continue; }
                pickup.Visual.transform.Rotate(0f, 220f * dt, 0f, Space.World);
                Vector3 destination = player.position + Vector3.up * 0.1f;
                float distance = Vector3.Distance(pickup.Visual.transform.position, destination);
                if (pickup.Age > 0.4f || distance < 1.7f)
                    pickup.Visual.transform.position = Vector3.MoveTowards(pickup.Visual.transform.position,
                        destination, (pickup.Age > 2f ? 15f : 5f + pickup.Age * 6f) * dt);
                if (distance > 0.7f && pickup.Age < 4f) continue;
                Destroy(pickup.Visual);
                pickups.RemoveAt(i);
                shardsCollectedThisWave++;
                audioFx.Play(RiftSfx.Pickup, 0.7f, 1f + 0.06f * Mathf.Min(8, shardsCollectedThisWave));
                fx.Sparks(player.position + Vector3.up * 0.8f, Palette.Gold, 8, 3.5f, 0.1f);
                hud.ShowPopup(player.position + Vector3.up * 2f, "+" + pickup.Amount, Palette.Gold);
                hud.PulseShards();
                run.CollectMaterial(pickup.Amount);
                if (run.Phase != RunPhase.Combat)
                {
                    playerController.enabled = false;
                    moveSource?.SetApplicationPaused(true);
                }
            }
        }

        private void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ShortEffect effect = effects[i];
                effect.Remaining -= dt;
                if (effect.Visual != null)
                    effect.Visual.transform.localScale *= Mathf.Max(0.0f, 1f - dt * 4f);
                if (effect.Remaining > 0f) continue;
                if (effect.Visual != null) Destroy(effect.Visual);
                effects.RemoveAt(i);
            }
        }

        private void ClearEncounter()
        {
            Imprints.Clear(); RebuildImprintVisuals();
            foreach (RiftEnemy enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
            foreach (Pickup pickup in pickups) if (pickup.Visual != null) Destroy(pickup.Visual);
            foreach (ShortEffect effect in effects) if (effect.Visual != null) Destroy(effect.Visual);
            enemies.Clear();
            pickups.Clear();
            effects.Clear();
            fx?.Clear();
        }

        private void ClampPlayerToArena()
        {
            Vector2 horizontal = new Vector2(player.position.x, player.position.z);
            if (horizontal.magnitude <= ArenaRadius) return;
            Vector2 bounded = horizontal.normalized * ArenaRadius;
            characterController.Move(new Vector3(bounded.x - horizontal.x, 0f, bounded.y - horizontal.y));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && run != null && run.Phase == RunPhase.Combat) SetPaused(true);
        }

        private void OnApplicationPause(bool applicationPaused)
        {
            if (applicationPaused && run != null && run.Phase == RunPhase.Combat) SetPaused(true);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            ClearEncounter();
            DestroyWorldResources();
        }
    }
}
