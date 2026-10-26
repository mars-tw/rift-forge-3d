using System;
using System.IO;
using NUnit.Framework;
using RiftForge.Editor;
using UnityEditor;
using UnityEditor.Build;

namespace RiftForge.Tests.EditMode
{
    public sealed class NativeReleaseGateTests
    {
        private static string Config(string stage = "complete-game", bool ready = true,
            string main = NativeReleaseGate.MainScenePath, int schema = 1)
        {
            return "{\"schemaVersion\":" + schema + ",\"sourceStage\":\"" + stage
                + "\",\"readyForStore\":" + (ready ? "true" : "false")
                + ",\"mainScene\":\"" + main + "\"}";
        }

        [Test]
        public void MissingConfig_FailsBeforeSceneValidation()
        {
            string missing = Path.Combine(Path.GetTempPath(), "RiftForgeReleaseGateMissing-" + Guid.NewGuid().ToString("N"), "build-config.json");
            bool sceneChecked = false;

            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfigFile(missing, _ => { sceneChecked = true; return true; }));

            Assert.That(error.Message, Does.Contain("missing"));
            Assert.That(sceneChecked, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not JSON")]
        [TestCase("{ this is not JSON }")]
        [TestCase("{\"schemaVersion\":1,\"sourceStage\":\"complete-game\"")]
        [TestCase("{\"schemaVersion\":1,\"sourceStage\":\"complete-game\",\"readyForStore\":true,\"mainScene\":\"Assets/_Project/Scenes/Main.unity\"}{}")]
        public void MalformedConfig_FailsClosed(string json)
        {
            bool sceneChecked = false;
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(json, _ => { sceneChecked = true; return true; }));

            Assert.That(error.Message, Does.Contain("malformed"));
            Assert.That(sceneChecked, Is.False);
        }

        [Test]
        public void FoundationStage_CannotBeReleasedByFlippingReadyFlag()
        {
            bool sceneChecked = false;
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(Config(stage: "foundation-only", ready: true),
                    _ => { sceneChecked = true; return true; }));

            Assert.That(error.Message, Does.Contain("complete-game"));
            Assert.That(sceneChecked, Is.False);
        }

        [TestCase("unknown")]
        [TestCase("Complete-Game")]
        [TestCase("")]
        public void UnknownStage_IsNotImplicitlyReleaseReady(string stage)
        {
            Assert.Throws<BuildFailedException>(() => NativeReleaseGate.ValidateConfig(Config(stage: stage), _ => true));
        }

        [Test]
        public void CompleteStageWithReadyFalse_IsStillBlocked()
        {
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(Config(ready: false), _ => true));

            Assert.That(error.Message, Does.Contain("readyForStore"));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void UnsupportedSchema_IsRejected(int schema)
        {
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(Config(schema: schema), _ => true));

