using System;

namespace Higurashi.IOS.Compatibility
{
    /// <summary>
    /// 原版背景（GBackgroundSet==1）下的立绘横向定位换算，对齐 PC 引擎：
    /// BurikoScriptFile.MODRyukishiRevertSpritePosition（OperationMODDrawCharacter/
    /// OperationMODDrawCharacterWithFiltering 内调用）与 AssetManager.RelativePathIsSprite。
    /// 原版立绘纹理（1280x960 → 逻辑 640x480）比主机立绘占宽更大，脚本里为主机版
    /// 排布的 x 坐标（±240/±320）会把原版立绘推出 4:3 可视区，PC 因此在使用原版
    /// 背景时把这两档位置向内收一档；其他 x 值与 sprite/portrait 以外的图层不变。
    /// </summary>
    public static class RyukishiSpritePlacementPolicy
    {
        /// <summary>
        /// PC AssetManager.RelativePathIsSprite：脚本请求路径以 sprite/ 或 portrait/
        /// 开头即视为立绘（用请求名判断，不用解析后的磁盘路径）。
        /// </summary>
        public static bool IsSpriteOrPortraitPath(string textureName)
        {
            return !string.IsNullOrEmpty(textureName) &&
                   (textureName.StartsWith("sprite/", StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("portrait/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// GBackgroundSet==1（原版背景）且请求路径是立绘时：±240→±160、±320→±240。
        /// 其余情况（含主机/重制背景、非立绘图层、其他 x 值）原样返回。
        /// </summary>
        public static int RemapSpriteX(int backgroundSet, string textureName, int x)
        {
            if (backgroundSet != 1 || !IsSpriteOrPortraitPath(textureName))
            {
                return x;
            }

            switch (x)
            {
                case 240:
                    return 160;
                case -240:
                    return -160;
                case 320:
                    return 240;
                case -320:
                    return -240;
                default:
                    return x;
            }
        }
    }
}
