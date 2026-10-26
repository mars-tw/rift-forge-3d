using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    /// <summary>
    /// Verifies PlayerController ground-plane mapping and diagonal normalization.
    /// </summary>
    public sealed class PlayerControllerTests
    {
        [Test]
        public void CalculateMovementVector_NormalizesDiagonalDigitalInput()
        {
            Vector3 movement = PlayerController.CalculateMovementVector(Vector2.one);

            Assert.That(movement.magnitude, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(movement.x, Is.EqualTo(1f / Mathf.Sqrt(2f)).Within(0.000001f));
            Assert.That(movement.z, Is.EqualTo(1f / Mathf.Sqrt(2f)).Within(0.000001f));
        }

        [Test]
        public void CalculateMovementVector_PreservesAnalogInputBelowUnitLength()
        {
            Vector3 movement = PlayerController.CalculateMovementVector(new Vector2(0.25f, -0.5f));

            Assert.That(movement, Is.EqualTo(new Vector3(0.25f, 0f, -0.5f)));
        }
    }
}
