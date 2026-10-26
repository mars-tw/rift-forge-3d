using System.Collections.Generic;
using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Pooled particle effects driven through ParticleSystem.Emit, plus short-lived expanding
    /// shockwave rings. One emitter per look keeps draw calls and allocations flat on mobile.
    /// </summary>
    public sealed class RiftFx : MonoBehaviour
    {
        private sealed class Ring
        {
            public Transform Visual;
            public float Age;
            public float Life;
            public float From;
            public float To;
        }

        private readonly List<Ring> rings = new List<Ring>();
        private readonly Stack<Transform> ringPool = new Stack<Transform>();
        private ParticleSystem sparks;
        private ParticleSystem glow;
        private ParticleSystem embers;
        private Material particleMaterial;
        private Material ringMaterial;
        private bool ownsParticleMaterial;

        public bool HasParticles => sparks != null && particleMaterial != null;
        public int ActiveRings => rings.Count;

        public void Initialize(Material shockwaveMaterial)
        {
            ringMaterial = shockwaveMaterial;
            particleMaterial = Resources.Load<Material>("RiftParticleAdd");
            if (particleMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null)
                {
                    particleMaterial = new Material(shader);
                    ownsParticleMaterial = true;
                }
            }
            if (particleMaterial == null) return; // Effects degrade to rings only.

            sparks = CreateSystem("Spark FX", 0.45f, 600, 1.2f, true);
            glow = CreateSystem("Glow FX", 0.6f, 200, 0f, false);
            embers = CreateSystem("Ember Ambience", 6f, 160, -0.08f, false);
            ParticleSystem.MainModule main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f, 0.9f), new Color(0.3f, 0.95f, 1f, 0.8f));
            ParticleSystem.EmissionModule emission = embers.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = embers.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 10f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            embers.transform.position = new Vector3(0f, -0.1f, 0f);
            embers.Play();
        }

        private ParticleSystem CreateSystem(string name, float lifetime, int max, float gravity, bool stretch)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(transform, false);
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystemRenderer renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.4f;
            }
            return system;
        }

        /// <summary>Radial burst of streaking sparks.</summary>
        public void Sparks(Vector3 position, Color color, int count, float speed, float size = 0.12f)
        {
            if (sparks == null) return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.15f;
                emit.position = position;
                emit.velocity = direction.normalized * (speed * Random.Range(0.45f, 1f));
                emit.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.35f));
                emit.startSize = size * Random.Range(0.6f, 1.3f);
                emit.startLifetime = Random.Range(0.22f, 0.5f);
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>Soft glowing puffs (death bursts, forge glow, dodge afterimage).</summary>
        public void Glow(Vector3 position, Color color, int count, float spread, float size)
        {
            if (glow == null) return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                emit.position = position + Random.insideUnitSphere * spread;
                emit.velocity = Random.insideUnitSphere * (spread * 1.5f) + Vector3.up * 0.6f;
                emit.startColor = color;
                emit.startSize = size * Random.Range(0.7f, 1.3f);
                emit.startLifetime = Random.Range(0.3f, 0.65f);
                glow.Emit(emit, 1);
            }
        }

        /// <summary>A flat ring that expands from <paramref name="from"/> to <paramref name="to"/> metres.</summary>
        public void Shockwave(Vector3 position, float from, float to, float life)
        {
            if (ringMaterial == null) return;
            Transform visual = ringPool.Count > 0 ? ringPool.Pop() : null;
            if (visual == null)
            {
                visual = new GameObject("Shockwave").transform;
                visual.SetParent(transform, false);
                const int segments = 24;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    GameObject segment = RiftGame.MakePrimitive(PrimitiveType.Cube, "Wave Segment", visual,
                        new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f),
                        new Vector3(0.15f, 0.04f, 0.035f), ringMaterial);
                    segment.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);
                    Renderer renderer = segment.GetComponent<Renderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            visual.gameObject.SetActive(true);
            visual.position = new Vector3(position.x, 0.06f, position.z);
            visual.localScale = Vector3.one * (from * 2f);
            rings.Add(new Ring { Visual = visual, Life = Mathf.Max(0.05f, life), From = from, To = to });
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                Ring ring = rings[i];
                ring.Age += dt;
                float t = Mathf.Clamp01(ring.Age / ring.Life);
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(ring.From, ring.To, eased);
                ring.Visual.localScale = new Vector3(radius * 2f, 1f + 2f * (1f - t), radius * 2f);
                if (t < 1f) continue;
                ring.Visual.gameObject.SetActive(false);
                ringPool.Push(ring.Visual);
                rings.RemoveAt(i);
            }
        }

        public void Clear()
        {
            if (sparks != null) sparks.Clear();
            if (glow != null) glow.Clear();
            foreach (Ring ring in rings)
            {
                ring.Visual.gameObject.SetActive(false);
                ringPool.Push(ring.Visual);
            }
            rings.Clear();
        }

        private void OnDestroy()
        {
            if (ownsParticleMaterial && particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
