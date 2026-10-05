using System;
using System.Globalization;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// Resolves the aspect ratio of the PC presentation canvas.
    /// The script-declared aspect belongs to the whole composition; a background
    /// texture's aspect is only a fallback when that declaration is unavailable.
    /// </summary>
    public static class PresentationAspectPolicy
    {
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
