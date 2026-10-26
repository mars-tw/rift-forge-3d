using System;
using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    public sealed class TouchPlayerInputSourceTests
    {
        private sealed class FixedInput : IPlayerInputSource
        {
            public Vector2 Move;
            public int Reads;
            public Vector2 ReadMove() { Reads++; return Move; }
        }

        private static TouchPlayerInputSource Create(float deadZone = 0f)
        {
            return new TouchPlayerInputSource(new FixedInput(), deadZone);
        }

        [Test]
        public void NoCapturedPointer_UsesCurrentFallbackAndClampsItsDiagonal()
        {
            var fallback = new FixedInput { Move = Vector2.one };
            var source = new TouchPlayerInputSource(fallback);

            Assert.That(source.ReadMove().magnitude, Is.EqualTo(1f).Within(0.000001f));
            fallback.Move = new Vector2(0.25f, -0.5f);
            Assert.That(source.ReadMove(), Is.EqualTo(fallback.Move));
            Assert.That(fallback.Reads, Is.EqualTo(2));
        }

        [Test]
        public void CapturedTouch_HasPriorityEvenAtRestAndDoesNotReadFallback()
        {
            var fallback = new FixedInput { Move = Vector2.right };
            var source = new TouchPlayerInputSource(fallback);

            Assert.That(source.TryBeginPointer(10), Is.True);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(fallback.Reads, Is.Zero);
            source.CancelPointer(10);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.right));
        }

        [Test]
        public void SecondFinger_CannotCaptureMoveOrReleaseOwnersMovement()
        {
            var source = Create();
            source.TryBeginPointer(10);
            source.TryMovePointer(10, Vector2.right * 0.5f);

            Assert.That(source.TryBeginPointer(11), Is.False);
            Assert.That(source.TryMovePointer(11, Vector2.left), Is.False);
            Assert.That(source.CancelPointer(11), Is.False);
            Assert.That(source.ActivePointerId, Is.EqualTo(10));
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.right * 0.5f));
        }

        [Test]
        public void CancelOwner_ClearsMovementAndRejectsStaleDragUntilNewPress()
        {
            var source = Create();
            source.TryBeginPointer(10);
            source.TryMovePointer(10, Vector2.up);

            Assert.That(source.CancelPointer(10), Is.True);
            Assert.That(source.ActivePointerId, Is.Null);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(source.TryMovePointer(10, Vector2.up), Is.False);
            Assert.That(source.TryBeginPointer(11), Is.True);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(0.2f)]
        public void RadialDeadZone_RejectsMovementAtOrBelowBoundary(float magnitude)
        {
            var source = Create(0.2f);
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.up * magnitude);

            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RadialDeadZone_RemapsRemainingTravelContinuously()
        {
            var source = Create(0.2f);
            source.TryBeginPointer(1);
            source.TryMovePointer(1, new Vector2(0.36f, 0.48f)); // Magnitude 0.6, direction (0.6, 0.8).

            Vector2 move = source.ReadMove();
            Assert.That(move.x, Is.EqualTo(0.3f).Within(0.000001f));
            Assert.That(move.y, Is.EqualTo(0.4f).Within(0.000001f));
            source.TryMovePointer(1, Vector2.up * 0.20001f);
            Assert.That(source.ReadMove().magnitude, Is.LessThan(0.00002f));
        }

        [Test]
        public void DiagonalAndBeyondTravelRadius_CannotExceedUnitSpeed()
        {
            var source = Create(0.2f);
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.one * 4f);

            Vector2 move = source.ReadMove();
            Assert.That(move.magnitude, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(move.x, Is.EqualTo(1f / Mathf.Sqrt(2f)).Within(0.000001f));
            Assert.That(move.y, Is.EqualTo(move.x).Within(0.000001f));
        }

        [Test]
        public void ZeroDeadZone_PreservesAnalogTravelAndAxisSigns()
        {
            var source = Create();
            source.TryBeginPointer(-23); // Input System pointer IDs are opaque, not finger indexes.
            source.TryMovePointer(-23, new Vector2(-0.25f, 0.5f));

            Assert.That(source.ReadMove().x, Is.EqualTo(-0.25f).Within(0.000001f));
            Assert.That(source.ReadMove().y, Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void NonFiniteInput_DoesNotPoisonPlayerMovement()
        {
            var source = Create();
            source.TryBeginPointer(1);
            source.TryMovePointer(1, new Vector2(float.NaN, 1f));
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            source.TryMovePointer(1, new Vector2(1f, float.PositiveInfinity));
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            source.TryMovePointer(1, new Vector2(float.MaxValue, float.MaxValue));
            Assert.That(source.ReadMove().magnitude, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void FocusLoss_SuppressesFallbackAndRequiresANewTouchAfterReturn()
        {
            var fallback = new FixedInput { Move = Vector2.left };
            var source = new TouchPlayerInputSource(fallback, 0f);
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.up);

            source.SetApplicationFocus(false);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(source.ActivePointerId, Is.Null);
            Assert.That(source.TryBeginPointer(2), Is.False);
            source.SetApplicationFocus(true);
            Assert.That(source.TryMovePointer(1, Vector2.up), Is.False);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.left));
            Assert.That(source.TryBeginPointer(2), Is.True);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ResumeWhileStillUnfocused_CannotReactivateMovement()
        {
            var source = new TouchPlayerInputSource(new FixedInput { Move = Vector2.up });
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.right);
            source.SetApplicationPaused(true);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(source.TryBeginPointer(2), Is.False);
            source.SetApplicationFocus(false);
            source.SetApplicationPaused(false);

            Assert.That(source.ActivePointerId, Is.Null);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(source.TryBeginPointer(2), Is.False);
            source.SetApplicationFocus(true);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.up));
        }

        [Test]
        public void Disable_ClearsPointerAndSuppressesAllInputUntilEnable()
        {
            var source = new TouchPlayerInputSource(new FixedInput { Move = Vector2.right });
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.up);
            source.SetEnabled(false);

            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(source.ActivePointerId, Is.Null);
            Assert.That(source.TryBeginPointer(1), Is.False);
            source.SetEnabled(true);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.right));
            Assert.That(source.TryMovePointer(1, Vector2.up), Is.False);
        }

        [Test]
        public void ResetTouch_IsIdempotentAndReturnsToFallback()
        {
            var source = new TouchPlayerInputSource(new FixedInput { Move = Vector2.down });
            source.TryBeginPointer(1);
            source.TryMovePointer(1, Vector2.right);
            source.ResetTouch();
            source.ResetTouch();

            Assert.That(source.ActivePointerId, Is.Null);
            Assert.That(source.ReadMove(), Is.EqualTo(Vector2.down));
        }

        [Test]
        public void DeadZone_RejectsValuesWithNoUsableAnalogRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(float.NaN));
        }
    }
}
