using System;
using System.IO;

namespace Higurashi.IOS.Compatibility
{
    public static class BadEndingChoicePolicy
    {
        public static bool IsBadEndingChoice(int episode, string scriptName, int choiceIndex)
        {
            var script = NormalizeScriptName(scriptName);
            switch (episode)
            {
                case 4:
                    return MatchesScript(script, "hima_003_03") &&
                           choiceIndex == 1;
                case 5:
                    return MatchesScript(script, "_meak_024") &&
                           choiceIndex == 0;
                case 6:
                    return (MatchesScript(script, "_tsum_024_1") &&
                            choiceIndex == 1) ||
                           (MatchesScript(script, "_tsum_026") &&
                            choiceIndex == 0);
                default:
                    return false;
            }
        }

        private static bool MatchesScript(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ||
                   actual.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase) ||
                   actual.EndsWith("\\" + expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeScriptName(string scriptName)
        {
            var script = (scriptName ?? string.Empty).Replace('\\', '/');
            script = Path.GetFileNameWithoutExtension(script);
            var separator = script.IndexOf(':');
            if (separator >= 0)
            {
                script = script.Substring(0, separator);
            }
            return script;
        }
    }
}
