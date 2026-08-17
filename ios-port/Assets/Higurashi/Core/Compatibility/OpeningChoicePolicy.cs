using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Compatibility
{
    public static class OpeningChoicePolicy
    {
        public const string LocalizedEnable = "启用 OP 动画";
        public const string LocalizedDisable = "禁用 OP 动画";
        public const string LocalizedPrompt = "OP 动画中包含剧透，是否要启用？";

        public static bool IsOpeningPrompt(string dialogue)
        {
            return Contains(dialogue, "OP 动画中包含剧透") ||
                   Contains(dialogue, "OP动画中包含剧透") ||
                   Contains(dialogue, "开场动画包含剧透") ||
                   (IsChineseOpeningLabel(dialogue) &&
                    Contains(dialogue, "剧透") &&
                    (Contains(dialogue, "启用") || Contains(dialogue, "播放"))) ||
                   (Contains(dialogue, "Video opening") && Contains(dialogue, "spoiler")) ||
                   (Contains(dialogue, "オープニング動画") && Contains(dialogue, "ネタバレ"));
        }

        public static bool IsOpeningChoice(string dialogue, IReadOnlyList<string> choices)
        {
            if (choices == null || choices.Count < 2)
            {
                return false;
            }

            if (IsOpeningPrompt(dialogue))
            {
                return true;
            }

            return IsKnownPair(choices[0], choices[1]);
        }

        private static bool IsKnownPair(string first, string second)
        {
            return (Contains(first, "Enable opening") && Contains(second, "Disable opening")) ||
                   (Contains(first, "動画再生を有効化") && Contains(second, "動画再生を無効化")) ||
                   (Contains(first, "启用播放") && Contains(second, "禁用播放")) ||
                   (string.Equals(first, LocalizedEnable, StringComparison.Ordinal) &&
                    string.Equals(second, LocalizedDisable, StringComparison.Ordinal));
        }

        private static bool IsChineseOpeningLabel(string value)
        {
            return Contains(value, "开场动画") || Contains(value, "片头动画") ||
                   Contains(value, "OP 动画") || Contains(value, "OP动画");
        }

        private static bool Contains(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
