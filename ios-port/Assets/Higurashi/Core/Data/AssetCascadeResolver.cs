using System;
using System.Collections.Generic;
using System.IO;

namespace Higurashi.IOS.Data
{
    /// <summary>
    /// Resolves 07th-Mod's ordered folder cascades without changing script paths.
    /// </summary>
    public sealed class AssetCascadeResolver
    {
        private readonly string _streamingAssetsRoot;

        public AssetCascadeResolver(string installedGameDataRoot)
        {
            if (string.IsNullOrWhiteSpace(installedGameDataRoot))
            {
                throw new ArgumentException("Installed game-data root is required.", nameof(installedGameDataRoot));
            }

            _streamingAssetsRoot = Path.Combine(installedGameDataRoot, "StreamingAssets");
        }

        public bool TryResolve(
            string relativePath,
            IReadOnlyList<string> cascadeFolders,
            out string resolvedPath,
            bool allowLipSyncVariantFallback = false)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                resolvedPath = null;
                return false;
            }

            if (cascadeFolders == null || cascadeFolders.Count == 0)
            {
                throw new ArgumentException("At least one cascade folder is required.", nameof(cascadeFolders));
            }

            var normalized = relativePath.Replace('\\', '/').TrimStart('/');
            for (var i = 0; i < cascadeFolders.Count; i++)
            {
                if (TryResolveInFolder(cascadeFolders[i], normalized, out resolvedPath))
                {
                    return true;
                }

                if (allowLipSyncVariantFallback &&
                    IsSpritePath(normalized) &&
                    TryResolveLipSyncVariant(cascadeFolders[i], normalized, out resolvedPath))
                {
                    return true;
                }
            }

            resolvedPath = null;
            return false;
        }

        private bool TryResolveInFolder(string folder, string relativePath, out string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                resolvedPath = null;
                return false;
            }

            var relative = folder.Trim('/', '\\') + "/" + relativePath;
            var candidate = SafePath.ResolveUnderRoot(_streamingAssetsRoot, relative);
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }

            resolvedPath = null;
            return false;
        }

        private bool TryResolveLipSyncVariant(string folder, string relativePath, out string resolvedPath)
        {
            var extension = Path.GetExtension(relativePath);
            var withoutExtension = relativePath.Substring(0, relativePath.Length - extension.Length);
            if (withoutExtension.Length == 0)
            {
                resolvedPath = null;
                return false;
            }

            var basePath = withoutExtension.Substring(0, withoutExtension.Length - 1);
            for (var variant = 0; variant < 3; variant++)
            {
                var candidate = basePath + variant + extension;
                if (!string.Equals(candidate, relativePath, StringComparison.Ordinal) &&
                    TryResolveInFolder(folder, candidate, out resolvedPath))
                {
                    return true;
                }
            }

            resolvedPath = null;
            return false;
        }

        private static bool IsSpritePath(string relativePath)
        {
            return relativePath.StartsWith("sprite/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("portrait/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

