namespace Higurashi.IOS.Compatibility
{
    public readonly struct AspectFitRectangle
    {
        public AspectFitRectangle(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
    }

    public static class AspectFitLayout
    {
        public static AspectFitRectangle Fit(float x, float y, float width, float height,
            float aspectRatio)
        {
            if (width <= 0f || height <= 0f)
            {
                return new AspectFitRectangle(x, y, 0f, 0f);
            }

            var ratio = aspectRatio > 0f ? aspectRatio : 16f / 9f;
            var fittedWidth = width;
            var fittedHeight = fittedWidth / ratio;
            if (fittedHeight > height)
            {
                fittedHeight = height;
                fittedWidth = fittedHeight * ratio;
            }

            return new AspectFitRectangle(
                x + (width - fittedWidth) * 0.5f,
                y + (height - fittedHeight) * 0.5f,
                fittedWidth,
                fittedHeight);
        }
    }
}
