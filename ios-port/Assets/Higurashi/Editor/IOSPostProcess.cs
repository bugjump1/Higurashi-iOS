#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Higurashi.IOS.Editor
{
    public static class IOSPostProcess
    {
        [PostProcessBuild(999)]
        public static void ApplyIOSSettings(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            UpdateInfoPlist(buildPath);
            UpdateXcodeProject(buildPath);
        }

        private static void UpdateInfoPlist(string buildPath)
        {
            var plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            plist.root.SetBoolean("UIRequiresFullScreen", true);
            plist.root.SetString("MinimumOSVersion", "15.0");
            plist.root.SetString("CFBundleDisplayName", PlayerSettings.productName);
            plist.root.SetString("CFBundleName", PlayerSettings.productName);
            plist.WriteToFile(plistPath);
        }

        private static void UpdateXcodeProject(string buildPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();

            SetCommonBuildProperties(project, mainTarget);
            SetCommonBuildProperties(project, frameworkTarget);
            project.SetBuildProperty(mainTarget, "TARGETED_DEVICE_FAMILY", "1,2");

            project.WriteToFile(projectPath);
        }

        private static void SetCommonBuildProperties(PBXProject project, string targetGuid)
        {
            project.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
            project.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
        }
    }
}
#endif
