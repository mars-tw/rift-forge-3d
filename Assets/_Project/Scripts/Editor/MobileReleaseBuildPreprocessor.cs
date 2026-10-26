using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace RiftForge.Editor
{
    /// <summary>Applies the release contract to direct non-development mobile player builds.</summary>
    public sealed class MobileReleaseBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (NativeReleaseGate.RequiresReleaseValidation(report.summary.platform, report.summary.options))
            {
                NativeReleaseGate.ValidateCurrentProject();
            }
        }
    }
}
