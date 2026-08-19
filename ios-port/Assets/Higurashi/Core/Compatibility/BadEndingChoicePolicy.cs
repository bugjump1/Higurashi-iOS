using System;

namespace Higurashi.IOS.Compatibility
{
    public static class BadEndingChoicePolicy
    {
        public static bool IsBadEndingChoice(int episode, string scriptName, int choiceIndex)
        {
            var script = scriptName ?? string.Empty;
            switch (episode)
            {
                case 4:
                    return string.Equals(script, "hima_003_03", StringComparison.OrdinalIgnoreCase) &&
                           choiceIndex == 1;
                case 5:
                    return string.Equals(script, "_meak_024", StringComparison.OrdinalIgnoreCase) &&
                           choiceIndex == 0;
                case 6:
                    return (string.Equals(script, "_tsum_024_1", StringComparison.OrdinalIgnoreCase) &&
                            choiceIndex == 1) ||
                           (string.Equals(script, "_tsum_026", StringComparison.OrdinalIgnoreCase) &&
                            choiceIndex == 0);
                default:
                    return false;
            }
        }
    }
}
