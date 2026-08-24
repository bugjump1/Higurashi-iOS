using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Compatibility
{
    public static class ConsoleChoiceMenuPolicy
    {
        public const string LocalizedPrompt =
            "这个篇章包含会导致坏结局的选项，你想看到这些选项吗？";

        public static bool IsConsoleChoicePrompt(string dialogue)
        {
            return Contains(dialogue, "This arc includes choices") ||
                   (Contains(dialogue, "この編には") && Contains(dialogue, "選択") &&
                    Contains(dialogue, "コンソール")) ||
                   (Contains(dialogue, "这个篇章包含") &&
                    (Contains(dialogue, "追加选项") || Contains(dialogue, "坏结局")));
        }

        public static bool IsConsoleChoiceMenu(string dialogue, IReadOnlyList<string> choices)
        {
            if (choices == null || choices.Count != 3)
            {
                return false;
            }

            if (IsConsoleChoicePrompt(dialogue))
            {
                return true;
            }

            return IsKnownChoice(choices[0], 0) &&
                   IsKnownChoice(choices[1], 1) &&
                   IsKnownChoice(choices[2], 2);
        }

        public static string Localize(string value)
        {
            var text = (value ?? string.Empty).Trim();
            switch (text)
            {
                case "Skip additional choices. Show only content from PC version":
                case "追加した選択を見せません。ＰＣ版内容だけ見せます":
                case "不要不要，我只想要好结局":
                    return "不要不要，我只想要好结局";
                case "Prompt additional choices from console version":
                case "コンソール版に追加した選択を見せます":
                case "我就是想看到坏选项":
                    return "我就是想看到坏选项";
                case "Prompt choices and highlight correct answers":
                case "選択を見せながら正解に記号をつける":
                case "可以哦，但请标记下正确选项":
                    return "可以哦，但请标记下正确选项";
                default:
                    return value ?? string.Empty;
            }
        }

        private static bool IsKnownChoice(string value, int index)
        {
            var text = (value ?? string.Empty).Trim();
            switch (index)
            {
                case 0:
                    return text == "Skip additional choices. Show only content from PC version" ||
                           text == "追加した選択を見せません。ＰＣ版内容だけ見せます" ||
                           text == "不要不要，我只想要好结局";
                case 1:
                    return text == "Prompt additional choices from console version" ||
                           text == "コンソール版に追加した選択を見せます" ||
                           text == "我就是想看到坏选项";
                case 2:
                    return text == "Prompt choices and highlight correct answers" ||
                           text == "選択を見せながら正解に記号をつける" ||
                           text == "可以哦，但请标记下正确选项";
                default:
                    return false;
            }
        }

        private static bool Contains(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
