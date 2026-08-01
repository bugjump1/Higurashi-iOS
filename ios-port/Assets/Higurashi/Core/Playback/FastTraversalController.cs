using System;

namespace Higurashi.IOS.Playback
{
    public enum FastTraversalMode
    {
        Normal = 0,
        Forward,
        Rewind
    }

    public interface INovelTraversalDriver
    {
        bool StepForward();
        bool StepBackward();
    }

    /// <summary>
    /// Advances at a controlled dialogue rate and never batches multiple lines
    /// into one rendered frame.
    /// </summary>
    public sealed class FastTraversalController
    {
        private float _elapsed;

        public FastTraversalController(float checkpointsPerSecond = 10f)
        {
            CheckpointsPerSecond = checkpointsPerSecond;
        }

        public event Action<FastTraversalMode> ModeChanged;

        public FastTraversalMode Mode { get; private set; }

        public float CheckpointsPerSecond
        {
            get => _checkpointsPerSecond;
            set => _checkpointsPerSecond = Math.Max(1f, Math.Min(30f, value));
        }

        private float _checkpointsPerSecond;

        public bool IsActive => Mode != FastTraversalMode.Normal;

        public void StartForward()
        {
            SetMode(FastTraversalMode.Forward);
        }

        public void StartRewind()
        {
            SetMode(FastTraversalMode.Rewind);
        }

        public void Stop()
        {
            SetMode(FastTraversalMode.Normal);
        }

        public bool Tick(float unscaledDeltaTime, INovelTraversalDriver driver)
        {
            if (!IsActive || driver == null)
            {
                return false;
            }

            _elapsed += Math.Max(0, unscaledDeltaTime);
            var interval = 1f / CheckpointsPerSecond;
            if (_elapsed < interval)
            {
                return false;
            }

            // Keep one checkpoint visible for at least one rendered frame.
            _elapsed = Math.Min(_elapsed - interval, interval);
            var stepped = Mode == FastTraversalMode.Forward
                ? driver.StepForward()
                : driver.StepBackward();

            if (!stepped)
            {
                Stop();
            }

            return stepped;
        }

        private void SetMode(FastTraversalMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            _elapsed = mode == FastTraversalMode.Normal ? 0 : 1f / CheckpointsPerSecond;
            ModeChanged?.Invoke(mode);
        }
    }
}

