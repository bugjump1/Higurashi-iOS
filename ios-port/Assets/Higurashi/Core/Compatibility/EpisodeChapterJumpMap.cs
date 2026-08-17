using System;
using System.Globalization;

namespace Higurashi.IOS.Compatibility
{
    public static class EpisodeChapterJumpMap
    {
        private static readonly string[] Episode01Sections =
        {
            "Day1", "Day2", "Day3", "Day4", "Day5", "Day9", "Day9_2", "Day10",
            "Day11", "Day12", "Day13", "Day14", "Day14_2", "Day15", "Day15_2", "Day15_3"
        };

        private static readonly string[] Episode02Sections =
        {
            "Day1", "Day2", "Day3", "Day4", "Day5", "Day6", "Day7", "Day8",
            "Day9", "Day9_2", "Day10", "Day10_2", "Day10_3", "Day10_4", "Day11",
            "Day11_2", "Day12", "Day12_2", "Day12_3"
        };

        private static readonly string[] Episode03Sections =
        {
            "Day1", "Day2", "Day3", "Day4", "Day5", "Day8", "Day8_2", "Day9",
            "Day9_2", "Day10", "Day10_2", "Day10_3", "Day10_4", "Day11", "Day11_2",
            "Day11_3", "Day12", "Day13", "Day13_2", "Day14"
        };

        private static readonly string[] Episode04Sections =
        {
            "Day1", "Day2", "Day2_2", "Day2_3", "Day3", "Day3_2", "Day3_3",
            "Day3_4", "Day3_5", "Day4"
        };

        // Original flow scripts use these non-contiguous s_jump values as chapter starts.
        private static readonly int[] Episode05JumpValues =
        {
            1, 2, 4, 6, 8, 10, 12, 14, 15, 17, 19, 21, 23, 25
        };

        private static readonly int[] Episode06JumpValues =
        {
            1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24
        };

        private static readonly int[] Episode07JumpValues =
        {
            1, 2, 3, 6, 9, 11, 13, 15, 17, 19, 21, 25
        };

        public static int Count(int episode)
        {
            var direct = GetDirectSections(episode);
            if (direct != null)
            {
                return direct.Length;
            }

            var jumps = GetFlowJumpValues(episode);
            if (jumps != null)
            {
                return jumps.Length;
            }

            return episode == 8 ? EpisodeEightChapterMap.Count : 0;
        }

        public static string Token(int episode, int chapterIndex)
        {
            var direct = GetDirectSections(episode);
            if (direct != null)
            {
                if (chapterIndex < 0 || chapterIndex >= direct.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(chapterIndex));
                }
                return direct[chapterIndex];
            }

            if (episode == 8)
            {
                return EpisodeEightChapterMap.Token(chapterIndex);
            }

            var jumps = GetFlowJumpValues(episode);
            if (jumps == null || chapterIndex < 0 || chapterIndex >= jumps.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(chapterIndex));
            }
            return TokenPrefix(episode) + chapterIndex.ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool TryGetFlowJumpValue(int episode, string token, out int jumpValue)
        {
            jumpValue = 0;
            if (episode == 8)
            {
                return EpisodeEightChapterMap.TryGetJumpValue(token, out jumpValue);
            }

            var jumps = GetFlowJumpValues(episode);
            var prefix = TokenPrefix(episode);
            if (jumps == null || string.IsNullOrEmpty(token) ||
                !token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(token.Substring(prefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var chapterIndex) ||
                chapterIndex < 0 || chapterIndex >= jumps.Length)
            {
                return false;
            }

            jumpValue = jumps[chapterIndex];
            return true;
        }

        private static string TokenPrefix(int episode)
        {
            return "EP" + episode.ToString("00", CultureInfo.InvariantCulture) + "_CHAPTER_";
        }

        private static string[] GetDirectSections(int episode)
        {
            switch (episode)
            {
                case 1: return Episode01Sections;
                case 2: return Episode02Sections;
                case 3: return Episode03Sections;
                case 4: return Episode04Sections;
                default: return null;
            }
        }

        private static int[] GetFlowJumpValues(int episode)
        {
            switch (episode)
            {
                case 5: return Episode05JumpValues;
                case 6: return Episode06JumpValues;
                case 7: return Episode07JumpValues;
                default: return null;
            }
        }
    }
}
