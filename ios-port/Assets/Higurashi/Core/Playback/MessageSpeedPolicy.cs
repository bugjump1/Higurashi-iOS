using System;

namespace Higurashi.IOS.Playback
{
    public static class MessageSpeedPolicy
    {
        public static float CharactersPerSecond(int userSpeed, int scriptSpeed)
        {
            if (scriptSpeed >= 0)
            {
                // PC scripts use zero as an immediate reveal and other values
                // as a 50 * (speed / 100) override.
                return scriptSpeed == 0 ? float.PositiveInfinity : 50f * scriptSpeed / 100f;
            }

            var normalized = Math.Max(0, Math.Min(100, userSpeed)) / 100f;
            return 18f + (90f - 18f) * normalized;
        }
    }
}
