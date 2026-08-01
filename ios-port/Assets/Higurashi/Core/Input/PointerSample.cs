namespace Higurashi.IOS.Input
{
    public enum PointerPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled
    }

    public readonly struct PointerSample
    {
        public PointerSample(int id, float x, float y, PointerPhase phase)
        {
            Id = id;
            X = x;
            Y = y;
            Phase = phase;
        }

        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public PointerPhase Phase { get; }

        public bool IsActive => Phase != PointerPhase.Ended && Phase != PointerPhase.Canceled;
    }
}

