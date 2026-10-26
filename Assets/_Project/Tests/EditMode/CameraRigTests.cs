using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    /// <summary>
    /// Verifies the frame-rate-independent damping contract used by CameraRig.
    /// </summary>
    public sealed class CameraRigTests
    {
        [Test]
        public void CalculateDampingFactor_MatchesExponentialFormula()
        {
            float actual = CameraRig.CalculateDampingFactor(5f, 0.1f);
            float expected = 1f - Mathf.Exp(-0.5f);

            Assert.That(actual, Is.EqualTo(expected).Within(0.000001f));
            Assert.That(actual, Is.InRange(0f, 1f));
        }

        [Test]
        public void CalculateDampingFactor_ComposesAcrossHalfSteps()
        {
            float fullStep = CameraRig.CalculateDampingFactor(4f, 0.2f);
            float halfStep = CameraRig.CalculateDampingFactor(4f, 0.1f);
            float composed = halfStep + ((1f - halfStep) * halfStep);

            Assert.That(composed, Is.EqualTo(fullStep).Within(0.000001f));
        }
    }
}
