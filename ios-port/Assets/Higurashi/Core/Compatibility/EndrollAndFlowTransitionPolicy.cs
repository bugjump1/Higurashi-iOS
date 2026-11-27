using System;

namespace Higurashi.IOS.Compatibility
{
    public static class EndrollAndFlowTransitionPolicy
    {
        public const int MaxPostFadeBlackWaitMilliseconds = 1000;
        public const int PostFadeBreathMilliseconds = 1000;
        public const int RunawayEndrollWaitThresholdMilliseconds = 300_000;
        public const int PostScrollRunawayMarginMilliseconds = 5000;
        public const float TitleInputCooldownSeconds = 0.6f;

        public static bool IsEndrollScript(string scriptName)
        {
            return !string.IsNullOrWhiteSpace(scriptName) &&
                   scriptName.IndexOf("endroll_staff", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int ResolveEndrollWaitMilliseconds(
            string scriptName,
            int requestedMilliseconds,
            bool endrollLayerShown,
            bool hasVisibleTargetEndrollLayer,
            int remainingEndrollTransitionMilliseconds)
        {
            if (requestedMilliseconds <= 0 || !IsEndrollScript(scriptName) || !endrollLayerShown)
            {
                return requestedMilliseconds;
            }

            var remaining = Math.Max(0, remainingEndrollTransitionMilliseconds);

            // Guard against upstream end-roll typos such as EP08's Wait(1850000)
            // (~30.8 minutes) while preserving the full active scroll duration.
            if (requestedMilliseconds > RunawayEndrollWaitThresholdMilliseconds)
            {
                return Math.Min(requestedMilliseconds, remaining + PostScrollRunawayMarginMilliseconds);
            }

            // Preserve the script's original wait verbatim while a credits bitmap
            // is meant to remain visible (scrolling or holding the final frame).
            if (hasVisibleTargetEndrollLayer)
            {
                return requestedMilliseconds;
            }

            // Credits have faded (or are fading) to black: wait for any in-flight
            // fade-out plus a short beat so the next page arrives automatically
            // instead of sitting on a 10+ second black screen that looks stuck.
            var cap = remaining > 0
                ? remaining + PostFadeBreathMilliseconds
                : MaxPostFadeBlackWaitMilliseconds;
            return Math.Min(requestedMilliseconds, cap);
        }

        public static bool ShouldRedirectUnexpectedChapterPreviewToTitle(
            string scriptName,
            string sectionName,
            bool chapterPreviewVisible,
            bool chapterPreviewAuthorized,
            bool tipReading)
        {
            if (chapterPreviewAuthorized || tipReading)
            {
                return false;
            }

            if (chapterPreviewVisible)
            {
                return true;
            }

            return string.Equals(scriptName, "flow", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(sectionName, "ChapterPreview", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTitleInteractionReady(bool titleVisible, float elapsedSinceTitleShownSeconds)
        {
            return titleVisible && elapsedSinceTitleShownSeconds >= TitleInputCooldownSeconds;
        }
    }
}
