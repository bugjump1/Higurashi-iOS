using System;
using System.Globalization;

namespace Higurashi.IOS.Compatibility
{
    public static class EpisodeEightChapterMap
    {
        private const string TokenPrefix = "EP08_CHAPTER_";
        private static readonly int[] JumpValues = { 0, 3, 5, 7, 8, 11, 13, 15, 17, 19 };
        private static readonly int[] CompletionValues = { 2, 4, 6, 7, 10, 12, 14, 16, 18, 25 };

        public static int Count => JumpValues.Length;

        public static string Token(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(chapterIndex));
            }
            return TokenPrefix + chapterIndex.ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool TryGetJumpValue(string token, out int jumpValue)
        {
            jumpValue = 0;
            if (string.IsNullOrEmpty(token) ||
                !token.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(token.Substring(TokenPrefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var chapterIndex) ||
                chapterIndex < 0 || chapterIndex >= Count)
            {
                return false;
            }

            jumpValue = JumpValues[chapterIndex];
            return true;
        }

        public static int CompletedChapterCount(int scriptChapterNumber)
        {
            var completed = 0;
            for (var i = 0; i < CompletionValues.Length; i++)
            {
                if (scriptChapterNumber < CompletionValues[i])
                {
                    break;
                }
                completed = i + 1;
            }
            return completed;
        }
    }
}
