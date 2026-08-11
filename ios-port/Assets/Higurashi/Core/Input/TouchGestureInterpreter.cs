using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Input
{
    /// <summary>
    /// Converts raw touch samples into semantic novel actions. It deliberately
    /// has no Unity dependency so the gesture rules can be tested on Windows.
    /// </summary>
    public sealed class TouchGestureInterpreter
    {
        private const double ThreeFingerJoinWindowSeconds = 0.20;
        private const double TapMaximumSeconds = 0.45;
        private const float ThreeFingerHorizontalThreshold = 0.15f;
        private const float SingleFingerHorizontalThreshold = 0.12f;
        private const float VerticalSwipeThreshold = 0.12f;

        private readonly HashSet<int> _threeFingerIds = new HashSet<int>();
        private bool _trackingThreeFingerGesture;
        private bool _waitingForThreeFingerRelease;
        private bool _fastStopArmed;
        private double _multiTouchCandidateStartedAt = -1;
        private float _threeFingerStartX;
        private float _threeFingerStartY;

        private int _singlePointerId = -1;
        private double _singlePointerStartedAt;
        private float _singlePointerStartX;
        private float _singlePointerStartY;

        public bool FastStopArmed => _fastStopArmed;

        public NovelInputAction ProcessFrame(
            IReadOnlyList<PointerSample> samples,
            float screenWidth,
            float screenHeight,
            double nowSeconds,
            bool fastTraversalActive)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return NovelInputAction.None;
            }

            // iOS may drop the final Ended/Canceled sample when Control Center,
            // a system gesture, or an application focus change interrupts a
            // touch.  Do not let that orphaned pointer permanently block every
            // later one-finger swipe.
            if (_singlePointerId >= 0 && !ContainsPointer(samples, _singlePointerId))
            {
                CancelSinglePointer();
            }

            if (_waitingForThreeFingerRelease)
            {
                if (!AnyTrackedThreeFingerIsActive(samples))
                {
                    _waitingForThreeFingerRelease = false;
                    _threeFingerIds.Clear();
                    _fastStopArmed = true;
                }

                return NovelInputAction.None;
            }

            if (!fastTraversalActive)
            {
                _fastStopArmed = false;
            }
            else if (!_fastStopArmed && !_trackingThreeFingerGesture && CountActive(samples) == 0)
            {
                // Also covers fast traversal started from a visible toolbar button.
                _fastStopArmed = true;
            }
            else if (_fastStopArmed && HasNewStoppingTouch(samples))
            {
                ResetOrdinaryGestureState();
                _fastStopArmed = false;
                return NovelInputAction.StopFastTraversal;
            }

            var activeCount = CountActive(samples);

            if (_trackingThreeFingerGesture)
            {
                return ContinueThreeFingerGesture(samples, screenWidth, screenHeight);
            }

            if (activeCount > 1)
            {
                CancelSinglePointer();
            }

            if (activeCount > 0 && _multiTouchCandidateStartedAt < 0)
            {
                _multiTouchCandidateStartedAt = nowSeconds;
            }

            if (activeCount == 3 &&
                nowSeconds - _multiTouchCandidateStartedAt <= ThreeFingerJoinWindowSeconds)
            {
                StartThreeFingerGesture(samples);
                return NovelInputAction.None;
            }

            if (activeCount > 3 ||
                (_multiTouchCandidateStartedAt >= 0 &&
                 nowSeconds - _multiTouchCandidateStartedAt > ThreeFingerJoinWindowSeconds))
            {
                _multiTouchCandidateStartedAt = -1;
            }

            var singleAction = ProcessSinglePointer(samples, screenWidth, screenHeight, nowSeconds);

            if (activeCount == 0)
            {
                _multiTouchCandidateStartedAt = -1;
            }

            return singleAction;
        }

        public void Reset()
        {
            ResetOrdinaryGestureState();
            _fastStopArmed = false;
        }

        private NovelInputAction ContinueThreeFingerGesture(
            IReadOnlyList<PointerSample> samples,
            float screenWidth,
            float screenHeight)
        {
            if (!TryGetTrackedCentroid(samples, out var x, out var y, out var activeTrackedCount))
            {
                ResetThreeFingerGesture();
                return NovelInputAction.None;
            }

            if (activeTrackedCount < 3)
            {
                ResetThreeFingerGesture();
                return NovelInputAction.None;
            }

            var dx = x - _threeFingerStartX;
            var dy = y - _threeFingerStartY;
            var normalizedX = Math.Abs(dx) / screenWidth;
            var normalizedY = Math.Abs(dy) / screenHeight;

            if (normalizedX < ThreeFingerHorizontalThreshold || normalizedX < normalizedY * 1.5f)
            {
                return NovelInputAction.None;
            }

            _trackingThreeFingerGesture = false;
            _waitingForThreeFingerRelease = true;
            return dx < 0
                ? NovelInputAction.StartFastForward
                : NovelInputAction.StartFastRewind;
        }

        private NovelInputAction ProcessSinglePointer(
            IReadOnlyList<PointerSample> samples,
            float screenWidth,
            float screenHeight,
            double nowSeconds)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];

                if (sample.Phase == PointerPhase.Began && _singlePointerId < 0)
                {
                    _singlePointerId = sample.Id;
                    _singlePointerStartedAt = nowSeconds;
                    _singlePointerStartX = sample.X;
                    _singlePointerStartY = sample.Y;
                    continue;
                }

                if (sample.Id != _singlePointerId ||
                    (sample.Phase != PointerPhase.Ended && sample.Phase != PointerPhase.Canceled))
                {
                    continue;
                }

                var id = _singlePointerId;
                _singlePointerId = -1;

                if (sample.Phase == PointerPhase.Canceled || id < 0)
                {
                    return NovelInputAction.None;
                }

                var dx = sample.X - _singlePointerStartX;
                var dy = sample.Y - _singlePointerStartY;
                var normalizedX = Math.Abs(dx) / screenWidth;
                var normalizedY = Math.Abs(dy) / screenHeight;

                if (normalizedY >= VerticalSwipeThreshold && normalizedY > normalizedX * 1.3f)
                {
                    return dy > 0
                        ? NovelInputAction.OpenHistory
                        : NovelInputAction.ToggleTextWindow;
                }

                if (normalizedX >= SingleFingerHorizontalThreshold &&
                    normalizedX > normalizedY * 1.3f)
                {
                    return dx > 0
                        ? NovelInputAction.PreviousTextBox
                        : NovelInputAction.Advance;
                }

                var maximumTapTravel = Math.Max(16f, Math.Min(screenWidth, screenHeight) * 0.03f);
                var travelSquared = dx * dx + dy * dy;
                if (nowSeconds - _singlePointerStartedAt <= TapMaximumSeconds &&
                    travelSquared <= maximumTapTravel * maximumTapTravel)
                {
                    return NovelInputAction.Advance;
                }
            }

            return NovelInputAction.None;
        }

        private void StartThreeFingerGesture(IReadOnlyList<PointerSample> samples)
        {
            _threeFingerIds.Clear();
            var x = 0f;
            var y = 0f;

            for (var i = 0; i < samples.Count && _threeFingerIds.Count < 3; i++)
            {
                if (!samples[i].IsActive)
                {
                    continue;
                }

                _threeFingerIds.Add(samples[i].Id);
                x += samples[i].X;
                y += samples[i].Y;
            }

            if (_threeFingerIds.Count != 3)
            {
                _threeFingerIds.Clear();
                return;
            }

            _threeFingerStartX = x / 3f;
            _threeFingerStartY = y / 3f;
            _trackingThreeFingerGesture = true;
            _multiTouchCandidateStartedAt = -1;
            CancelSinglePointer();
        }

        private bool TryGetTrackedCentroid(
            IReadOnlyList<PointerSample> samples,
            out float x,
            out float y,
            out int activeCount)
        {
            x = 0;
            y = 0;
            activeCount = 0;

            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (!_threeFingerIds.Contains(sample.Id) || !sample.IsActive)
                {
                    continue;
                }

                x += sample.X;
                y += sample.Y;
                activeCount++;
            }

            if (activeCount == 0)
            {
                return false;
            }

            x /= activeCount;
            y /= activeCount;
            return true;
        }

        private bool AnyTrackedThreeFingerIsActive(IReadOnlyList<PointerSample> samples)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].IsActive && _threeFingerIds.Contains(samples[i].Id))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNewStoppingTouch(IReadOnlyList<PointerSample> samples)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].Phase == PointerPhase.Began || samples[i].Phase == PointerPhase.Moved)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountActive(IReadOnlyList<PointerSample> samples)
        {
            var count = 0;
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsPointer(IReadOnlyList<PointerSample> samples, int id)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private void ResetThreeFingerGesture()
        {
            _trackingThreeFingerGesture = false;
            _waitingForThreeFingerRelease = false;
            _threeFingerIds.Clear();
            _multiTouchCandidateStartedAt = -1;
        }

        private void ResetOrdinaryGestureState()
        {
            ResetThreeFingerGesture();
            CancelSinglePointer();
        }

        private void CancelSinglePointer()
        {
            _singlePointerId = -1;
        }
    }
}
