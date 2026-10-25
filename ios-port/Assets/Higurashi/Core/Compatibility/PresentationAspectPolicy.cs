using System;
using System.Globalization;
using System.IO;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// Resolves the presentation canvas aspect and legacy style/mode declarations.
    /// The runtime canvas aspect now follows the actual scene texture (D10): every
    /// background aspect-fits the screen by its own ratio, and black/white scene
    /// fills keep the previous real background's frame. Style-driven methods and
    /// Resolve remain for settings compatibility tests and declared-aspect paths.
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

        /// <summary>
        /// Full-frame scene fills such as black/white have no meaningful texture
        /// aspect; they must never drive the presentation canvas ratio.
        /// </summary>
        public static bool IsFullFrameSceneName(string textureName)
        {
            var name = Path.GetFileNameWithoutExtension(textureName ?? string.Empty);
            return string.Equals(name, "black", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "white", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Canvas aspect for the active scene: the current background texture's
        /// own ratio, falling back to the previous real background (so black/white
        /// fills keep the frame stable) and finally to 16:9.
        /// </summary>
        public static float AspectForSceneTextures(
            string currentName, int currentWidth, int currentHeight,
            string previousName, int previousWidth, int previousHeight)
        {
            var aspect = RealTextureAspect(currentName, currentWidth, currentHeight);
            if (aspect > 0f)
            {
                return aspect;
            }

            aspect = RealTextureAspect(previousName, previousWidth, previousHeight);
            if (aspect > 0f)
            {
                return aspect;
            }

            return 16f / 9f;
        }

        /// <summary>
        /// Uniform scale that keeps a presentation layer inside the canvas frame
        /// without cropping: oversized layers shrink proportionally (never enlarge,
        /// never stretch), so CG-fallback-only wide assets stay fully visible in a
        /// 4:3 frame.
        /// </summary>
        public static float LayerFitScale(float width, float height,
            float frameWidth, float frameHeight)
        {
            if (width <= 0f || height <= 0f || frameWidth <= 0f || frameHeight <= 0f)
            {
                return 1f;
            }

            var widthScale = frameWidth / width;
            var heightScale = frameHeight / height;
            var scale = widthScale < heightScale ? widthScale : heightScale;
            return scale < 1f ? scale : 1f;
        }

        private static float RealTextureAspect(string name, int width, int height)
        {
            if (IsFullFrameSceneName(name) || width <= 0 || height <= 0)
            {
                return 0f;
            }

            return (float)width / height;
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
