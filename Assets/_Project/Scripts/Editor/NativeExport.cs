using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RiftForge.Editor
{
    /// <summary>Batch-only, unsigned native project exports. Never exports SampleScene as a finished game.</summary>
    public static class NativeExport
    {
        private const string AppId = "tw.mars.riftforge3d";
        public const string Version = "1.3.1";
        public const int Build = 5;
        private const string ScenePath = NativeReleaseGate.MainScenePath;

        public static void Android()
        {
            Export(BuildTarget.Android, NamedBuildTarget.Android, "Android");
        }

        public static void IOS()
        {
            Export(BuildTarget.iOS, NamedBuildTarget.iOS, "iOS");
        }

        private static void Export(BuildTarget target, NamedBuildTarget namedTarget, string directory)
        {
            if (!Application.isBatchMode)
                throw new BuildFailedException("NativeExport requires batch mode; it does not modify a running Editor session.");
            if (Application.unityVersion != "6000.5.4f1")
                throw new BuildFailedException("Install the project's Unity 6000.5.4f1 Editor and target module first.");
            NativeReleaseGate.ValidateCurrentProject();
            if (!File.Exists("Assets/_Project/Scripts/Runtime/TouchPlayerInputSource.cs"))
                throw new BuildFailedException("TouchPlayerInputSource.cs is missing. File presence alone does not verify mobile input integration or testing.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("Install the matching Android/iOS Build Support module.");

            string root = Path.GetFullPath("Builds/Native");
            string output = Path.Combine(root, directory);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new BuildFailedException("Choose a fresh native export directory; existing outputs will not be erased.");
            Directory.CreateDirectory(output);
            PlayerSettings.SetApplicationIdentifier(namedTarget, AppId);
            PlayerSettings.companyName = "RiftForge";
            PlayerSettings.productName = "Rift Forge 3D";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = Build;
            PlayerSettings.iOS.buildNumber = Build.ToString();
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = target == BuildTarget.Android;
            EditorUserBuildSettings.buildAppBundle = target == BuildTarget.Android;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unsigned native project export failed; inspect the local Editor log.");
            File.WriteAllText(Path.Combine(root, directory + "-export.json"),
                "{\"appId\":\"" + AppId + "\",\"version\":\"" + Version + "\",\"build\":" + Build + "," +
                "\"target\":\"" + directory + "\",\"status\":\"PROJECT_EXPORTED\",\"signed\":false," +
                "\"compiledStoreBinary\":false,\"uploaded\":false}");
        }
    }
}
