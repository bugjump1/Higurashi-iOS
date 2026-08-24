using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Compatibility
{
    public static class VisualStyleFolderPolicy
    {
        public static string[] SpriteFoldersFor(
            int styleIndex,
            int artSetCount,
            IReadOnlyList<string> declaredFolders)
        {
            if (artSetCount >= 3)
            {
                switch (styleIndex)
                {
                    case 1:
                        return new[] { "CGAlt" };
                    case 2:
                        return new[] { "OGSprites" };
                    default:
                        return new[] { "CG" };
                }
            }

            return FirstDeclaredFolderOnly(declaredFolders);
        }

        public static string[] BackgroundFoldersFor(
            int backgroundStyleIndex,
            int artSetCount,
            IReadOnlyList<string> declaredFolders)
        {
            if (artSetCount >= 3 && backgroundStyleIndex == 1)
            {
                return new[] { "OGBackgrounds" };
            }

            if (artSetCount >= 3 && backgroundStyleIndex == 0)
            {
                return new[] { "CG" };
            }

            return FirstDeclaredFolderOnly(declaredFolders);
        }

        private static string[] FirstDeclaredFolderOnly(IReadOnlyList<string> folders)
        {
            if (folders != null)
            {
                for (var i = 0; i < folders.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(folders[i]))
                    {
                        return new[] { folders[i] };
                    }
                }
            }

            return new[] { "CG" };
        }
    }
}
