using System;

namespace Higurashi.IOS.Compatibility
{
    public static class MobileOptionDisplayName
    {
        public static string ArtSet(string name)
        {
            if (Equals(name, "Console")) return "主机版";
            if (Equals(name, "Remake")) return "重制版";
            if (Equals(name, "Original")) return "原版";
            return name ?? string.Empty;
        }

        public static string AudioSet(string name)
        {
            if (Equals(name, "New BGM/SE")) return "新版 BGM/SE";
            if (Equals(name, "GIN's BGM/SE")) return "GIN 版 BGM/SE";
            if (Equals(name, "Italo BGM/SE")) return "Italo 版 BGM/SE";
            if (Equals(name, "Original BGM/SE")) return "原版 BGM/SE";
            return name ?? string.Empty;
        }

        private static bool Equals(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
