using UnityEngine;
using UnityEngine.EventSystems;

namespace RiftForge
{
    /// <summary>One dodge on press. A short early press may queue; release/exit cancels before consumption.</summary>
    public sealed class DodgeTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RiftGame game;
        private int? pointer;
        private float queuedUntil;
        private int clickFrame = -1;
        public bool IsQueued => pointer.HasValue && queuedUntil > 0;
        public void Cancel() { pointer = null; queuedUntil = 0; clickFrame = Time.frameCount; }
        public void Bind(RiftGame owner) => game = owner;
        public void OnPointerDown(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left || pointer.HasValue || game == null || !game.CanSimulate) return;
            pointer = data.pointerId; clickFrame = Time.frameCount;
            if (game.DodgeCooldownRemaining <= 0) game.Dodge();
            else if (game.DodgeCooldownRemaining <= .15f) queuedUntil = Time.unscaledTime + .18f;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data == null || pointer != data.pointerId) return;
            pointer = null; queuedUntil = 0; clickFrame = Time.frameCount;
        }
        public void OnPointerExit(PointerEventData data) => OnPointerUp(data);
        public void OnClick() { if (!pointer.HasValue && clickFrame != Time.frameCount) game?.Dodge(); }
        private void Update()
        {
            if (!IsQueued) return;
            if (!game.CanSimulate || Time.unscaledTime > queuedUntil) { queuedUntil = 0; return; }
            if (game.DodgeCooldownRemaining <= 0) { queuedUntil = 0; game.Dodge(); }
        }
        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focus) { if (!focus) OnDisable(); }
        private void OnApplicationPause(bool pause) { if (pause) OnDisable(); }
    }
}
