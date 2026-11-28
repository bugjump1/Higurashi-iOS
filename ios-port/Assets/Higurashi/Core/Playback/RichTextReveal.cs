using System;
using System.Text;

namespace Higurashi.IOS.Playback
{
    /// <summary>
    /// 标签感知的富文本逐字显示工具。
    /// PC (07th-Mod) 对话正文内联 &lt;size=N&gt;/&lt;color=#hex&gt;/&lt;i&gt;/&lt;b&gt; 等富文本标签；
    /// 逐字显示必须只计算可见字符、不切半标签、未闭合标签也保持合法富文本。
    /// 与 PC TextMeshPro 的 maxVisibleCharacters 语义对齐：标签字符不计入可见数，
    /// 截断点绝不落在 &lt;...&gt; 中间。
    /// </summary>
    public static class RichTextReveal
    {
        /// <summary>
        /// 计算可见字符数（仅跳过 Unity IMGUI 支持的富文本标签，普通尖括号正文按可见字符计）。
        /// 与 TMP 的 maxVisibleCharacters 计数语义一致，同时避免把 &lt;关于...&gt; 等正文误吞。
        /// </summary>
        public static int CountVisible(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var visible = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (TryMatchRichTextTag(text, i, out var tagEnd))
                {
                    i = tagEnd;
                    continue;
                }

                visible++;
            }

            return visible;
        }

        /// <summary>
        /// 返回包含前 <paramref name="visibleCount"/> 个可见字符及其间完整富文本标签的子串。
        /// 绝不在富文本标签中间截断；<paramref name="visibleCount"/> 超过可见总数时返回全文。
        /// 仅识别 Unity IMGUI 实际解析的富文本标签，非富文本 &lt;...&gt; 按普通可见字符逐字显示。
        /// 截断时若上一个富文本标签尚未闭合，结果中该标签保持开启（与 PC TMP 行为一致：
        /// 标签作用于已显示的后续可见字符，待后续揭示到 &lt;/...&gt; 时闭合）。
        /// </summary>
        public static string VisibleSubstring(string text, int visibleCount)
        {
            if (string.IsNullOrEmpty(text) || visibleCount <= 0)
            {
                return string.Empty;
            }

            if (visibleCount >= CountVisible(text))
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            var seen = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (TryMatchRichTextTag(text, i, out var tagEnd))
                {
                    // 截断点之后不再开启新标签（避免塞入无可见内容的尾标签）。
                    if (seen >= visibleCount)
                    {
                        break;
                    }

                    sb.Append(text, i, tagEnd - i + 1);
                    i = tagEnd;
                    continue;
                }

                if (seen >= visibleCount)
                {
                    break;
                }

                sb.Append(text[i]);
                seen++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 判断是否已揭示全部可见字符（与 <see cref="CountVisible"/> 配套）。
        /// </summary>
        public static bool IsRevealComplete(string text, int visibleCount)
        {
            return visibleCount >= CountVisible(text);
        }

        private static bool TryMatchRichTextTag(string text, int start, out int end)
        {
            end = -1;
            if (start < 0 || start >= text.Length || text[start] != '<')
            {
                return false;
            }

            var close = text.IndexOf('>', start + 1);
            if (close <= start + 1)
            {
                return false;
            }

            // 闭合 '>' 之前若再遇到 '<'，说明前一个 '<' 是普通正文而非富文本标签。
            if (text.IndexOf('<', start + 1, close - start - 1) >= 0)
            {
                return false;
            }

            var inner = text.Substring(start + 1, close - start - 1);
            if (!IsSupportedRichTextTag(inner))
            {
                return false;
            }

            end = close;
            return true;
        }

        private static bool IsSupportedRichTextTag(string inner)
        {
            return inner.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("/b", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("i", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("/i", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("/color", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("/size", StringComparison.OrdinalIgnoreCase) ||
                   inner.Equals("/material", StringComparison.OrdinalIgnoreCase) ||
                   (inner.StartsWith("color=", StringComparison.OrdinalIgnoreCase) &&
                    inner.Length > "color=".Length) ||
                   (inner.StartsWith("size=", StringComparison.OrdinalIgnoreCase) &&
                    inner.Length > "size=".Length) ||
                   (inner.StartsWith("material=", StringComparison.OrdinalIgnoreCase) &&
                    inner.Length > "material=".Length) ||
                   (inner.StartsWith("quad ", StringComparison.OrdinalIgnoreCase) &&
                    inner.Length > "quad ".Length);
        }
    }
}
