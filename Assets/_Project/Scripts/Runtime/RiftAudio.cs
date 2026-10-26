using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiftForge
{
    public enum RiftSfx
    {
        Swing, Hit, Finisher, Burst, Kill, Telegraph, Slam, Dodge, Perfect, Pickup, Forge, WaveStart,
        Hurt, Victory, Defeat, UiTap
    }

    /// <summary>Deterministic procedural sound synthesis. No external audio files are required.</summary>
    public static class RiftAudioSynth
    {
        public const int SampleRate = 44100;

        public static float Duration(RiftSfx sfx)
        {
            switch (sfx)
            {
                case RiftSfx.Swing: return 0.16f;
                case RiftSfx.Hit: return 0.18f;
                case RiftSfx.Finisher: return 0.36f;
                case RiftSfx.Burst: return 0.75f;
                case RiftSfx.Kill: return 0.32f;
                case RiftSfx.Telegraph: return 0.4f;
                case RiftSfx.Slam: return 0.55f;
                case RiftSfx.Dodge: return 0.22f;
                case RiftSfx.Perfect: return 0.7f;
                case RiftSfx.Pickup: return 0.2f;
                case RiftSfx.Forge: return 0.9f;
                case RiftSfx.WaveStart: return 0.9f;
                case RiftSfx.Hurt: return 0.25f;
                case RiftSfx.Victory: return 1.6f;
                case RiftSfx.Defeat: return 1.4f;
                default: return 0.07f;
            }
        }

        /// <summary>Render a mono sound. Output is bounded to [-0.9, 0.9] and fades out at the end.</summary>
        public static float[] Render(RiftSfx sfx)
        {
            int length = Mathf.Max(1, Mathf.RoundToInt(Duration(sfx) * SampleRate));
            float[] data = new float[length];
            uint seed = 0x9E3779B9u ^ (uint)((int)sfx * 7919 + 17);
            float lowpass = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float u = i / (float)length;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 9) / 4194304f) - 1f;
                float v;
                switch (sfx)
                {
                    case RiftSfx.Swing:
                    {
                        float cutoff = Mathf.Lerp(0.05f, 0.45f, Mathf.Sin(u * Mathf.PI));
                        lowpass += (noise - lowpass) * cutoff;
                        v = lowpass * Mathf.Sin(u * Mathf.PI) * 0.9f;
                        break;
                    }
                    case RiftSfx.Hit:
                        v = Sine(t, Mathf.Lerp(180f, 70f, u)) * Env(u, 0.01f, 4f) * 0.8f + noise * Env(u, 0.002f, 14f) * 0.45f;
                        break;
                    case RiftSfx.Finisher:
                        v = Sine(t, Mathf.Lerp(140f, 45f, u)) * Env(u, 0.01f, 3f) * 0.8f + noise * Env(u, 0.002f, 8f) * 0.4f
                            + Sine(t, 1240f) * Env(u, 0.002f, 10f) * 0.18f;
                        break;
                    case RiftSfx.Burst:
                        lowpass += (noise - lowpass) * 0.08f;
                        v = Sine(t, Mathf.Lerp(90f, 32f, u)) * Env(u, 0.01f, 2.5f) * 0.75f + lowpass * Env(u, 0.02f, 2f) * 1.2f
                            + Sine(t, 660f + 220f * u) * Env(u, 0.05f, 3f) * 0.15f;
                        break;
                    case RiftSfx.Kill:
                        v = (Sine(t, 880f * (1f - 0.4f * u)) * 0.25f + Sine(t, 55f) * 0.6f) * Env(u, 0.004f, 5f)
                            + noise * Env(u, 0.001f, 12f) * 0.3f;
                        break;
                    case RiftSfx.Telegraph:
                        v = Square(t, Mathf.Lerp(220f, 520f, u)) * 0.12f * Mathf.Sin(u * Mathf.PI);
                        break;
                    case RiftSfx.Slam:
                        lowpass += (noise - lowpass) * 0.05f;
                        v = Sine(t, Mathf.Lerp(70f, 28f, u)) * Env(u, 0.005f, 3f) * 0.9f + lowpass * Env(u, 0.002f, 4f) * 1.3f;
                        break;
                    case RiftSfx.Dodge:
                    {
                        float cutoff = Mathf.Lerp(0.4f, 0.03f, u);
                        lowpass += (noise - lowpass) * cutoff;
                        v = lowpass * Env(u, 0.05f, 2.5f) * 1.1f;
                        break;
                    }
                    case RiftSfx.Perfect:
                        v = (Sine(t, 1318.5f) * 0.3f + Sine(t, 1975.5f) * 0.2f + Sine(t, 2637f) * 0.1f) * Env(u, 0.003f, 3.5f)
                            + Sine(t, Mathf.Lerp(200f, 900f, u)) * 0.12f * Mathf.Sin(u * Mathf.PI);
                        break;
                    case RiftSfx.Pickup:
                        v = Sine(t, u < 0.4f ? 1046.5f : 1568f) * Env(u, 0.002f, 4f) * 0.4f;
                        break;
                    case RiftSfx.Forge:
                        // Anvil: inharmonic partials with a fast metallic decay.
                        v = (Sine(t, 523f) * 0.35f + Sine(t, 1393f) * 0.22f + Sine(t, 2741f) * 0.14f + Sine(t, 4120f) * 0.07f)
                            * Env(u, 0.001f, 4.5f) + noise * Env(u, 0.0005f, 30f) * 0.4f;
                        break;
                    case RiftSfx.WaveStart:
                        v = (Saw(t, 110f) * 0.18f + Saw(t, 164.8f) * 0.12f + Sine(t, 55f) * 0.4f) * Env(u, 0.08f, 2.2f);
                        break;
                    case RiftSfx.Hurt:
                        v = Square(t, Mathf.Lerp(300f, 90f, u)) * 0.22f * Env(u, 0.005f, 4f) + noise * Env(u, 0.002f, 10f) * 0.35f;
                        break;
                    case RiftSfx.Victory:
                    {
                        float note = u < 0.2f ? 523.3f : u < 0.4f ? 659.3f : u < 0.6f ? 784f : 1046.5f;
                        v = (Sine(t, note) * 0.3f + Saw(t, note * 0.5f) * 0.08f) * Env(u, 0.01f, 1.4f);
                        break;
                    }
                    case RiftSfx.Defeat:
                    {
                        float note = u < 0.33f ? 392f : u < 0.66f ? 311.1f : 233.1f;
                        v = (Sine(t, note) * 0.3f + Saw(t, note * 0.5f) * 0.08f) * Env(u, 0.01f, 1.2f);
                        break;
                    }
                    default:
                        v = Sine(t, 1400f) * Env(u, 0.001f, 8f) * 0.3f;
                        break;
                }
                float fadeOut = Mathf.Clamp01((1f - u) * 25f);
                float fadeIn = Mathf.Clamp01(i / 32f);
                data[i] = Mathf.Clamp(v * fadeIn * fadeOut, -0.9f, 0.9f);
            }
            return data;
        }

        private static float Sine(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Square(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t) >= 0f ? 1f : -1f;
        private static float Saw(float t, float hz) => 2f * (t * hz - Mathf.Floor(0.5f + t * hz));

        /// <summary>Attack/exponential-decay envelope on normalized time.</summary>
        private static float Env(float u, float attack, float decay)
        {
            if (u < attack) return u / attack;
            return Mathf.Exp(-decay * (u - attack));
        }
    }

    /// <summary>Plays synthesized effects and the optional music loop from Resources/Audio.</summary>
    public sealed class RiftAudio : MonoBehaviour
    {
        private readonly Dictionary<RiftSfx, AudioClip> clips = new Dictionary<RiftSfx, AudioClip>();
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private AudioSource music;
        private int nextVoice;
        private float targetMusicVolume;

        public static float SfxVolume = 0.8f;
        public bool HasMusic => music != null && music.clip != null;
        public int ClipCount => clips.Count;

        public void Initialize()
        {
            foreach (RiftSfx sfx in Enum.GetValues(typeof(RiftSfx)))
            {
                float[] samples = RiftAudioSynth.Render(sfx);
                AudioClip clip = AudioClip.Create("sfx_" + sfx, samples.Length, 1, RiftAudioSynth.SampleRate, false);
                clip.SetData(samples, 0);
                clips[sfx] = clip;
            }
            for (int i = 0; i < 10; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices.Add(source);
            }
            AudioClip loop = Resources.Load<AudioClip>("Audio/rift_battle_loop");
            if (loop != null)
            {
                music = gameObject.AddComponent<AudioSource>();
                music.clip = loop;
                music.loop = true;
                music.playOnAwake = false;
                music.volume = 0f;
                music.Play();
            }
        }

        public void Play(RiftSfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (voices.Count == 0 || !clips.TryGetValue(sfx, out AudioClip clip)) return;
            AudioSource source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * SfxVolume));
        }

        /// <summary>0 silences the music (title/forge quieter, combat louder).</summary>
        public void SetMusicIntensity(float level) => targetMusicVolume = Mathf.Clamp01(level) * 0.55f;

        private void Update()
        {
            if (music == null) return;
            music.volume = Mathf.MoveTowards(music.volume, targetMusicVolume, Time.unscaledDeltaTime * 0.6f);
            music.pitch = Mathf.Lerp(music.pitch, Time.timeScale < 0.5f ? 0.85f : 1f, Time.unscaledDeltaTime * 8f);
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips.Values) if (clip != null) Destroy(clip);
            clips.Clear();
        }
    }
}
