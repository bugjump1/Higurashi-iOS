using System;
using System.IO;

namespace Higurashi.IOS.Data
{
    public static class SafePath
    {
        public static string ResolveUnderRoot(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("Root path is required.", nameof(root));
            }

            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOf('\0') >= 0)
            {
                throw new InvalidDataException("Data-pack path is empty or invalid.");
            }

            var normalizedRelative = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalizedRelative) || normalizedRelative.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("Absolute paths are not allowed in a data pack.");
            }

            var parts = normalizedRelative.Split(Path.DirectorySeparatorChar);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                {
                    throw new InvalidDataException("Parent traversal is not allowed in a data pack.");
                }
            }

            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative));
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!candidate.StartsWith(fullRoot, comparison))
            {
                throw new InvalidDataException("Data-pack path escapes the target directory.");
            }

            return candidate;
        }
    }
}
