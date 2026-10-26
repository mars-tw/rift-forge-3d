using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RiftForge.Editor
{
    /// <summary>Batch-only non-Development Windows player for native-exit contrast.</summary>
    public static class RiftWindowsReleaseBuild
    {
        public const string RequiredUnityVersion = "6000.5.4f1";
        public const string Version = "1.3.1";
        public const int Build = 5;
        public const string OutputPath = "Builds/Release/RiftForge.exe";
        private const string ExpectedSdkRuntimeSha256 = "03c2c29cf777f14e7512b491f14af1af570e59cd3fd1f23dffc19bfbe791a248";

        [Serializable]
        private sealed class BuildConfig
        {
            public string editorVersion;
            public string nativeVersion;
            public int nativeBuild;
            public string mainScene;
            public bool readyForStore;
        }

        public static void Windows()
        {
            if (!Application.isBatchMode)
                throw new BuildFailedException("RiftWindowsReleaseBuild requires batch mode.");
            if (!string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
                throw new BuildFailedException("Use exactly Unity " + RequiredUnityVersion + ".");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows Standalone Build Support is unavailable.");

            string scene = NativeReleaseGate.MainScenePath;
            if (!string.Equals(scene, "Assets/_Project/Scenes/Main.unity", StringComparison.Ordinal)
                || scene.IndexOf("SampleScene", StringComparison.OrdinalIgnoreCase) >= 0
                || !File.Exists(scene)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                throw new BuildFailedException("The exact playable Main.unity scene asset is required; SampleScene is forbidden.");
            if (NativeReleaseGate.RequiresReleaseValidation(BuildTarget.StandaloneWindows64, BuildOptions.None))
                throw new BuildFailedException("Windows BuildOptions.None must remain outside the mobile release gate.");

            BuildConfig config = JsonUtility.FromJson<BuildConfig>(File.ReadAllText(NativeReleaseGate.ConfigRelativePath));
            if (config == null
                || !string.Equals(config.editorVersion, RequiredUnityVersion, StringComparison.Ordinal)
                || !string.Equals(config.nativeVersion, Version, StringComparison.Ordinal)
                || config.nativeBuild != Build
                || !string.Equals(config.mainScene, scene, StringComparison.Ordinal))
                throw new BuildFailedException("native/build-config.json does not match the Windows R7 version contract.");
            if (!string.Equals(PlayerSettings.bundleVersion, Version, StringComparison.Ordinal)
                || PlayerSettings.Android.bundleVersionCode != Build
                || !string.Equals(PlayerSettings.iOS.buildNumber, Build.ToString(), StringComparison.Ordinal)
                || !string.Equals(NativeExport.Version, Version, StringComparison.Ordinal)
                || NativeExport.Build != Build)
                throw new BuildFailedException("ProjectSettings, NativeExport and native config must all be version 1.3.1 / build 5.");

            string output = Path.GetFullPath(OutputPath);
            string outputRoot = Path.GetDirectoryName(output) ?? throw new BuildFailedException("Release output parent is invalid.");
            string dataDirectory = Path.Combine(outputRoot, "RiftForge_Data");
            string runtime = Path.Combine(outputRoot, "UnityPlayer.dll");
            if (File.Exists(output) || File.Exists(runtime) || Directory.Exists(dataDirectory))
                throw new BuildFailedException("Choose a fresh Builds/Release output; existing player files will not be overwritten.");
            Directory.CreateDirectory(outputRoot);

            string sdkRuntime = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "WindowsStandaloneSupport", "Variations",
                "win64_player_nondevelopment_mono", "UnityPlayer.dll");
            if (!File.Exists(sdkRuntime) || !string.Equals(Sha256(sdkRuntime), ExpectedSdkRuntimeSha256, StringComparison.Ordinal))
                throw new BuildFailedException("Installed SDK non-Development x64 Mono UnityPlayer.dll does not match the reviewed Unity 6000.5.4f1 runtime.");

            const BuildOptions options = BuildOptions.None;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded
                || report.summary.options != BuildOptions.None
                || (report.summary.options & BuildOptions.Development) != 0)
                throw new BuildFailedException("Non-Development Windows build failed or returned unexpected BuildOptions.");
            if (!File.Exists(runtime) || !string.Equals(Sha256(runtime), ExpectedSdkRuntimeSha256, StringComparison.Ordinal))
                throw new BuildFailedException("Built UnityPlayer.dll is not the exact installed non-Development SDK runtime.");

            string outputRuntimeSha = Sha256(runtime);
            File.WriteAllText(Path.Combine(outputRoot, "RiftForge-release-build.json"),
                "{\"status\":\"WINDOWS_NONDEVELOPMENT_BUILD_SUCCEEDED\","
                + "\"unityVersion\":\"" + Application.unityVersion + "\","
                + "\"version\":\"" + Version + "\",\"build\":" + Build + ","
                + "\"target\":\"StandaloneWindows64\",\"buildOptions\":\"None\","
                + "\"development\":false,\"readyForStore\":" + config.readyForStore.ToString().ToLowerInvariant() + ","
                + "\"sdkRuntimeSha256\":\"" + ExpectedSdkRuntimeSha256 + "\","
                + "\"outputRuntimeSha256\":\"" + outputRuntimeSha + "\"}\n");
            Debug.Log("RIFT_WINDOWS_RELEASE_BUILD_OK version=" + Version + " build=" + Build
                + " options=" + report.summary.options + " sdkRuntimeSha=" + ExpectedSdkRuntimeSha256
                + " outputRuntimeSha=" + outputRuntimeSha + " output=" + output);
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
