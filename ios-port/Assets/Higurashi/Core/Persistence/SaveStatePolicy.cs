using System;

namespace Higurashi.IOS.Persistence
{
    public enum SaveSurface
    {
        Story,
        Title,
        Credits,
        Movie,
        Choice,
        ChapterPreview,
        TipsChapter,
        TipsList,
        TipReading,
        FragmentChapter,
        FragmentList,
        FragmentReading,
        BonusContent,
        Faulted,
        Completed
    }

    public static class SaveStatePolicy
    {
        public static bool CanWriteRegularSave(
            SaveSurface surface,
            bool savingEnabled,
            bool interfaceEnabled)
        {
            return (surface == SaveSurface.Story ||
                    surface == SaveSurface.FragmentChapter ||
                    surface == SaveSurface.FragmentList ||
                    surface == SaveSurface.FragmentReading ||
                    surface == SaveSurface.BonusContent) &&
                   savingEnabled && interfaceEnabled;
        }

        public static bool IsRecoverableStorySave(
            SaveSurface surface,
            bool savingEnabled,
            bool interfaceEnabled)
        {
            return savingEnabled && interfaceEnabled &&
                   (surface == SaveSurface.Story ||
                    surface == SaveSurface.Choice ||
                    surface == SaveSurface.TipsChapter ||
                    surface == SaveSurface.FragmentChapter ||
                    surface == SaveSurface.FragmentList ||
                    surface == SaveSurface.FragmentReading ||
                    surface == SaveSurface.BonusContent);
        }

        public static bool IsKnownLegacyTipsBrowserSave(string script, string summary)
        {
            return string.Equals(script, "flow", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrEmpty(summary) &&
                   summary.IndexOf("OP 动画中包含剧透", StringComparison.Ordinal) >= 0;
        }
    }
}
