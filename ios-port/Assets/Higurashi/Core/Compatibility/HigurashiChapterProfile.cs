using System;

namespace Higurashi.IOS.Compatibility
{
    public sealed class HigurashiChapterProfile
    {
        public HigurashiChapterProfile(
            int episodeNumber,
            string productName,
            string bundleIdentifier,
            string dataDirectoryName,
            string dataPackFileName,
            string gameId,
            string chapterSlug,
            string chineseChapterTitle)
        {
            EpisodeNumber = episodeNumber;
            EpisodeCode = episodeNumber.ToString("00");
            ProductName = productName;
            BundleIdentifier = bundleIdentifier;
            DataDirectoryName = dataDirectoryName;
            DataPackFileName = dataPackFileName;
            GameId = gameId;
            ChapterSlug = chapterSlug;
            ChineseChapterTitle = chineseChapterTitle;
        }

        public int EpisodeNumber { get; }
        public string EpisodeCode { get; }
        public string ProductName { get; }
        public string BundleIdentifier { get; }
        public string DataDirectoryName { get; }
        public string DataPackFileName { get; }
        public string GameId { get; }
        public string ChapterSlug { get; }
        public string ChineseChapterTitle { get; }
        public string FullChineseTitle => "寒蝉鸣泣之时 " + ChineseChapterTitle;
        public string ArtifactStem => "Higurashi-" + EpisodeCode + "-iOS-unsigned";
    }

    public static class HigurashiChapterProfiles
    {
        public static readonly HigurashiChapterProfile Episode01 = new HigurashiChapterProfile(
            1, "HigurashiEp01", "com.bugjump.higurashi.ep01", "HigurashiEp01_Data",
            "Higurashi-01-data.zip", "higurashi-01", "onikakushi", "鬼隐篇");

        public static readonly HigurashiChapterProfile Episode02 = new HigurashiChapterProfile(
            2, "HigurashiEp02", "com.bugjump.higurashi.ep02", "HigurashiEp02_Data",
            "Higurashi-02-data.zip", "higurashi-02", "watanagashi", "绵流篇");

        public static HigurashiChapterProfile ForEpisode(int episodeNumber)
        {
            switch (episodeNumber)
            {
                case 1: return Episode01;
                case 2: return Episode02;
                default:
                    throw new ArgumentOutOfRangeException(nameof(episodeNumber), episodeNumber,
                        "This episode has not been configured yet.");
            }
        }

        public static HigurashiChapterProfile ForProductName(string productName)
        {
            if (string.Equals(productName, Episode02.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode02;
            }
            return Episode01;
        }
    }
}
