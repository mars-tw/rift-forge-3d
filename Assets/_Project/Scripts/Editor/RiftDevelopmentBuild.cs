using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;

[assembly: InternalsVisibleTo("RiftForge.Tests.EditMode")]

namespace RiftForge.Editor
{
    /// <summary>Local development QA builds. Android APKs use debug signing when no custom keystore is configured.</summary>
    public static class RiftDevelopmentBuild
    {
        private const string MainScene = NativeReleaseGate.MainScenePath;
        private const string AppId = "tw.mars.riftforge3d";

        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, NamedBuildTarget.Standalone,
                "Builds/Validation/RiftForge-dev.exe");
        }

        public static void Android()
        {
            Build(BuildTarget.Android, NamedBuildTarget.Android,
                "Builds/Validation/RiftForge-dev.apk");
        }

        public static void AndroidShaderFixQA()
        {
            Build(BuildTarget.Android, NamedBuildTarget.Android,
                "Builds/Validation/RiftForge-dev-shader-fix.apk");
        }

        public static void AndroidPrimitiveFixQA()
        {
            Build(BuildTarget.Android, NamedBuildTarget.Android,
                "Builds/Validation/RiftForge-dev-primitive-fix.apk");
        }

        public static void IOS()
        {
            Build(BuildTarget.iOS, NamedBuildTarget.iOS,
                "Builds/Validation/RiftForge-iOS-dev");
        }

        private static void Build(BuildTarget target, NamedBuildTarget namedTarget, string relativeOutput)
        {
            if (!UnityEngine.Application.isBatchMode)
                throw new BuildFailedException("Development builds require batch mode.");
            if (!File.Exists(MainScene))
                throw new BuildFailedException("Playable Main.unity is missing.");

            string output = Path.GetFullPath(relativeOutput);
            if (File.Exists(output) || Directory.Exists(output))
                throw new BuildFailedException("Validation output already exists: " + output);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException());

            string originalId = PlayerSettings.GetApplicationIdentifier(namedTarget);
            bool originalAppBundle = EditorUserBuildSettings.buildAppBundle;
            bool originalAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            AndroidSdkVersions originalMinSdk = PlayerSettings.Android.minSdkVersion;
            AndroidArchitecture originalArchitectures = PlayerSettings.Android.targetArchitectures;
            AndroidJdkPreferenceScope originalJdk = target == BuildTarget.Android
                ? new AndroidJdkPreferenceScope()
                : null;
            try
            {
                PlayerSettings.SetApplicationIdentifier(namedTarget, AppId);
                if (target == BuildTarget.Android)
                {
                    string embeddedJdk = Path.Combine(EditorApplication.applicationContentsPath,
                        "PlaybackEngines", "AndroidPlayer", "OpenJDK");
                    if (!File.Exists(Path.Combine(embeddedJdk, "bin", "java.exe")))
                        throw new BuildFailedException("Unity Android OpenJDK is missing: " + embeddedJdk);
                    AndroidExternalToolsSettings.jdkRootPath = embeddedJdk;
                    EditorUserBuildSettings.buildAppBundle = false;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                }

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene },
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Development build failed: " + report.summary.result);
                UnityEngine.Debug.Log("RIFT_DEV_BUILD_OK " + target + " " + output);
            }
            finally
            {
                try
                {
                    originalJdk?.Dispose();
                }
                finally
                {
                    PlayerSettings.SetApplicationIdentifier(namedTarget, originalId);
                    EditorUserBuildSettings.buildAppBundle = originalAppBundle;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = originalAndroidProject;
                    PlayerSettings.Android.minSdkVersion = originalMinSdk;
                    PlayerSettings.Android.targetArchitectures = originalArchitectures;
                }
            }
        }

        internal sealed class AndroidJdkPreferenceScope : IDisposable
        {
            // Unity 6000.5 AndroidJavaRoot uses this logical EditorPrefs key.
            // EditorPrefs handles the platform-specific storage name and location.
            private const string EmbeddedPreferenceKey = "JdkUseEmbedded";
            private readonly bool hadEmbeddedPreference;
            private readonly bool originalUseEmbedded;
            private readonly string originalPath;
            private bool disposed;

            internal AndroidJdkPreferenceScope()
            {
                // The path getter may normalize Android tool settings. Capture
                // preference presence and value before asking it for a path.
                hadEmbeddedPreference = EditorPrefs.HasKey(EmbeddedPreferenceKey);
                originalUseEmbedded = EditorPrefs.GetBool(EmbeddedPreferenceKey, true);
                try
                {
                    originalPath = AndroidExternalToolsSettings.jdkRootPath;
                }
                catch
                {
                    RestoreEmbeddedPreference();
                    throw;
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                try
                {
                    AndroidExternalToolsSettings.jdkRootPath = originalPath;
                    // A nonempty path selects custom mode even when it equals
                    // Unity's embedded JDK. The empty-path setter restores the
                    // embedded mode in Unity's live Android tool-settings cache.
                    if (originalUseEmbedded)
                        AndroidExternalToolsSettings.jdkRootPath = string.Empty;
                }
                finally
                {
                    disposed = true;
                    RestoreEmbeddedPreference();
                }
            }

            private void RestoreEmbeddedPreference()
            {
                if (hadEmbeddedPreference)
                    EditorPrefs.SetBool(EmbeddedPreferenceKey, originalUseEmbedded);
                else
                    EditorPrefs.DeleteKey(EmbeddedPreferenceKey);
            }
        }
    }
}
