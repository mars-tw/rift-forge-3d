using System.Collections.Generic;
using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// One rift creature. Stalkers slam a ground ring, Lancers charge along a telegraphed line,
    /// and the Warden slams a large ring and enrages into a three-slam chain at half health.
    /// Every attack is telegraphed; damage only lands on the strike frame.
    /// </summary>
    public sealed class RiftEnemy : MonoBehaviour
    {
        private enum Tactic { Emerge, Pursue, Telegraph, Charge, Recover }

        private RiftGame game;
        private GameObject threat;
        private Tactic tactic;
        private Vector3 markedPoint;
        private Vector3 chargeDirection;
        private Vector3 chargeStart;
        private Vector3 knockback;
        private float timer;
        private float telegraphLength;
        private float telegraphTotal = 1f;
        private Transform threatProgress;
        private float emergeDelay;
        private float emergeTime;
        private float flashTime;
        private float punch;
        private float walkPhase;
        private int health;
        private int maxHealth;
        private int strikeDamage;
        private float strikeRadius;
        private float moveSpeed;
        private float attackReach;
        private int chainRemaining;
        private bool chargeHit;
        private bool dead;
        private Transform body;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform healthFill;
        private Transform healthBar;
        private Renderer[] renderers;
        private Material[] baseMaterials;
        private RiftCharacter character;
        private void Update() { if (dead && character != null) character.Pose("Death", Time.deltaTime, 1.8f, false); }
        public void PlayDeath() { if (character != null) character.Pose("Death", 0, 1.8f, false); Destroy(gameObject, 1.9f); }

        public bool EchoSlam { get; private set; }
        public void EnableEcho() => EchoSlam = Kind == EnemyKind.Stalker;
        public bool IsBoss => Kind == EnemyKind.Warden;
        public EnemyKind Kind { get; private set; }
        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool Enraged { get; private set; }
        public bool IsEmerging => tactic == Tactic.Emerge;
        public bool IsTelegraphing => tactic == Tactic.Telegraph;
        /// <summary>Seconds until the telegraphed strike lands; negative when not telegraphing.</summary>
        public float SecondsUntilStrike => tactic == Tactic.Telegraph ? timer : -1f;
        public void ConfigureInitiationPartner() { health = maxHealth = 5000; moveSpeed = 2f; }

        public void Configure(RiftGame owner, int wave, bool boss)
        {
            Configure(owner, wave, boss ? EnemyKind.Warden : EnemyKind.Stalker, 0f);
        }

        public void Configure(RiftGame owner, int wave, EnemyKind kind, float emergeAfter)
        {
            game = owner;
            Kind = kind;
            switch (kind)
            {
                case EnemyKind.Warden:
                    health = 320; strikeDamage = 28; strikeRadius = 2.15f; attackReach = 3.2f; moveSpeed = 2.3f;
                    telegraphLength = 1.25f;
                    break;
                case EnemyKind.Lancer:
                    health = 40 + (wave - 1) * 14; strikeDamage = 15 + (wave - 1) * 2; strikeRadius = 0.95f;
                    attackReach = 6.2f; moveSpeed = 2.2f + 0.15f * wave; telegraphLength = 0.9f;
                    break;
                default:
                    health = 52 + (wave - 1) * 18; strikeDamage = 13 + (wave - 1) * 3; strikeRadius = 1.25f;
                    attackReach = 2.0f; moveSpeed = 2.5f + 0.2f * wave;
                    // Wave 1 is the teaching wave: a slightly longer ring warning.
                    telegraphLength = wave == 1 ? 0.95f : 0.82f;
                    break;
            }
            maxHealth = health;
            emergeDelay = Mathf.Max(0f, emergeAfter);
            emergeTime = 0f;
            tactic = Tactic.Emerge;
            timer = 0f;
            threat = kind == EnemyKind.Lancer ? owner.CreateThreatLine(7f, strikeRadius) :
                owner.CreateThreatRing(strikeRadius, kind == EnemyKind.Warden);
            threat.SetActive(false);
            threatProgress = threat.transform.Find("Progress");
            CacheVisuals();
            ApplyEmergePose(0f);
        }

        private void CacheVisuals()
        {
            character = GetComponentInChildren<RiftCharacter>();
            body = transform.Find("Body");
            leftLeg = transform.Find("Leg L");
            rightLeg = transform.Find("Leg R");
            healthBar = transform.Find("Health Bar");
            healthFill = healthBar != null ? healthBar.Find("Fill") : null;
            if (healthBar != null) healthBar.gameObject.SetActive(false);
            var list = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            if (healthBar != null) list.RemoveAll(r => r.transform.IsChildOf(healthBar));
            renderers = list.ToArray();
            baseMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseMaterials[i] = renderers[i].sharedMaterial;
        }

        /// <summary>True when <paramref name="point"/> is inside the active telegraphed strike zone.</summary>
        public bool ThreatContains(Vector3 point)
        {
            if (tactic != Tactic.Telegraph) return false;
            point.y = 0f;
            if (Kind == EnemyKind.Lancer)
            {
                Vector3 start = chargeStart; start.y = 0f;
                Vector3 end = start + chargeDirection * 7f;
                return DistanceToSegment(point, start, end) <= strikeRadius;
            }
            Vector3 offset = point - new Vector3(markedPoint.x, 0f, markedPoint.z);
            return offset.sqrMagnitude <= strikeRadius * strikeRadius;
        }

        public void Tick(float deltaTime)
        {
            if (dead || game == null || !game.CanSimulate) return;
            AnimateFeedback(deltaTime);
            Vector3 delta = game.PlayerPosition - transform.position;
            delta.y = 0f;

            if (knockback.sqrMagnitude > 0.0004f)
            {
                Vector3 pushed = transform.position + knockback * deltaTime;
                transform.position = ClampToArena(pushed);
                knockback = Vector3.MoveTowards(knockback, Vector3.zero, 22f * deltaTime);
            }

            switch (tactic)
            {
                case Tactic.Emerge:
                    emergeTime += deltaTime;
                    float t = Mathf.Clamp01((emergeTime - emergeDelay) / 0.5f);
                    ApplyEmergePose(t);
                    if (delta.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(delta.normalized);
                    if (t >= 1f) tactic = Tactic.Pursue;
                    break;
                case Tactic.Pursue:
                    if (delta.sqrMagnitude <= attackReach * attackReach) BeginTelegraph(delta);
                    else
                    {
                        Vector3 direction = delta.normalized;
                        transform.position += direction * (moveSpeed * deltaTime);
                        walkPhase += deltaTime * moveSpeed * 3.2f;
                        Face(direction, deltaTime * 8f);
                    }
                    break;
                case Tactic.Telegraph:
                    timer -= deltaTime;
                    if (threatProgress != null)
                    {
                        float p = Mathf.Clamp01(1f - timer / Mathf.Max(0.01f, telegraphTotal));
                        threatProgress.localScale = Kind == EnemyKind.Lancer ? new Vector3(1f, 1f, p) : new Vector3(p, 1f, p);
                    }
                    if (Kind == EnemyKind.Lancer) Face(chargeDirection, deltaTime * 20f);
                    if (timer <= 0f) Strike();
                    break;
                case Tactic.Charge:
                    float step = 17f * deltaTime;
                    transform.position = ClampToArena(transform.position + chargeDirection * step);
                    walkPhase += deltaTime * 20f;
                    Vector3 toPlayer = game.PlayerPosition - transform.position;
                    toPlayer.y = 0f;
                    if (!chargeHit && toPlayer.sqrMagnitude <= (strikeRadius + 0.25f) * (strikeRadius + 0.25f))
                    {
                        chargeHit = true;
                        game.TryDamagePlayer(strikeDamage, transform.position);
                    }
                    Vector3 travelled = transform.position - chargeStart;
                    travelled.y = 0f;
                    timer -= deltaTime;
                    if (travelled.magnitude >= 7f || timer <= 0f)
                    {
                        game.EnemyStrikeLanded(this, transform.position, false);
                        tactic = Tactic.Recover;
                        timer = 0.95f;
                    }
                    break;
                default:
                    timer -= deltaTime;
                    if (timer <= 0f) tactic = Tactic.Pursue;
                    break;
            }
        }

        private void BeginTelegraph(Vector3 delta)
        {
            tactic = Tactic.Telegraph;
            timer = telegraphLength;
            telegraphTotal = telegraphLength;
            chainRemaining = Enraged ? 2 : EchoSlam ? 1 : 0;
            if (Kind == EnemyKind.Lancer)
            {
                chargeDirection = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
                chargeStart = transform.position;
                threat.transform.position = new Vector3(transform.position.x, 0.045f, transform.position.z);
                threat.transform.rotation = Quaternion.LookRotation(chargeDirection);
            }
            else
            {
                markedPoint = game.PlayerPosition;
                markedPoint.y = 0.045f;
                threat.transform.position = markedPoint;
            }
            threat.SetActive(true);
            game.EnemyTelegraphed(this);
        }

        private void Strike()
        {
            threat.SetActive(false);
            if (Kind == EnemyKind.Lancer)
            {
                tactic = Tactic.Charge;
                chargeHit = false;
                timer = 0.6f;
                game.EnemyStrikeLanded(this, transform.position, false);
                return;
            }
            Vector3 offset = game.PlayerPosition - markedPoint;
            offset.y = 0f;
            if (offset.sqrMagnitude <= strikeRadius * strikeRadius)
                game.TryDamagePlayer(strikeDamage, markedPoint);
            game.EnemyStrikeLanded(this, markedPoint, IsBoss);
            punch = -0.35f;

            if (chainRemaining > 0)
            {
                chainRemaining--;
                // Follow-up slams track the player but keep a readable warning.
                tactic = Tactic.Telegraph;
                timer = 0.8f;
                telegraphTotal = 0.8f;
                markedPoint = game.PlayerPosition;
                markedPoint.y = 0.045f;
                threat.transform.position = markedPoint;
                threat.SetActive(true);
                game.EnemyTelegraphed(this);
                return;
            }
            tactic = Tactic.Recover;
            timer = IsBoss ? (Enraged ? 1.15f : 1.0f) : 0.72f;
        }

        /// <summary>Legacy entry point used by tests: a hit with no knockback.</summary>
        public void TakeHit(int damage) => TakeHit(damage, Vector3.zero, false);

        public void TakeHit(int damage, Vector3 push, bool stagger)
        {
            if (dead || damage <= 0 || game == null || !game.CanSimulate) return;
            health = Mathf.Max(0, health - damage);
            flashTime = 0.09f;
            punch = 0.3f;
            float resistance = IsBoss ? 0.25f : 1f;
            push.y = 0f;
            knockback += push * resistance;
            if (healthBar != null && !IsBoss) healthBar.gameObject.SetActive(true);
            if (health > 0)
            {
                if (stagger && !IsBoss && (tactic == Tactic.Telegraph || tactic == Tactic.Pursue))
                {
                    threat.SetActive(false);
                    tactic = Tactic.Recover;
                    timer = 0.65f;
                }
                if (IsBoss && !Enraged && health <= maxHealth / 2)
                {
                    Enraged = true;
                    telegraphLength = 1.15f;
                    moveSpeed = 2.9f;
                    game.BossEnraged(this);
                }
                game.SpawnHitFlash(transform.position + Vector3.up * (IsBoss ? 2f : 1.2f));
                return;
            }
            dead = true;
            if (threat != null) Destroy(threat);
            game.EnemyKilled(this);
        }

        private void AnimateFeedback(float deltaTime)
        {
            if (character != null)
            {
                if (flashTime > 0) character.Pose("RecieveHit", deltaTime, .2f, false);
                else if (tactic == Tactic.Telegraph) character.SampleAt("SwordSlash", .4f * (1f - timer / Mathf.Max(.01f, telegraphTotal)));
                else if (tactic == Tactic.Recover) character.SampleAt("SwordSlash", Mathf.Clamp01(.4f + (1f - timer) * .6f));
                else character.Pose(tactic == Tactic.Pursue || tactic == Tactic.Charge ? "Run" : "Idle", deltaTime);
            }
            if (flashTime > 0f)
            {
                flashTime -= Time.unscaledDeltaTime;
                Material flash = game.FlashMaterial;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sharedMaterial = flashTime > 0f ? flash : baseMaterials[i];
            }

            punch = Mathf.MoveTowards(punch, 0f, deltaTime * 2.2f);
            if (body != null && tactic != Tactic.Emerge)
            {
                float crouch = 0f;
                if (tactic == Tactic.Telegraph)
                    crouch = 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, telegraphLength));
                float breathe = Mathf.Sin(Time.time * 3.1f + transform.position.x) * 0.03f;
                float sy = 1f + breathe - 0.16f * crouch + punch * 0.4f;
                float sxz = 1f - breathe * 0.5f + 0.1f * crouch - punch * 0.25f;
                body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(sxz, sy, sxz));
                body.localRotation = Quaternion.Euler(-14f * crouch + (tactic == Tactic.Charge ? 18f : 0f), 0f, 0f);
            }
            if (leftLeg != null && rightLeg != null)
            {
                float swing = Mathf.Sin(walkPhase) * 32f;
                leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
            if (healthBar != null) healthBar.rotation = Quaternion.Euler(45f, 0f, 0f);
            if (healthFill != null)
            {
                float f = maxHealth > 0 ? (float)health / maxHealth : 0f;
                healthFill.localScale = new Vector3(Mathf.Max(0.001f, f), 1f, 1f);
                healthFill.localPosition = new Vector3(-0.5f + f * 0.5f, 0f, 0f);
            }
        }

        private Vector3 bodyBaseScale = Vector3.one;
        private bool bodyScaleCached;
        private bool emergeAnnounced;

        private void ApplyEmergePose(float t)
        {
            if (body != null && !bodyScaleCached)
            {
                bodyBaseScale = body.localScale;
                bodyScaleCached = true;
            }
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(-2.4f * (IsBoss ? 1.6f : 1f), 0f, eased);
            transform.position = p;
            if (t > 0f && !emergeAnnounced && game != null)
            {
                emergeAnnounced = true;
                game.EnemyEmerging(this);
            }
        }

        private void Face(Vector3 direction, float blend)
        {
            if (direction.sqrMagnitude <= 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Mathf.Min(1f, blend));
        }

        private static Vector3 ClampToArena(Vector3 position)
        {
            Vector2 h = new Vector2(position.x, position.z);
            if (h.magnitude > 8.6f) h = h.normalized * 8.6f;
            return new Vector3(h.x, position.y, h.y);
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Swap the material of named parts, keeping hit-flash restoration correct.</summary>
        public void Recolor(string partName, Material material)
        {
            if (renderers == null || material == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].name != partName) continue;
                baseMaterials[i] = material;
                if (flashTime <= 0f) renderers[i].sharedMaterial = material;
            }
        }

        private void OnDestroy()
        {
            if (threat != null) Destroy(threat);
        }
    }
}
