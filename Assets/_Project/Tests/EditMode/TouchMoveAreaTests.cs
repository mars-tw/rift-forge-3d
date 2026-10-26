using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RiftForge.Tests.EditMode
{
    public sealed class TouchMoveAreaTests
    {
        private GameObject areaObject;
        private GameObject eventSystemObject;
        private TouchMoveArea area;
        private EventSystem eventSystem;

        [SetUp]
        public void SetUp()
        {
            eventSystemObject = new GameObject("TouchMoveAreaTests.EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            areaObject = new GameObject("TouchMoveAreaTests.Area", typeof(RectTransform), typeof(TouchMoveArea));
            area = areaObject.GetComponent<TouchMoveArea>();
            area.Source.DeadZone = 0f;
            area.TravelRadius = 100f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(areaObject);
            Object.DestroyImmediate(eventSystemObject);
        }

        private PointerEventData Pointer(int id, Vector2 position,
            PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            return new PointerEventData(eventSystem) { pointerId = id, position = position, button = button };
        }

        [Test]
        public void Drag_UsesLocalTravelRadiusWithoutMovingTheUIOrPlayer()
        {
            Vector3 initialPosition = area.transform.position;
            area.OnPointerDown(Pointer(1, new Vector2(20f, 30f)));
            area.OnDrag(Pointer(1, new Vector2(45f, 80f)));

            Assert.That(area.Source.ReadMove().x, Is.EqualTo(0.25f).Within(0.000001f));
            Assert.That(area.Source.ReadMove().y, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(area.transform.position, Is.EqualTo(initialPosition));
        }

        [Test]
        public void OtherFingerDownDragAndUp_DoNotReplaceOriginOrMovement()
        {
            area.OnPointerDown(Pointer(1, Vector2.zero));
            area.OnDrag(Pointer(1, Vector2.right * 50f));
            area.OnPointerDown(Pointer(2, Vector2.one * 500f));
            area.OnDrag(Pointer(2, Vector2.left * 1000f));
            area.OnPointerUp(Pointer(2, Vector2.zero));
            area.OnDrag(Pointer(1, Vector2.up * 50f));

            Assert.That(area.Source.ActivePointerId, Is.EqualTo(1));
            Assert.That(area.Source.ReadMove(), Is.EqualTo(Vector2.up * 0.5f));
        }

        [Test]
        public void PointerUpAndEndDrag_ClearOwnerWithoutAcceptingLateDrag()
        {
            area.OnPointerDown(Pointer(1, Vector2.zero));
            area.OnDrag(Pointer(1, Vector2.up * 100f));
            area.OnPointerUp(Pointer(1, Vector2.zero));
            area.OnEndDrag(Pointer(1, Vector2.zero));
            area.OnDrag(Pointer(1, Vector2.up * 100f));

            Assert.That(area.Source.ActivePointerId, Is.Null);
            Assert.That(area.Source.TryBeginPointer(2), Is.True);
            Assert.That(area.Source.ReadMove(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RightMouseButton_DoesNotCaptureOrReleaseMovement()
        {
            area.OnPointerDown(Pointer(1, Vector2.zero, PointerEventData.InputButton.Right));
            Assert.That(area.Source.ActivePointerId, Is.Null);
            area.OnPointerDown(Pointer(1, Vector2.zero));
            area.OnPointerUp(Pointer(1, Vector2.zero, PointerEventData.InputButton.Right));
            Assert.That(area.Source.ActivePointerId, Is.EqualTo(1));
        }

        [Test]
        public void DragThreshold_IsDisabledOnlyForCapturedPointer()
        {
            area.OnPointerDown(Pointer(1, Vector2.zero));
            PointerEventData owner = Pointer(1, Vector2.zero);
            PointerEventData other = Pointer(2, Vector2.zero);
            area.OnInitializePotentialDrag(owner);
            area.OnInitializePotentialDrag(other);

            Assert.That(owner.useDragThreshold, Is.False);
            Assert.That(other.useDragThreshold, Is.True);
        }

        [Test]
        public void InvalidRadius_IsRepairedBeforeDividingDisplacement()
        {
            area.TravelRadius = 0f;
            Assert.That(area.TravelRadius, Is.EqualTo(1f));
            area.TravelRadius = float.NaN;
            Assert.That(area.TravelRadius, Is.EqualTo(100f));
        }
    }
}
