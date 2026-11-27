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

        // PC ChapterButton.cs: ViewAllTips writes TipsMode=3, NewTips writes
        // TipsMode=4; flow.mg then calls ShowTips(TipsMode - 3), mapping
        // 3 -> 0 (ScopeCumulative) and 4 -> 1 (ScopeNewOnly).
        public const int ChapterScreenTipsModeAll = 3;
        public const int ChapterScreenTipsModeNew = 4;

        public static int TipsModeForChapterScreen(bool allUnlocked)
        {
            return allUnlocked ? ChapterScreenTipsModeAll : ChapterScreenTipsModeNew;
        }

        public static bool UsesCountModel(int episode)
        {
            return episode >= 5;
        }

        /// <summary>该章在 PC 图鉴（ScopeGlobal）中读取的全局进度旗标名。</summary>
        public static string GlobalProgressFlagName(int episode)
        {
            return UsesCountModel(episode) ? "GTotalTips" : GlobalDayFlagName(episode);
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

        // EP01-04 每个章节跳转档位（1 起）完成时 flow.mg 写入 SetDay 的 ChapterNumber
        //（即 G<篇名>Day 应达到的值，由八章数据包 flow.mg 字节码实测提取）。
        private static readonly int[] Episode01FlowDayThroughChapter =
        {
            1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 13, 15
        };

        private static readonly int[] Episode02FlowDayThroughChapter =
        {
            1, 2, 3, 4, 5, 7, 8, 10, 12, 14, 16, 20
        };

        private static readonly int[] Episode03FlowDayThroughChapter =
        {
            1, 2, 3, 4, 5, 6, 8, 11, 13, 16, 17, 19, 21
        };

        private static readonly int[] Episode04FlowDayThroughChapter =
        {
            1, 3, 4, 7, 9, 9
        };

        // EP05-08 每个章节跳转档位（1 起）完成时 GTotalTips 应达到的全局 ID 上界
        //（严格位于 PC flow.mg 的全局域：EP05=1..23、EP06=25..38、EP07=39..51、
        // EP08=通关 52，与 WindowBasis / CountModelGlobalIncludes 同域比较）。
        private static readonly int[] Episode05TipsThroughChapter =
        {
            1, 2, 3, 5, 6, 7, 9, 12, 15, 17, 18, 20, 21, 23
        };

        private static readonly int[] Episode06TipsThroughChapter =
        {
            25, 26, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38
        };

        private static readonly int[] Episode07TipsThroughChapter =
        {
            39, 40, 41, 42, 43, 45, 47, 48, 48, 49, 50, 51
        };

        /// <summary>
        /// EP01-04 章节跳转档位（1 起）完成时的 flow ChapterNumber（G&lt;篇名&gt;Day）。
        /// EP05-08 或无效档位返回 0。
        /// </summary>
        public static int FlowDayThroughChapter(int episode, int logicalChapter)
        {
            if (logicalChapter <= 0)
            {
                return 0;
            }
            switch (episode)
            {
                case 1:
                    return ValueOrLast(Episode01FlowDayThroughChapter, logicalChapter);
                case 2:
                    return ValueOrLast(Episode02FlowDayThroughChapter, logicalChapter);
                case 3:
                    return ValueOrLast(Episode03FlowDayThroughChapter, logicalChapter);
                case 4:
                    return ValueOrLast(Episode04FlowDayThroughChapter, logicalChapter);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// EP05-08 章节跳转档位（1 起）完成时的全局 GTotalTips 值；超出末位返回
        /// 通关值，未知档位返回 0。EP08 仅 1 条通关 TIPS（Id 51），前 9 档返回 0，
        /// 末档（第 10 档）起返回通关值 52（PC：Id &lt; GTotalTips）。
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
                    return logicalChapter >= EpisodeEightChapterMap.Count ? 52 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 全八章统一入口：章节跳转档位（1 起）完成时应达到的全局 TIPS 旗标值
        /// （EP01-04 为 G&lt;篇名&gt;Day，EP05-08 为 GTotalTips）。
        /// </summary>
        public static int GlobalProgressThroughChapter(int episode, int logicalChapter)
        {
            return UsesCountModel(episode)
                ? TipsThroughChapter(episode, logicalChapter)
                : FlowDayThroughChapter(episode, logicalChapter);
        }

        /// <summary>通关 / 全解锁时该章全局 TIPS 旗标应达到的完整值。</summary>
        public static int FullGlobalProgress(int episode)
        {
            switch (episode)
            {
                case 1: return Episode01FlowDayThroughChapter[Episode01FlowDayThroughChapter.Length - 1];
                case 2: return Episode02FlowDayThroughChapter[Episode02FlowDayThroughChapter.Length - 1];
                case 3: return Episode03FlowDayThroughChapter[Episode03FlowDayThroughChapter.Length - 1];
                case 4: return Episode04FlowDayThroughChapter[Episode04FlowDayThroughChapter.Length - 1];
                case 5: return Episode05TipsThroughChapter[Episode05TipsThroughChapter.Length - 1];
                case 6: return Episode06TipsThroughChapter[Episode06TipsThroughChapter.Length - 1];
                case 7: return Episode07TipsThroughChapter[Episode07TipsThroughChapter.Length - 1];
                case 8: return 52;
                default: return 0;
            }
        }

        /// <summary>
        /// 单调合并内存、持久化文件、旧版解锁档位与通关状态的全局 TIPS 进度，
        /// 保证重启、返回标题或读取早期存档后图鉴进度永不回退。
        /// </summary>
        public static int MergeGlobalProgress(
            int episode,
            int memoryValue,
            int persistedValue,
            int legacyUnlockedChapter,
            bool gameCleared)
        {
            var merged = Math.Max(0, Math.Max(memoryValue, persistedValue));
            if (gameCleared)
            {
                merged = Math.Max(merged, FullGlobalProgress(episode));
            }
            if (merged == 0 && legacyUnlockedChapter > 0)
            {
                merged = Math.Max(merged,
                    GlobalProgressThroughChapter(episode, legacyUnlockedChapter));
            }
            return merged;
        }

        private static int ValueOrLast(int[] table, int logicalChapter)
        {
            return logicalChapter <= table.Length
                ? table[logicalChapter - 1]
                : table[table.Length - 1];
        }
    }
}
