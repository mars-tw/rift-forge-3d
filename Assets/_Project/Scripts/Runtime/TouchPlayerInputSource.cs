using System;
using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Holds movement intent for one captured pointer, without moving or animating a player.
    /// When no pointer is captured, movement comes from the keyboard/gamepad fallback.
    /// </summary>
    public sealed class TouchPlayerInputSource : IPlayerInputSource
    {
        private readonly IPlayerInputSource fallback;
        private int? activePointerId;
        private Vector2 touchMove;
        private float deadZone;
        private bool enabled = true;
        private bool focused = true;
        private bool paused;

        public TouchPlayerInputSource(IPlayerInputSource fallback = null, float deadZone = 0.15f)
        {
            this.fallback = fallback ?? new InputSystemPlayerInputSource();
            DeadZone = deadZone;
        }

        /// <summary>The pointer owning movement; other UI fingers cannot replace it.</summary>
        public int? ActivePointerId => activePointerId;

        /// <summary>Radial dead zone in [0, 1). The remaining range is remapped continuously to [0, 1].</summary>
        public float DeadZone
        {
            get => deadZone;
            set
            {
                if (!IsFinite(value) || value < 0f || value >= 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                deadZone = value;
            }
        }

        private bool Available => enabled && focused && !paused;

        public bool TryBeginPointer(int pointerId)
        {
            if (!Available || activePointerId.HasValue)
            {
                return false;
            }

            activePointerId = pointerId;
            touchMove = Vector2.zero;
            return true;
        }

        /// <summary>Accepts a displacement divided by the UI's travel radius.</summary>
        public bool TryMovePointer(int pointerId, Vector2 normalizedDisplacement)
        {
            if (!Available || activePointerId != pointerId)
            {
                return false;
            }

            touchMove = ClampFinite(normalizedDisplacement);
            return true;
        }

        /// <summary>Releases only the owning pointer, including an OS-cancelled touch.</summary>
        public bool CancelPointer(int pointerId)
        {
            if (activePointerId != pointerId)
            {
                return false;
            }

            ResetTouch();
            return true;
        }

        public void ResetTouch()
        {
            activePointerId = null;
            touchMove = Vector2.zero;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            ResetIfUnavailable();
        }

        public void SetApplicationFocus(bool value)
        {
            focused = value;
            ResetIfUnavailable();
        }

        public void SetApplicationPaused(bool value)
        {
            paused = value;
            ResetIfUnavailable();
        }

        /// <inheritdoc />
        public Vector2 ReadMove()
        {
            if (!Available)
            {
                return Vector2.zero;
            }

            if (!activePointerId.HasValue)
            {
                return ClampFinite(fallback.ReadMove());
            }

            float magnitude = touchMove.magnitude;
            if (magnitude <= deadZone)
            {
                return Vector2.zero;
            }

            return touchMove / magnitude * ((magnitude - deadZone) / (1f - deadZone));
        }

        private void ResetIfUnavailable()
        {
            if (!Available)
            {
                ResetTouch();
            }
        }

        private static Vector2 ClampFinite(Vector2 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                return Vector2.zero;
            }

            // Scale first to avoid overflowing squared magnitude for large finite coordinates.
            float largest = Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y));
            if (largest > 1f)
            {
                return (value / largest).normalized;
            }

            return Vector2.ClampMagnitude(value, 1f);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
