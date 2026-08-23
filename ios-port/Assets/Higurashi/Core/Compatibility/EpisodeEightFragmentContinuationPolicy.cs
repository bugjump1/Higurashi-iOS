using Higurashi.IOS.Buriko;

namespace Higurashi.IOS.Compatibility
{
    public static class EpisodeEightFragmentContinuationPolicy
    {
        // flow.Game resumes at _mats_009 after the fragment loop has returned.
        public const int ResumeStoryJumpValue = 9;

        public static bool ShouldRecoverFromUnexpectedExit(
            int episodeNumber,
            int activeFragmentId,
            int fragmentLoop,
            int fragment51Read,
            bool titleVisible,
            bool runtimeCompleted)
        {
            return episodeNumber == 8 &&
                   activeFragmentId == 50 &&
                   fragmentLoop == 0 &&
                   fragment51Read != 0 &&
                   (titleVisible || runtimeCompleted);
        }

        public static bool HasReachedStoryContinuation(
            int episodeNumber,
            int activeFragmentId,
            int fragmentLoop,
            string currentScriptName)
        {
            return episodeNumber == 8 &&
                   activeFragmentId == 50 &&
                   fragmentLoop == 0 &&
                   string.Equals(currentScriptName, "_mats_009",
                       System.StringComparison.OrdinalIgnoreCase);
        }

        // Earlier manually generated fragment saves can omit init.txt's globals.
        // Restore only that known incomplete EP08 fragment state and leave normal
        // saves, including users' display and audio preferences, untouched.
        public static bool RestoreMissingFragmentDefaults(int episodeNumber, BurikoMemory memory)
        {
            if (episodeNumber != 8 || memory == null ||
                memory.GetLocalFlag("LFragmentLoop") == 0 ||
                memory.HasGlobalFlag("GADVMode"))
            {
                return false;
            }

            memory.SetGlobalFlag("GADVMode", 1);
            memory.SetGlobalFlag("GLinemodeSp", 0);
            memory.SetGlobalFlag("GWindowOpacity", 50);
            memory.SetGlobalFlag("GVoiceVolume", 75);
            memory.SetGlobalFlag("GBGMVolume", 50);
            memory.SetGlobalFlag("GSEVolume", 50);
            memory.SetGlobalFlag("GLanguage", 0);
            memory.SetGlobalFlag("GMOD_SETTING_LOADER", 3);
            return true;
        }
    }
}