            Assert.That(error.Message, Does.Contain("schemaVersion"));
        }

        [TestCase("Assets/Scenes/SampleScene.unity")]
        [TestCase("Assets/_Project/Scenes/Other.unity")]
        [TestCase("Assets/_Project/Scenes/main.unity")]
        public void DifferentMainPath_IsRejectedEvenIfSceneExists(string main)
        {
            bool sceneChecked = false;
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(Config(main: main), _ => { sceneChecked = true; return true; }));

            Assert.That(error.Message, Does.Contain("exactly"));
            Assert.That(sceneChecked, Is.False);
        }

        [Test]
        public void MissingOrInvalidMainSceneAsset_IsRejected()
        {
            BuildFailedException error = Assert.Throws<BuildFailedException>(() =>
                NativeReleaseGate.ValidateConfig(Config(), _ => false));

            Assert.That(error.Message, Does.Contain("missing or invalid"));
        }

        [Test]
        public void CompleteReadyConfig_ChecksExactRealSceneAndIsAccepted()
        {
            string checkedPath = null;
            Assert.DoesNotThrow(() => NativeReleaseGate.ValidateConfig(Config(), path =>
            {
                checkedPath = path;
                return path == NativeReleaseGate.MainScenePath;
            }));

            Assert.That(checkedPath, Is.EqualTo("Assets/_Project/Scenes/Main.unity"));
        }

        [TestCase(BuildTarget.Android)]
        [TestCase(BuildTarget.iOS)]
        public void NonDevelopmentMobilePlayer_RequiresReleaseValidation(BuildTarget target)
        {
            Assert.That(NativeReleaseGate.RequiresReleaseValidation(target, BuildOptions.None), Is.True);
        }

        [TestCase(BuildTarget.Android)]
        [TestCase(BuildTarget.iOS)]
        public void TestAssemblyFlagAlone_CannotBypassNonDevelopmentRelease(BuildTarget target)
        {
            Assert.That(NativeReleaseGate.RequiresReleaseValidation(target, BuildOptions.IncludeTestAssemblies), Is.True);
        }

        [TestCase(BuildTarget.Android, BuildOptions.Development)]
        [TestCase(BuildTarget.iOS, BuildOptions.Development)]
        [TestCase(BuildTarget.Android, BuildOptions.Development | BuildOptions.IncludeTestAssemblies)]
        public void DevelopmentAndTestPlayers_AreOutsideReleaseGate(BuildTarget target, BuildOptions options)
        {
            Assert.That(NativeReleaseGate.RequiresReleaseValidation(target, options), Is.False);
        }

        [TestCase(BuildTarget.StandaloneWindows64)]
        [TestCase(BuildTarget.StandaloneOSX)]
        [TestCase(BuildTarget.WebGL)]
        public void NonMobileBuilds_AreOutsideReleaseGate(BuildTarget target)
        {
            Assert.That(NativeReleaseGate.RequiresReleaseValidation(target, BuildOptions.None), Is.False);
        }

        [TestCase("schemaVersion")]
        [TestCase("sourceStage")]
        [TestCase("readyForStore")]
        [TestCase("mainScene")]
        public void RequiredFieldMissing_IsRejectedBeforeSceneAccess(string field)
        {
            string json = Config();
            string member = field == "schemaVersion" ? "\"schemaVersion\":1,"
                : field == "sourceStage" ? "\"sourceStage\":\"complete-game\","
                : field == "readyForStore" ? "\"readyForStore\":true,"
                : ",\"mainScene\":\"" + NativeReleaseGate.MainScenePath + "\"";
            bool sceneChecked = false;
            Assert.Throws<BuildFailedException>(() => NativeReleaseGate.ValidateConfig(json.Replace(member, ""),
                _ => { sceneChecked = true; return true; }));
            Assert.That(sceneChecked, Is.False);
        }

        [TestCase("\"schemaVersion\":1", "\"schemaVersion\":\"1\"")]
        [TestCase("\"schemaVersion\":1", "\"schemaVersion\":1.0")]
        [TestCase("\"schemaVersion\":1", "\"schemaVersion\":null")]
        [TestCase("\"readyForStore\":true", "\"readyForStore\":\"true\"")]
        [TestCase("\"readyForStore\":true", "\"readyForStore\":1")]
        [TestCase("\"readyForStore\":true", "\"readyForStore\":null")]
        [TestCase("\"sourceStage\":\"complete-game\"", "\"sourceStage\":null")]
        [TestCase("\"mainScene\":\"Assets/_Project/Scenes/Main.unity\"", "\"mainScene\":null")]
        [TestCase("\"readyForStore\":true", "\"readyForStore\":true,\"readyForStore\":true")]
        [TestCase("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")]
        [TestCase("\"readyForStore\":true", "\"readyForStore\":true,\"\\u0072eadyForStore\":true")]
        [TestCase("\"sourceStage\":\"complete-game\"", "\"sourceStage\":\"complete-game\",\"\\u0073ourceStage\":\"complete-game\"")]
        public void CoercibleWrongTypesAndDuplicates_AreRejectedBeforeSceneAccess(string member, string replacement)
        {
            bool sceneChecked = false;
            Assert.Throws<BuildFailedException>(() => NativeReleaseGate.ValidateConfig(Config().Replace(member, replacement),
                _ => { sceneChecked = true; return true; }));
            Assert.That(sceneChecked, Is.False);
        }

        [TestCase(" trailing")]
        [TestCase(" null")]
        [TestCase(" {}")]
        public void ContentAfterCompleteRoot_IsRejected(string suffix)
        {
            Assert.Throws<BuildFailedException>(() => NativeReleaseGate.ValidateConfig(Config() + suffix, _ => true));
        }

        [Test]
        public void NestedExtrasAndEscapedStringDelimiters_DoNotCauseFalseBoundaryFailure()
        {
            string json = Config().Insert(1, "\"extra\":{\"nested\":[\"} { ] \\\" escaped\",[]]},");
            Assert.DoesNotThrow(() => NativeReleaseGate.ValidateConfig(json, _ => true));
        }

        [Test]
        public void EscapedRequiredKey_HasItsDecodedNameAndTypeChecked()
        {
            string json = Config().Replace("\"readyForStore\"", "\"\\u0072eadyForStore\"");
            Assert.DoesNotThrow(() => NativeReleaseGate.ValidateConfig(json, _ => true));
        }
    }
}
