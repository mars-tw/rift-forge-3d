using System;
using System.IO;
using NUnit.Framework;
using RiftForge.Editor;
using UnityEditor;
using UnityEditor.Android;

namespace RiftForge.Tests.EditMode
{
    public sealed class RiftDevelopmentBuildJdkTests
    {
        private const string EmbeddedPreferenceKey = "JdkUseEmbedded";
        // The pinned Unity 6000.5 AndroidJavaRoot stores its JDK 17 path here.
        private const string DirectoryPreferenceKey = "Jdk17Path";

        [Test]
        public void MissingEmbeddedPreference_RemainsAbsentAfterSettingAnExplicitPath()
        {
            WithPreservedUserPreferences(() =>
            {
                AndroidExternalToolsSettings.jdkRootPath = string.Empty;
                string expectedPath = AndroidExternalToolsSettings.jdkRootPath;
                EditorPrefs.DeleteKey(EmbeddedPreferenceKey);

                using (new RiftDevelopmentBuild.AndroidJdkPreferenceScope())
                {
                    AndroidExternalToolsSettings.jdkRootPath = expectedPath;
                    Assert.That(EditorPrefs.HasKey(EmbeddedPreferenceKey), Is.True);
                    Assert.That(EditorPrefs.GetBool(EmbeddedPreferenceKey), Is.False);
                }

                Assert.That(EditorPrefs.HasKey(EmbeddedPreferenceKey), Is.False);
                Assert.That(Normalize(AndroidExternalToolsSettings.jdkRootPath), Is.EqualTo(Normalize(expectedPath)));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingEmbeddedPreference_IsRestoredWhenTheScopedOperationThrows(bool useEmbedded)
        {
            WithPreservedUserPreferences(() =>
            {
                string embeddedPath = Path.Combine(EditorApplication.applicationContentsPath,
                    "PlaybackEngines", "AndroidPlayer", "OpenJDK");
                AndroidExternalToolsSettings.jdkRootPath = useEmbedded ? string.Empty : embeddedPath;
                EditorPrefs.SetBool(EmbeddedPreferenceKey, useEmbedded);
                string expectedPath = AndroidExternalToolsSettings.jdkRootPath;

                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (new RiftDevelopmentBuild.AndroidJdkPreferenceScope())
                    {
                        AndroidExternalToolsSettings.jdkRootPath = useEmbedded ? embeddedPath : string.Empty;
                        throw new InvalidOperationException("Simulated build operation failure.");
                    }
                });

                Assert.That(EditorPrefs.HasKey(EmbeddedPreferenceKey), Is.True);
                Assert.That(EditorPrefs.GetBool(EmbeddedPreferenceKey), Is.EqualTo(useEmbedded));
                Assert.That(Normalize(AndroidExternalToolsSettings.jdkRootPath), Is.EqualTo(Normalize(expectedPath)));
            });
        }

        private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        private static void WithPreservedUserPreferences(Action operation)
        {
            // Capture real user storage before the public path getter can
            // normalize it. Assertions run before this independent cleanup.
            bool hadEmbedded = EditorPrefs.HasKey(EmbeddedPreferenceKey);
            bool originalEmbedded = EditorPrefs.GetBool(EmbeddedPreferenceKey, true);
            bool hadDirectory = EditorPrefs.HasKey(DirectoryPreferenceKey);
            string originalDirectory = EditorPrefs.GetString(DirectoryPreferenceKey);
            string originalPath = null;
            try
            {
                originalPath = AndroidExternalToolsSettings.jdkRootPath;
                operation();
            }
            finally
            {
                try
                {
                    if (originalPath != null)
                    {
                        AndroidExternalToolsSettings.jdkRootPath = originalPath;
                        if (originalEmbedded)
                            AndroidExternalToolsSettings.jdkRootPath = string.Empty;
                    }
                }
                finally
                {
                    if (hadEmbedded)
                        EditorPrefs.SetBool(EmbeddedPreferenceKey, originalEmbedded);
                    else
                        EditorPrefs.DeleteKey(EmbeddedPreferenceKey);
                    if (hadDirectory)
                        EditorPrefs.SetString(DirectoryPreferenceKey, originalDirectory);
                    else
                        EditorPrefs.DeleteKey(DirectoryPreferenceKey);
                }
            }
        }
    }
}
