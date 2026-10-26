using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RiftForge
{
    /// <summary>
    /// Turns one touch into repeated normal attacks while it remains on STRIKE.
    /// RiftGame still owns the attack cooldown, hit test, damage and combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoldStrikeButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RiftGame game;
        private int? heldPointerId;
        private int suppressedClickFrame = -1;

        public RiftGame Owner => game;
        public bool IsHeld => heldPointerId.HasValue;
        public void Cancel() => ResetHold();

        public void Bind(RiftGame owner)
        {
            game = owner ?? throw new ArgumentNullException(nameof(owner));
            ResetHold();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                heldPointerId.HasValue || game == null || !game.CanSimulate) return;

            heldPointerId = eventData.pointerId;
            game.Attack();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || heldPointerId != eventData.pointerId) return;
            heldPointerId = null;
            // Unity's Button invokes onClick after pointer-up in the same frame.
            suppressedClickFrame = Time.frameCount;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null || heldPointerId != eventData.pointerId) return;
            heldPointerId = null;
            suppressedClickFrame = Time.frameCount;
        }

        /// <summary>Keep keyboard, gamepad and accessibility Button activation working.</summary>
        public void OnClick()
        {
            if (!heldPointerId.HasValue && suppressedClickFrame != Time.frameCount) game?.Attack();
        }

        private void Update()
        {
            if (!heldPointerId.HasValue) return;
            if (game == null || !game.CanSimulate)
            {
                ResetHold();
                return;
            }
            if (game.AttackCooldownRemaining <= 0f) game.Attack();
        }

        private void OnDisable() => ResetHold();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetHold();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetHold();
        }

        private void ResetHold()
        {
            heldPointerId = null;
            suppressedClickFrame = -1;
        }
    }
}
