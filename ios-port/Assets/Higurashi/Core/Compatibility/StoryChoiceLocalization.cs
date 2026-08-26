using System;

namespace Higurashi.IOS.Compatibility
{
    public static class StoryChoiceLocalization
    {
        public static string Localize(string value)
        {
            var text = (value ?? string.Empty).Trim();
            switch (text)
            {
                case "Open the red box":
                case "赤い箱を開ける":
                    return "打开红色箱子";
                case "Open the blue box":
                case "青い箱を開ける":
                    return "打开蓝色箱子";
                case "Plead for my life":
                case "命乞いをする":
                    return "向他求饶";
                case "Watch for an opportunity":
                case "隙を窺う":
                    return "寻找机会";
                case "At that time, I became aware of Keiichi's gaze":
                case "その時、私は圭一の視線に気がついた":
                    return "那时，我注意到了圭一的视线";
                case "And then, I turned around to face Keiichi":
                case "そして私は、圭一に振り返った":
                    return "然后，我回头看向圭一";
                case "I advised him on who he should give the doll to.":
                case "Ａ．圭一に人形を誰に渡すべきか助言した。":
                    return "Ａ．建议圭一要把人偶交给谁";
                case "I did nothing and watched the events unfold.":
                case "Ｂ．私は何もせず、成り行きを見守った。":
                    return "Ｂ．什么都不做，在旁边看着";
                case "大丈夫だ":
                    return "没事的（留在教室）";
                case "…そうだな":
                case "...そうだな":
                    return "……是啊（去办公室）";
                case "沙都子、まだかっ？！ もう時間がないぞ！！":
                case "沙都子、まだかっ？！もう時間がないぞ！！":
                    return "沙都子，还没想到吗？！已经没有时间了！！";
                case "沙都子、どうだっ？！ お前ならわかるはずだ！！":
                case "沙都子、どうだっ？！お前ならわかるはずだ！！":
                    return "沙都子，怎么样？！你能做到的！！";
                default:
                    return value ?? string.Empty;
            }
        }
    }
}
