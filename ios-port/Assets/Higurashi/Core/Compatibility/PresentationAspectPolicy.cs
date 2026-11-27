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
        /// End-roll staff bitmaps (exported at 3x resolution and scrolled via
        /// top-left coordinates). Several live under text/ (e.g. text/meak_staff_01,
        /// text/tumi_staff_01, text/staff_maturi1a/1b/2), so they must be
        /// excluded from full-screen text/ decoration fitting.
        /// </summary>
        public static bool IsEndrollTexture(string textureName)
        {
            var name = Path.GetFileNameWithoutExtension(textureName ?? string.Empty);
            return name.StartsWith("tumi_staff", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("meak_staff", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("staff_mina", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("staff_maturi", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shared title/decoration layers that exist only as 16:9 console art
        /// (no 4:3 OG variant). Inside a 4:3 original-style frame these
        /// 853-logical-px-wide layers overflow the 640-px frame; how each one
        /// is brought back inside depends on where its artwork sits:
        /// <list type="bullet">
        /// <item>AnchorRight - top-right menu art (EP5-8 menu titlelogo and its
        /// sgtitle tree banner) keeps full size and pins its right edge to the
        /// frame's right edge, preserving 1:1 registration with the top-bleeding
        /// tree silhouette.</item>
        /// <item>FitBottomRight - the chapter-preview scenario/logo in the
        /// bottom-right shrinks uniformly to fit the frame and pins its right and
        /// bottom edges to the frame's bottom-right corner, keeping clear of both
        /// EP8's long description text above and the preview buttons on the left.</item>
        /// <item>ShiftLeft - EP8's localized scenario/stripes carries the
        /// translation credits at x 1245-1812 of 1920, past the 4:3 crop
        /// (240-1680); it shifts left just enough to show them.</item>
        /// <item>FitInsideFrame - centered full-screen art (non-endroll text/
        /// overlays, and the boot logo when it falls back to 16:9 console art)
        /// shrinks uniformly, the PC 4:3-mode letterbox behavior.</item>
        /// </list>
        /// Character sprites (sprite//portrait/), scene CG, choice artwork and
        /// scrolling end-roll bitmaps are deliberately never decoration layers.
        /// </summary>
        public static DecorationLayerPlacement DecorationPlacementFor(
            int episode, string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return DecorationLayerPlacement.None;
            }

            var normalized = Path.ChangeExtension(
                textureName.Replace('\\', '/').TrimStart('/'), null);
            var name = Path.GetFileName(normalized);
            if (IsEndrollTexture(name))
            {
                return DecorationLayerPlacement.None;
            }

            if (string.Equals(name, "titlelogo", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("sgtitle", StringComparison.OrdinalIgnoreCase))
            {
                return DecorationLayerPlacement.AnchorRight;
            }

            if (normalized.StartsWith("scenario/logo", StringComparison.OrdinalIgnoreCase))
            {
                return DecorationLayerPlacement.FitBottomRight;
            }

            if (episode == 8 &&
                string.Equals(normalized, "scenario/stripes", StringComparison.OrdinalIgnoreCase))
            {
                return DecorationLayerPlacement.ShiftLeft;
            }

            if (string.Equals(normalized, "logo", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return DecorationLayerPlacement.FitInsideFrame;
            }

            return DecorationLayerPlacement.None;
        }

        /// <summary>
        /// Resolves the fitted size and center-relative offset (same units as
        /// the input dimensions; negative X = left, positive Y = down) for a
        /// centered decoration layer. Leaves console/16:9 frames untouched.
        /// </summary>
        public static void ResolveDecorationLayer(
            int episode, string textureName,
            float width, float height,
            float frameWidth, float frameHeight,
            out float scaledWidth, out float scaledHeight,
            out float offsetX, out float offsetY)
        {
            scaledWidth = width;
            scaledHeight = height;
            offsetX = 0f;
            offsetY = 0f;
            if (width <= 0f || height <= 0f || frameWidth <= 0f || frameHeight <= 0f)
            {
                return;
            }

            switch (DecorationPlacementFor(episode, textureName))
            {
                case DecorationLayerPlacement.FitInsideFrame:
                {
                    var scale = UniformFitScale(width, height, frameWidth, frameHeight);
                    scaledWidth = width * scale;
                    scaledHeight = height * scale;
                    return;
                }
                case DecorationLayerPlacement.FitBottomRight:
                {
                    var scale = UniformFitScale(width, height, frameWidth, frameHeight);
                    scaledWidth = width * scale;
                    scaledHeight = height * scale;
                    if (scale < 1f)
                    {
                        offsetX = (frameWidth - scaledWidth) * 0.5f;
                        offsetY = (frameHeight - scaledHeight) * 0.5f;
                    }
                    return;
                }
                case DecorationLayerPlacement.AnchorRight:
                    if (width > frameWidth)
                    {
                        offsetX = (frameWidth - width) * 0.5f;
                    }
                    return;
                case DecorationLayerPlacement.ShiftLeft:
                    if (width > frameWidth)
                    {
                        var anchorRight = (frameWidth - width) * 0.5f;
                        var shift = -width * EpisodeEightStripesShiftFraction;
                        offsetX = shift > anchorRight ? shift : anchorRight;
                    }
                    return;
            }
        }

        /// <summary>
        /// Uniform shrink that keeps a FitInsideFrame or FitBottomRight
        /// decoration layer fully inside the canvas frame (never enlarges, never
        /// stretches). Every other layer returns 1 so its size is unchanged.
        /// </summary>
        public static float DecorationLayerFitScale(
            int episode, string textureName, float width, float height,
            float frameWidth, float frameHeight)
        {
            var placement = DecorationPlacementFor(episode, textureName);
            if ((placement != DecorationLayerPlacement.FitInsideFrame &&
                 placement != DecorationLayerPlacement.FitBottomRight) ||
                width <= 0f || height <= 0f || frameWidth <= 0f || frameHeight <= 0f)
            {
                return 1f;
            }

            return UniformFitScale(width, height, frameWidth, frameHeight);
        }

        /// <summary>
        /// Horizontal offset (same units as the widths, negative = left) for an
        /// unscaled AnchorRight or ShiftLeft decoration layer that is wider than
        /// the frame. Zero whenever the layer already fits (16:9 frames).
        /// </summary>
        public static float DecorationLayerOffsetX(
            int episode, string textureName, float width, float frameWidth)
        {
            ResolveDecorationLayer(episode, textureName, width, 480f, frameWidth, 480f,
                out _, out _, out var offsetX, out _);
            return offsetX;
        }

        private static float UniformFitScale(
            float width, float height, float frameWidth, float frameHeight)
        {
            var widthScale = frameWidth / width;
            var heightScale = frameHeight / height;
            var scale = widthScale < heightScale ? widthScale : heightScale;
            return scale < 1f ? scale : 1f;
        }

        // EP8 stripes credits end at x 1812 of 1920; 140 px brings them to 1672,
        // inside the 4:3 crop's 1680 right edge, without reaching the description.
        private const float EpisodeEightStripesShiftFraction = 140f / 1920f;

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

    public enum DecorationLayerPlacement
    {
        None,
        FitInsideFrame,
        FitBottomRight,
        AnchorRight,
        ShiftLeft
    }
}
