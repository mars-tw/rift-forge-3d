using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RiftForge
{
    /// <summary>
    /// Converts uGUI drag events on a dedicated movement area to movement intent.
    /// The player controller remains responsible for all physical movement.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TouchMoveArea : MonoBehaviour, IPointerDownHandler, IDragHandler,
        IPointerUpHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(1f)] private float travelRadius = 100f;
        [SerializeField, Range(0f, 0.95f)] private float deadZone = 0.15f;

        private TouchPlayerInputSource source;
        private Vector2 origin;

        public TouchPlayerInputSource Source => source ??= new TouchPlayerInputSource(deadZone: SafeDeadZone);
        public Vector2 VisualOrigin => Source.ActivePointerId.HasValue ? Vector2.ClampMagnitude(origin, 20f) : Vector2.zero;

        /// <summary>Travel radius in RectTransform local units, independent of Canvas scaling.</summary>
        public float TravelRadius
        {
            get => float.IsNaN(travelRadius) || float.IsInfinity(travelRadius) ? 100f : Mathf.Max(1f, travelRadius);
            set => travelRadius = float.IsNaN(value) || float.IsInfinity(value) ? 100f : Mathf.Max(1f, value);
        }

        private float SafeDeadZone => float.IsNaN(deadZone) || float.IsInfinity(deadZone)
            ? 0.15f : Mathf.Clamp(deadZone, 0f, 0.95f);

        private void Awake()
        {
            if (playerController != null)
            {
                BindTo(playerController);
            }
        }

        public void BindTo(PlayerController target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            playerController = target;
            target.SetInputSource(Source);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (TryGetLocalPosition(eventData, out Vector2 local) && Source.TryBeginPointer(eventData.pointerId))
            {
                origin = local;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null && Source.ActivePointerId == eventData.pointerId)
            {
                eventData.useDragThreshold = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData == null || Source.ActivePointerId != eventData.pointerId)
            {
                return;
            }

            if (TryGetLocalPosition(eventData, out Vector2 local))
            {
                Source.TryMovePointer(eventData.pointerId, (local - origin) / TravelRadius);
            }
            else
            {
                Source.CancelPointer(eventData.pointerId);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                Source.CancelPointer(eventData.pointerId);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnPointerUp(eventData);
        }

        private bool TryGetLocalPosition(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, eventData.position, eventData.pressEventCamera, out local);
        }

        private void OnEnable() => Source.SetEnabled(true);
        private void OnDisable() => Source.SetEnabled(false);
        private void OnApplicationFocus(bool focused) => Source.SetApplicationFocus(focused);
        private void OnApplicationPause(bool paused) => Source.SetApplicationPaused(paused);

        private void OnValidate()
        {
            TravelRadius = travelRadius;
            deadZone = SafeDeadZone;
            if (source != null)
            {
                source.DeadZone = deadZone;
            }
        }
    }
}
