using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// PC (07th-Mod) AssetManager 的目录级联规则。
    /// 参考反编译 Assets.Scripts.Core.AssetManagement.AssetManager.PathToAssetWithName：
    /// PC 上所有图片（立绘、背景、CG、图层）共用同一条级联——
    ///   1. GBackgroundSet == 1 且某个 artset 声明了 OGBackgrounds 时，OGBackgrounds 前置；
    ///   2. 遍历当前 artset 声明的目录，GBackgroundSet == 0 时跳过 OGBackgrounds；
    ///   3. 目录名去重、保持声明顺序（artset 由 init.txt 的 ModAddArtset 注册）。
    /// 八章脚本注册相同的三套：Console=CG / Remake=CGAlt:CG / Original=OGBackgrounds:OGSprites:CG。
    /// </summary>
    public static class VisualStyleFolderPolicy
    {
        public static string[] SpriteFoldersFor(
            int styleIndex,
            int artSetCount,
            IReadOnlyList<string> declaredFolders)
        {
            // 脚本通过 ModAddArtset 声明的目录优先（严格对齐 PC 的运行时注册机制）。
            var declared = NonBlankFolders(declaredFolders);
            if (declared.Count > 0)
            {
                return declared.ToArray();
            }

            // 无脚本声明时的兜底（与历史上硬编码的三套目录一致）。
            if (artSetCount >= 3)
            {
                switch (styleIndex)
                {
                    case 1:
                        return new[] { "CGAlt", "CG" };
                    case 2:
                        return new[] { "OGBackgrounds", "OGSprites", "CG" };
                    default:
                        return new[] { "CG" };
                }
            }

            return new[] { "CG" };
        }

        /// <summary>
        /// PC AssetManager.PathToAssetWithName 的统一级联公式，
        /// 立绘、背景、CG 与图层加载都应使用同一条目录序列。
        /// </summary>
        /// <param name="spriteStyleIndex">立绘风格（= PC GArtStyle / CurrentArtsetIndex）。</param>
        /// <param name="backgroundStyleIndex">背景风格（= PC GBackgroundSet，0 主机 / 1 原版）。</param>
        /// <param name="artSetCount">脚本注册的 artset 总数。</param>
        /// <param name="declaredSpriteFolders">当前立绘风格 artset 的声明目录。</param>
        /// <param name="ogBackgroundsDeclared">任意 artset 是否声明过 OGBackgrounds
        /// （对应 PC 的 MaybeOriginalBackgroundCascadePath；没有任何 artset 声明时
        /// 即使 GBackgroundSet==1 也不前置，与 PC 行为一致）。</param>
        public static string[] UnifiedLookupFolders(
            int spriteStyleIndex,
            int backgroundStyleIndex,
            int artSetCount,
            IReadOnlyList<string> declaredSpriteFolders,
            bool ogBackgroundsDeclared)
        {
            var spriteFolders = SpriteFoldersFor(
                spriteStyleIndex, artSetCount, declaredSpriteFolders);
            var result = new List<string>();

            if (backgroundStyleIndex == 1 && ogBackgroundsDeclared)
            {
                AddUnique(result, "OGBackgrounds");
            }

            for (var i = 0; i < spriteFolders.Length; i++)
            {
                var folder = spriteFolders[i];
                if (backgroundStyleIndex == 0 && IsOgBackgrounds(folder))
                {
                    continue;
                }

                AddUnique(result, folder);
            }

            if (result.Count == 0)
            {
                result.Add("CG");
            }

            return result.ToArray();
        }

        private static List<string> NonBlankFolders(IReadOnlyList<string> folders)
        {
            var result = new List<string>();
            if (folders == null)
            {
                return result;
            }

            for (var i = 0; i < folders.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(folders[i]))
                {
                    result.Add(folders[i]);
                }
            }

            return result;
        }

        private static bool IsOgBackgrounds(string folder)
        {
            return string.Equals(folder, "OGBackgrounds", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddUnique(List<string> folders, string folder)
        {
            for (var i = 0; i < folders.Count; i++)
            {
                if (string.Equals(folders[i], folder, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            folders.Add(folder);
        }
    }
}
