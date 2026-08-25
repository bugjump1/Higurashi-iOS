using System;

namespace Higurashi.IOS.Playback
{
    public static class MessageSpeedPolicy
    {
        public static int ScriptOverride(bool enabled, int scriptValue)
        {
            return enabled ? 50 * (Math.Max(0, scriptValue) / 100) : -1;
        }

        public static int EffectiveSpeed(int userSpeed, int scriptOverride)
        {
            return Math.Max(0, Math.Min(100,
                scriptOverride >= 0 ? scriptOverride : userSpeed));
        }

        public static float CharactersPerSecond(int userSpeed, int scriptOverride)
        {
            var normalized = EffectiveSpeed(userSpeed, scriptOverride) / 100f;
            return 18f + 72f * normalized;
        }
    }
}
