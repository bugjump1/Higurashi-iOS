using System;
using System.Globalization;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// TIPS 可见性判定，逐条对齐 PC 反编译语义（Assets.Scripts.UI.Tips.TipsData /
    /// TipsManager，2026-10-25 本机八章 DLL 实证）：
    ///
    /// EP1–04 章号制：仅新增 = UnlockChapter == L&lt;篇名&gt;Day；累计 = ≤ L Day；
    ///   图鉴（global）= ≤ G&lt;篇名&gt;Day。UnlockChapter 来自 tips.json（与 flow 章号
    ///   同刻度；flow 的 SetDay 节把 L/G 旗标写成与 ChapterNumber 相同的值）。
    /// EP05–08 累计制：仅新增 = 窗口基值 ∈ [NewTipsStart, NewTipsStart+NewTipsCount)；
    ///   累计 = 窗口基值 &lt; NewTipsStart+NewTipsCount；图鉴 = 窗口基值 &lt; GTotalTips
    ///   且 GFlag_GameClear 后全开（EP6-8 有此细则）。窗口基值：EP5 用列表索引
    ///   （= Id-1，其目录 Id 为 1..23 而 NewTipsStart 从 0 起）；EP6-8 用全局 Id
    ///   （24..37 / 38..50 / 51，与 NewTipsStart 同域）。
    ///
    /// TipsManager.Show(0)=累计、(1)=仅新增、(2)=图鉴；脚本经 ShowTips(TipsMode-3)
    /// 传入。原实现把 0/1 语义互换、并以硬编码章号表（主机刻度）对比 flow 章号，
    /// 本类替代该表。
    /// </summary>
    public static class TipsVisibilityPolicy
    {
        public const int ScopeCumulative = 0;
        public const int ScopeNewOnly = 1;
        public const int ScopeGlobal = 2;

        public static bool UsesCountModel(int episode)
        {
            return episode >= 5;
        }

        /// <summary>EP1-04 当前进度的本地旗标名（PC 窗口模式读取）。</summary>
        public static string LocalDayFlagName(int episode)
        {
            switch (episode)
            {
                case 1: return "LOnikakushiDay";
                case 2: return "LWatanagashiDay";
                case 3: return "LTatarigoroshiDay";
                case 4: return "LHimatsubushiDay";
                default: return "ChapterNumber";
            }
        }

        /// <summary>EP1-04 历史最高进度的全局旗标名（PC 图鉴模式读取）。</summary>
        public static string GlobalDayFlagName(int episode)
        {
            switch (episode)
            {
                case 1: return "GOnikakushiDay";
                case 2: return "GWatanagashiDay";
                case 3: return "GTatarigoroshiDay";
                case 4: return "GHimatsubushiDay";
                default: return "GTotalTips";
            }
        }

        /// <summary>EP1-04：窗口判定（PC：== 为仅新增、≤ 为累计）。</summary>
        public static bool ChapterModelIncludes(bool onlyNew, int unlockChapter, int currentDay)
        {
            return onlyNew
                ? unlockChapter == currentDay
                : unlockChapter <= currentDay;
        }

        /// <summary>EP1-04：图鉴判定（PC：≤ G Day）。</summary>
        public static bool ChapterModelGlobalIncludes(int unlockChapter, int maxDay)
        {
            return unlockChapter <= maxDay;
        }

        /// <summary>EP05-08：窗口比较基值（EP5=列表索引，EP6-8=全局 Id）。</summary>
        public static int WindowBasis(int episode, int tipId)
        {
            return episode == 5 ? tipId - 1 : tipId;
        }

        /// <summary>EP05-08：窗口判定（PC：onlyNew=窗口内，否则 &lt; 窗口尾）。</summary>
        public static bool CountModelIncludes(bool onlyNew, int basis, int newTipsStart, int newTipsCount)
        {
            var end = newTipsStart + newTipsCount;
            return onlyNew
                ? basis >= newTipsStart && basis < end
                : basis < end;
        }

        /// <summary>EP05-08：图鉴判定（PC：Id &lt; GTotalTips；通关后全开）。</summary>
        public static bool CountModelGlobalIncludes(int basis, int totalTips, bool gameClear)
        {
            return gameClear || basis < totalTips;
        }

        /// <summary>
        /// 从 EP1-04 章节跳转档位名解析 flow 章号（"Day9_2"→9）。EP5-08 的
        /// 合成档位名（EPxx_CHAPTER_NN）返回 -1。
        /// </summary>
        public static int DayNumberFromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return -1;
            }
            var span = token.Trim();
            if (span.StartsWith("Day", StringComparison.OrdinalIgnoreCase))
            {
                span = span.Substring(3);
            }
            var digits = 0;
            while (digits < span.Length && char.IsDigit(span[digits]))
            {
                digits++;
            }
            return digits > 0 && int.TryParse(span.Substring(0, digits),
                       NumberStyles.None, CultureInfo.InvariantCulture, out var day)
                ? day
                : -1;
        }

        // EP05-08 每个章节跳转档位完成时的累计 TIPS 数（= GTotalTips 应达到的值）。
        // 由 flow.mg 的 NewTipsStart/Count 配对（章号刻度）与 EpisodeChapterJumpMap
        // 的 s_jump 档位推导，末位=目录总数（含尾声 TIPS）。
        private static readonly int[] Episode05TipsThroughChapter =
        {
            1, 2, 3, 5, 6, 7, 9, 12, 15, 17, 18, 20, 21, 23
        };

        private static readonly int[] Episode06TipsThroughChapter =
        {
            1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
        };

        private static readonly int[] Episode07TipsThroughChapter =
        {
            1, 2, 3, 4, 5, 7, 9, 10, 10, 11, 12, 13
        };

        /// <summary>
        /// 章节跳转档位（1 起）完成时的累计 TIPS 数，超出末位返回目录总数；
        /// 未知档位返回 0。EP8 仅 1 条 TIPS（Id 51），任一档位即按通关值 52 计
        /// （PC：Id &lt; GTotalTips）。
        /// </summary>
        public static int TipsThroughChapter(int episode, int logicalChapter)
        {
            if (logicalChapter <= 0)
            {
                return 0;
            }
            switch (episode)
            {
                case 5:
                    return ValueOrLast(Episode05TipsThroughChapter, logicalChapter);
                case 6:
                    return ValueOrLast(Episode06TipsThroughChapter, logicalChapter);
                case 7:
                    return ValueOrLast(Episode07TipsThroughChapter, logicalChapter);
                case 8:
                    return 52;
                default:
                    return 0;
            }
        }

        private static int ValueOrLast(int[] table, int logicalChapter)
        {
            return logicalChapter <= table.Length
                ? table[logicalChapter - 1]
                : table[table.Length - 1];
        }
    }
}
