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
        /// 计算可见字符数（&lt;...&gt; 标签内的字符不计）。
        /// 与 TMP 的 maxVisibleCharacters 计数语义一致。
        /// </summary>
        public static int CountVisible(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var visible = 0;
            var inTag = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (inTag)
                {
                    if (c == '>')
                    {
                        inTag = false;
                    }
                    continue;
                }

                visible++;
            }

            return visible;
        }

        /// <summary>
        /// 返回包含前 <paramref name="visibleCount"/> 个可见字符及其间完整标签的子串。
        /// 绝不在标签中间截断；<paramref name="visibleCount"/> 超过可见总数时返回全文。
        /// 截断时若上一个标签尚未闭合，结果中该标签保持开启（与 PC TMP 行为一致：
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
            var inTag = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '<')
                {
                    // 截断点之后不再开启新标签（避免塞入无可见内容的尾标签）。
                    if (seen >= visibleCount)
                    {
                        break;
                    }
                    inTag = true;
                    sb.Append(c);
                    continue;
                }

                if (inTag)
                {
                    // 截断前已进入的标签必须完整补全到 '>'（绝不切半标签）。
                    sb.Append(c);
                    if (c == '>')
                    {
                        inTag = false;
                    }
                    continue;
                }

                if (seen >= visibleCount)
                {
                    break;
                }

                sb.Append(c);
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
    }
}
