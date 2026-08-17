using System;

namespace Higurashi.IOS.Playback
{
    public sealed class AutoAdvanceScheduler
    {
        private int _dialogueSerial = int.MinValue;
        private bool _delayScheduled;
        private bool _voiceWasPlaying;
        private double _deadline = double.PositiveInfinity;

        public void Reset()
        {
            _dialogueSerial = int.MinValue;
            _delayScheduled = false;
            _voiceWasPlaying = false;
            _deadline = double.PositiveInfinity;
        }

        public bool ShouldAdvance(
            int dialogueSerial,
            bool revealComplete,
            bool voicePlaying,
            double now,
            double readingDelay,
            double postVoiceDelay)
        {
            if (_dialogueSerial != dialogueSerial)
            {
                _dialogueSerial = dialogueSerial;
                _delayScheduled = false;
                _voiceWasPlaying = voicePlaying;
                _deadline = double.PositiveInfinity;
            }

            if (!revealComplete)
            {
                return false;
            }

            if (!_delayScheduled)
            {
                _delayScheduled = true;
                _voiceWasPlaying = voicePlaying;
                _deadline = now + Math.Max(0, readingDelay);
                return false;
            }

            if (voicePlaying)
            {
                _voiceWasPlaying = true;
                return false;
            }

            if (_voiceWasPlaying)
            {
                _voiceWasPlaying = false;
                _deadline = Math.Max(_deadline, now + Math.Max(0, postVoiceDelay));
            }

            if (now < _deadline)
            {
                return false;
            }

            // Avoid issuing an advance every frame if the caller cannot move yet.
            _deadline = now + Math.Max(0, readingDelay);
            return true;
        }
    }
}
