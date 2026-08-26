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

    public enum MobileChoiceMode
    {
        NoAdditionalChoices = 0,
        AdditionalChoices = 1,
        AdditionalChoicesWithAnswer = 2
    }

    [Serializable]
    public sealed class HigurashiUserSettings
    {
        public int artSetIndex = 0;
        public int spriteStyleIndex = 0;
        public int backgroundStyleIndex = 0;
        public int audioPresetIndex;
        public int censorshipLevel = 2;
        public int bgmVolume = 100;
        public int voiceVolume = 75;
        public int windowOpacity = 50;
        public int textScale = 100;
        public int textSpeed = 50;
        public int autoSpeed = 50;
        public int renderQuality = 2;
        public bool lipSync = true;
        public bool autoSave = true;
        public bool skipUnread;
        public MobilePresentationMode presentationMode = MobilePresentationMode.Fit;
        public MobileChoiceMode choiceMode = MobileChoiceMode.NoAdditionalChoices;
    }

    public static class VisualStylePolicy
    {
        public const int ConsolePreset = 0;
        public const int RemakePreset = 1;
        public const int OriginalPreset = 2;
        public const int CustomPreset = -1;

        public static int PresetFor(int spriteStyleIndex, int backgroundStyleIndex)
        {
            if (spriteStyleIndex == ConsolePreset && backgroundStyleIndex == 0)
            {
                return ConsolePreset;
            }
            if (spriteStyleIndex == RemakePreset && backgroundStyleIndex == 0)
            {
                return RemakePreset;
            }
            if (spriteStyleIndex == OriginalPreset && backgroundStyleIndex == 1)
            {
                return OriginalPreset;
            }
            return CustomPreset;
        }

        public static void ApplyPreset(HigurashiUserSettings settings, int preset)
        {
            if (settings == null)
            {
                return;
            }

            switch (preset)
            {
                case ConsolePreset:
                    settings.spriteStyleIndex = ConsolePreset;
                    settings.backgroundStyleIndex = 0;
                    break;
                case RemakePreset:
                    settings.spriteStyleIndex = RemakePreset;
                    settings.backgroundStyleIndex = 0;
                    break;
                case OriginalPreset:
                    settings.spriteStyleIndex = OriginalPreset;
                    settings.backgroundStyleIndex = 1;
                    break;
                default:
                    ApplyPreset(settings, ConsolePreset);
                    break;
            }

            settings.artSetIndex = settings.spriteStyleIndex;
        }
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
