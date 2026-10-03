using System;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// Mirrors PC 07th-Mod SceneController.MODSkipImage: while the GHideCG flag
    /// is 1, console event CGs are skipped entirely so the previously drawn
    /// background stays on screen. A draw is only skipped when the texture
    /// actually resolves into the "CG" cascade folder AND its script name
    /// starts with the "scene/" prefix — OG assets, backgrounds, masks and
    /// every non-scene name keep drawing normally.
    /// </summary>
    public static class HideConsoleCgPolicy
    {
        public const string ConsoleFolder = "CG";
        public const string ScenePrefix = "scene/";

        public static bool ShouldSkip(string resolvedFolder, string textureName, int hideCg)
        {
            if (hideCg != 1)
            {
                return false;
            }

            if (string.IsNullOrEmpty(textureName) ||
                !textureName.StartsWith(ScenePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(resolvedFolder, ConsoleFolder, StringComparison.Ordinal);
        }
    }
}
