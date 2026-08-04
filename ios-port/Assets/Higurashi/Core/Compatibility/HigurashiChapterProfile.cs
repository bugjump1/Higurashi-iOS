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
            long expectedDataPackSize,
            string expectedDataPackSha256,
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
            ExpectedDataPackSize = expectedDataPackSize;
            ExpectedDataPackSha256 = expectedDataPackSha256;
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
        public long ExpectedDataPackSize { get; }
        public string ExpectedDataPackSha256 { get; }
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
            "Higurashi-01-data.zip", 1919394073L,
            "82EA7368576B2EC1E313505E854C784B67D44FBD36472F70A54FD6BE480CEB4F",
            "higurashi-01", "onikakushi", "鬼隐篇");

        public static readonly HigurashiChapterProfile Episode02 = new HigurashiChapterProfile(
            2, "HigurashiEp02", "com.bugjump.higurashi.ep02", "HigurashiEp02_Data",
            "Higurashi-02-data.zip", 2269419044L,
            "0481E9D02ED7A993BFC0CC4BEA378DC35E16621BEEBC09578057533FE0DC1CF0",
            "higurashi-02", "watanagashi", "绵流篇");

        public static readonly HigurashiChapterProfile Episode03 = new HigurashiChapterProfile(
            3, "HigurashiEp03", "com.bugjump.higurashi.ep03", "HigurashiEp03_Data",
            "Higurashi-03-data.zip", 2079546842L,
            "13F2957DC7D6F2A6A7A9DAE737E3C4029D30A20F4E34B200AE5499C79C3A5FEF",
            "higurashi-03", "tatarigoroshi", "祟杀篇");

        public static readonly HigurashiChapterProfile Episode04 = new HigurashiChapterProfile(
            4, "HigurashiEp04", "com.bugjump.higurashi.ep04", "HigurashiEp04_Data",
            "Higurashi-04-data.zip", 1416754682L,
            "473DA280F2F4D98BE3B961FAD4D871D369CB71CF4DA51DCF395A2D542AC557ED",
            "higurashi-04", "himatsubushi", "暇溃篇");

        public static readonly HigurashiChapterProfile Episode05 = new HigurashiChapterProfile(
            5, "HigurashiEp05", "com.bugjump.higurashi.ep05", "HigurashiEp05_Data",
            "Higurashi-05-data.zip", 1961020275L,
            "AFAAD2CCBF45C9BC6729C020DE6E86A58CB741EFD889280681181B243644A302",
            "higurashi-05", "meakashi", "目明篇");

        public static readonly HigurashiChapterProfile Episode06 = new HigurashiChapterProfile(
            6, "HigurashiEp06", "com.bugjump.higurashi.ep06", "HigurashiEp06_Data",
            "Higurashi-06-data.zip", 2524592182L,
            "460C397D1F7B4B7FC756E3273A238DD1AC9FF2D4F89BFD417341038CD7B47869",
            "higurashi-06", "tsumihoroboshi", "罪灭篇");

        public static readonly HigurashiChapterProfile Episode07 = new HigurashiChapterProfile(
            7, "HigurashiEp07", "com.bugjump.higurashi.ep07", "HigurashiEp07_Data",
            "Higurashi-07-data.zip", 2565499174L,
            "189A0538BE429C9C66CC5F3B74D20ED2E945A50C64F2C50CCF1600121D6C8318",
            "higurashi-07", "minagoroshi", "皆杀篇");

        public static HigurashiChapterProfile ForEpisode(int episodeNumber)
        {
            switch (episodeNumber)
            {
                case 1: return Episode01;
                case 2: return Episode02;
                case 3: return Episode03;
                case 4: return Episode04;
                case 5: return Episode05;
                case 6: return Episode06;
                case 7: return Episode07;
                default:
                    throw new ArgumentOutOfRangeException(nameof(episodeNumber), episodeNumber,
                        "This episode has not been configured yet.");
            }
        }

        public static HigurashiChapterProfile ForProductName(string productName)
        {
            if (string.Equals(productName, Episode07.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode07;
            }
            if (string.Equals(productName, Episode06.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode06;
            }
            if (string.Equals(productName, Episode05.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode05;
            }
            if (string.Equals(productName, Episode04.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode04;
            }
            if (string.Equals(productName, Episode03.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode03;
            }
            if (string.Equals(productName, Episode02.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                return Episode02;
            }
            return Episode01;
        }
    }
}
