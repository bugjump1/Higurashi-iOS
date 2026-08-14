using System;
using System.IO;
using Higurashi.IOS.Compatibility;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Higurashi.IOS.Editor
{
    public static class IOSBuild
    {
        private const string GeneratedScenePath = "Assets/Generated/Bootstrap.unity";
        private const string AppIconPath = "Assets/Branding/AppIcon.png";
        public static void Build()
        {
            ConfigurePlayer();
            EnsureBootstrapScene();

            var outputPath = GetArgument("-customBuildPath");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "iOS"));
            }

            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            Directory.CreateDirectory(outputPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GeneratedScenePath },
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unity iOS export failed: " + report.summary.result +
                    ", errors=" + report.summary.totalErrors);
            }

            Debug.Log("iOS Xcode project exported to " + outputPath);
        }

        private static void ConfigurePlayer()
        {
            var chapterArgument = GetArgument("-chapterNumber");
            if (!int.TryParse(chapterArgument, out var chapterNumber))
            {
                chapterNumber = 1;
            }
            var profile = HigurashiChapterProfiles.ForEpisode(chapterNumber);
            var bundleIdentifier = GetArgument("-bundleIdentifier");
            if (string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                bundleIdentifier = profile.BundleIdentifier;
            }

            var buildNumber = GetArgument("-buildNumber");
            if (string.IsNullOrWhiteSpace(buildNumber))
            {
                buildNumber = "1";
            }

            var appVersion = GetArgument("-appVersion");
            if (string.IsNullOrWhiteSpace(appVersion))
            {
                appVersion = "0.1.0";
            }
            if (!IsValidAppVersion(appVersion))
            {
                throw new ArgumentException(
                    "Invalid -appVersion value '" + appVersion +
                    "'. Use three numeric components such as 0.9.0.");
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            PlayerSettings.companyName = "Personal Research";
            PlayerSettings.productName = profile.ProductName;
            PlayerSettings.bundleVersion = appVersion;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.buildNumber = buildNumber;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            ConfigureAppIcon();
        }

        private static bool IsValidAppVersion(string value)
        {
            var components = value.Split('.');
            if (components.Length != 3)
            {
                return false;
            }

            foreach (var component in components)
            {
                if (component.Length == 0)
                {
                    return false;
                }
                foreach (var character in component)
                {
                    if (character < '0' || character > '9')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ConfigureAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
            {
                throw new FileNotFoundException("The extracted Higurashi app icon is missing.", AppIconPath);
            }

            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application);
            var icons = new Texture2D[sizes.Length];
            for (var i = 0; i < icons.Length; i++)
            {
                icons[i] = icon;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.iOS, icons, IconKind.Application);
        }

        private static void EnsureBootstrapScene()
        {
            var directory = Path.GetDirectoryName(GeneratedScenePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, GeneratedScenePath))
            {
                throw new IOException("Unable to save generated bootstrap scene.");
            }

            AssetDatabase.Refresh();
        }

        private static string GetArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }
    }
}
