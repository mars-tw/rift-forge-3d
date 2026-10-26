using UnityEngine;

namespace RiftForge
{
    /// <summary>Samples authored skeletal curves; combat timing remains owned by the game rules.</summary>
    public sealed class RiftCharacter : MonoBehaviour
    {
        private Animation clips;
        private string current;
        private float clock;
        public int ClipCount => clips == null ? 0 : clips.GetClipCount();
        public void Initialize() { clips = GetComponentInChildren<Animation>(); if (clips != null) clips.Stop(); }
        public void SampleAt(string action, float phase)
        {
            if (clips == null) return;
            current = action; clock = 0;
            foreach (AnimationState state in clips)
            {
                bool active = state.name == action || state.name.EndsWith("|" + action);
                state.enabled = active; state.weight = active ? 1 : 0;
                if (active) state.normalizedTime = Mathf.Clamp01(phase);
            }
            clips.Sample();
        }
        public void Pose(string action, float delta, float duration = 0, bool loop = true)
        {
            if (clips == null) return;
            if (action != current) { current = action; clock = 0; }
            clock += Mathf.Max(0, delta);
            foreach (AnimationState state in clips)
            {
                bool active = state.name == action || state.name.EndsWith("|" + action);
                state.enabled = active; state.weight = active ? 1 : 0;
                if (!active) continue;
                float phase = clock / Mathf.Max(.001f, duration > 0 ? duration : state.length);
                state.normalizedTime = loop ? Mathf.Repeat(phase, 1) : Mathf.Clamp01(phase);
            }
            clips.Sample();
        }
    }
}
