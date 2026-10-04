using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Higurashi.IOS.Playback
{
    /// <summary>
    /// 把对话富文本里的相对字号标签（&lt;size=±N&gt;）转换为绝对像素字号，
    /// 以规避 IMGUI 渲染路径对相对字号支持不确定的问题；实际效果仍待
    /// GitHub Actions 构建后的真机验证。
    /// 背景：官方 styled text 文档（uGUI 2.0）对 size 只示例绝对像素值、
    /// 未记载相对语义，而 PC 07th-Mod 脚本（TextMeshPro 语义）使用
    /// &lt;size=-2&gt;/&lt;size=+4&gt;；设备观察到标签被吞但字号不变。
    ///
    /// 规则：
    /// - &lt;size=N&gt;（无符号绝对值，N&gt;0）保持原样；
    /// - &lt;size=±N&gt;（相对）按"当前生效字号"换算为绝对值（最小钳到 1），
    ///   当前生效字号 = baseFontSize 与已打开的 size 标签叠加；
    /// - &lt;/size&gt; 恢复外层生效字号；多余的 &lt;/size&gt; 原样保留；
    /// - 非法或未知的 size 参数（如 &lt;size=abc&gt;）原样保留，不静默改写正文；
    /// - 其他任何标签（color/i/b/未知）原样保留，不影响字号栈；
    /// - 原始字符串不被修改，返回新字符串。
    /// </summary>
    public static class RichTextSizeNormalize
    {
        public static string Normalize(string text, int baseFontSize)
        {
            if (string.IsNullOrEmpty(text) || baseFontSize <= 0 ||
                text.IndexOf("<size", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return text;
            }

            var effective = baseFontSize;
            var stack = new Stack<int>();
            var result = new StringBuilder(text.Length + 16);
            var index = 0;
            while (index < text.Length)
            {
                var ch = text[index];
                if (ch != '<')
                {
                    result.Append(ch);
                    index++;
                    continue;
                }

                var close = text.IndexOf('>', index + 1);
                if (close < 0)
                {
                    // 没有闭合 '>' 的残缺标签：原样输出剩余内容（与 IMGUI
                    // "未闭合标签按普通文本渲染"的兜底一致）。
                    result.Append(text, index, text.Length - index);
                    break;
                }

                var inner = text.Substring(index + 1, close - index - 1);
                if (IsSizeOpenTag(inner, out var parameter))
                {
                    if (TryResolveSize(parameter, effective, out var absolute))
                    {
                        result.Append("<size=").Append(absolute).Append('>');
                        stack.Push(effective);
                        effective = absolute;
                    }
                    else
                    {
                        // 非法 size 参数：保持标签原样（协议要求不静默改写正文）。
                        AppendOriginal(result, text, index, close);
                    }
                }
                else if (IsSizeCloseTag(inner))
                {
                    result.Append("</size>");
                    if (stack.Count > 0)
                    {
                        effective = stack.Pop();
                    }
                }
                else
                {
                    // 其他标签（color/i/b/未知）原样保留，不影响字号栈。
                    AppendOriginal(result, text, index, close);
                }

                index = close + 1;
            }

            return result.ToString();
        }

        private static void AppendOriginal(StringBuilder result, string text, int start, int close)
        {
            result.Append(text, start, close - start + 1);
        }

        private static bool IsSizeOpenTag(string inner, out string parameter)
        {
            parameter = null;
            return inner.StartsWith("size=", StringComparison.OrdinalIgnoreCase) &&
                   (parameter = inner.Substring("size=".Length)) != null;
        }

        private static bool IsSizeCloseTag(string inner)
        {
            return inner.Equals("/size", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveSize(string parameter, int effective, out int absolute)
        {
            absolute = 0;
            if (string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            var trimmed = parameter.Trim();
            var isRelative = trimmed[0] == '+' || trimmed[0] == '-';
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var value))
            {
                return false;
            }

            if (!isRelative)
            {
                if (value <= 0)
                {
                    return false; // 非正的"绝对"值属于非法输入，保持原样。
                }

                absolute = value;
                return true;
            }

            // 相对值：基于当前生效字号换算，最小钳到 1（字号没有可渲染的 ≤0 语义）。
            absolute = Math.Max(1, effective + value);
            return true;
        }
    }
}
