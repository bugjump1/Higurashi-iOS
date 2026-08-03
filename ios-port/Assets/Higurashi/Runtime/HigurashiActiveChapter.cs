using Higurashi.IOS.Compatibility;
using UnityEngine;

namespace Higurashi.IOS.Runtime
{
    public static class HigurashiActiveChapter
    {
        public static HigurashiChapterProfile Profile =>
            HigurashiChapterProfiles.ForProductName(Application.productName);
    }
}
