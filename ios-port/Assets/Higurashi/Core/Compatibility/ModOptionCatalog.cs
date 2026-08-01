using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Compatibility
{
    public enum MobilePresentationMode
    {
        OriginalFourByThree = 0,
        Fit,
        Fill
    }

    [Serializable]
    public sealed class HigurashiUserSettings
    {
        public int artSetIndex = 1;
        public int audioPresetIndex;
        public int censorshipLevel = 2;
        public int voiceVolume = 75;
        public int windowOpacity = 50;
        public int textSpeed = 50;
        public int autoSpeed = 50;
        public int renderQuality = 2;
        public bool lipSync = true;
        public bool skipUnread;
        public MobilePresentationMode presentationMode = MobilePresentationMode.Fit;
    }

    public sealed class PathCascadeDefinition
    {
        public PathCascadeDefinition(string name, params string[] folders)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Folders = folders ?? throw new ArgumentNullException(nameof(folders));
        }

        public string Name { get; }
        public IReadOnlyList<string> Folders { get; }
    }

    public static class ModOptionCatalog
    {
        private static readonly PathCascadeDefinition[] OnikakushiArtSets =
        {
            new PathCascadeDefinition("Console", "CG"),
            new PathCascadeDefinition("Remake", "CGAlt", "CG"),
            new PathCascadeDefinition("Original", "OGBackgrounds", "OGSprites", "CG")
        };

        private static readonly PathCascadeDefinition[] OnikakushiBgmSets =
        {
            new PathCascadeDefinition("New MangaGamer (2019)", "April2019BGM", "BGM"),
            new PathCascadeDefinition("GIN / Hou BGM (2014)", "OGBGM", "BGM"),
            new PathCascadeDefinition("Hou+ Demo (2020)", "HouPlusDemoBGM", "BGM"),
            new PathCascadeDefinition("Hou+ BGM (2022)", "HouPlusBGM", "BGM")
        };

        public static IReadOnlyList<PathCascadeDefinition> ArtSets => OnikakushiArtSets;
        public static IReadOnlyList<PathCascadeDefinition> BgmSets => OnikakushiBgmSets;
    }
}
