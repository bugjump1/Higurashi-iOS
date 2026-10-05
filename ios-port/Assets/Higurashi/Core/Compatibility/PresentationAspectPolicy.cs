using System;
using System.Globalization;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// Resolves the user-facing presentation policy and legacy aspect declarations.
    /// Runtime display mode is now derived from the selected background style;
    /// Resolve remains for compatibility tests and older declared-aspect paths.
    /// </summary>
    public static class PresentationAspectPolicy
    {
        public static MobilePresentationMode ModeForBackgroundStyle(int backgroundStyleIndex)
        {
            return backgroundStyleIndex == 1
                ? MobilePresentationMode.OriginalFourByThree
                : MobilePresentationMode.Fit;
        }

        public static float AspectForBackgroundStyle(int backgroundStyleIndex)
        {
            return backgroundStyleIndex == 1 ? 4f / 3f : 16f / 9f;
        }

        public static float LogicalCanvasWidthForBackgroundStyle(int backgroundStyleIndex)
        {
            return 480f * AspectForBackgroundStyle(backgroundStyleIndex);
        }

        public static float Resolve(string declaredAspect, float backgroundWidth,
            float backgroundHeight)
        {
            if (TryParse(declaredAspect, out var ratio))
            {
                return ratio;
            }

            if (backgroundWidth > 0f && backgroundHeight > 0f)
            {
                return backgroundWidth / backgroundHeight;
            }

            return 16f / 9f;
        }

        private static bool TryParse(string value, out float ratio)
        {
            if (!float.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out ratio) || ratio <= 0f)
            {
                ratio = 0f;
                return false;
            }

            if (ratio < 1f)
            {
                ratio = 1f / ratio;
            }

            return true;
        }
    }
}
