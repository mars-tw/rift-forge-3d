using NUnit.Framework;
using UnityEngine;

namespace RiftForge.Tests.EditMode
{
    /// <summary>
    /// Verifies that GameBootstrap wires its required scene dependencies exactly once.
    /// </summary>
    public sealed class GameBootstrapTests
    {
        [Test]
        public void Initialize_WithRequiredDependencies_AssignsCameraTargetAndIsIdempotent()
        {
            GameObject playerObject = new GameObject("Test Player");
            GameObject cameraObject = new GameObject("Test Camera Rig");
            GameObject bootstrapObject = new GameObject("Test Bootstrap");

            try
            {
                PlayerController player = playerObject.AddComponent<PlayerController>();
                CameraRig cameraRig = cameraObject.AddComponent<CameraRig>();
                GameBootstrap bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                bootstrap.Configure(player, cameraRig);

                bool firstResult = bootstrap.Initialize();
                bool secondResult = bootstrap.Initialize();

                Assert.That(firstResult, Is.True);
                Assert.That(secondResult, Is.True);
                Assert.That(bootstrap.IsInitialized, Is.True);
                Assert.That(cameraRig.Target, Is.SameAs(player.transform));
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
            }
        }
    }
}
