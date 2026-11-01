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
        /// Shared title/decoration layers that exist only as 16:9 console art
        /// (no 4:3 OG variant): the EP5-8 main-menu title logo and its companion
        /// banner, the chapter-preview logo, and full-screen text overlays.
        /// Inside a 4:3 original-style frame these 853-logical-px-wide layers
        /// would overflow and clip their right-edge lettering (the EP5-8 title
        /// and chapter-preview truncation), so they shrink uniformly instead -
        /// the PC 4:3-mode letterbox behavior for non-sprite CG. Character
        /// sprites (sprite//portrait/) are deliberately NOT in this list: they
        /// keep the PC full-height, viewport-clipped presentation.
        /// </summary>
        public static bool IsSharedDecorationLayer(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return false;
            }

            var name = Path.GetFileNameWithoutExtension(textureName);
            if (string.Equals(name, "titlelogo", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("sgtitle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalized = textureName.Replace('\\', '/');
            return normalized.StartsWith("scenario/logo", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Uniform shrink that keeps a shared decoration layer fully inside the
        /// canvas frame (never enlarges, never stretches). Layers outside the
        /// decoration list return 1 so their presentation is unchanged.
        /// </summary>
        public static float DecorationLayerFitScale(
            string textureName, float width, float height,
            float frameWidth, float frameHeight)
        {
            if (!IsSharedDecorationLayer(textureName) ||
                width <= 0f || height <= 0f || frameWidth <= 0f || frameHeight <= 0f)
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
