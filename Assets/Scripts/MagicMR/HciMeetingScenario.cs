namespace MagicMR
{
    /// <summary>
    /// HCI meeting 4.pdf scenes. Gestures stay the study 4D map:
    /// A pinch = style, B circle = life, C swipe = rule, D fist-burst = reward/undo.
    /// </summary>
    public enum HciMeetingScenario
    {
        LighterDemon = 0,
        WhompingWillow = 1,
        PixelCritter = 2,
        HistoricStatue = 3,
        Bedroom = 4,
        DeskPlant = 5,
        MangaPet = 6,
        SeasonTree = 7
    }

    public static class HciMeetingScenarioNames
    {
        public static string DisplayName(HciMeetingScenario scenario)
        {
            switch (scenario)
            {
                case HciMeetingScenario.LighterDemon: return "打火机 · 焦炭小恶魔";
                case HciMeetingScenario.WhompingWillow: return "打人柳";
                case HciMeetingScenario.PixelCritter: return "像素虫";
                case HciMeetingScenario.HistoricStatue: return "历史石像";
                case HciMeetingScenario.Bedroom: return "出租屋";
                case HciMeetingScenario.DeskPlant: return "桌面植物";
                case HciMeetingScenario.MangaPet: return "漫画萌宠";
                case HciMeetingScenario.SeasonTree: return "四季树";
                default: return scenario.ToString();
            }
        }

        public static string Hint(HciMeetingScenario scenario)
        {
            switch (scenario)
            {
                case HciMeetingScenario.LighterDemon:
                    return "A焦炭火  B翅膀咳嗽烟  C躲闪  D爆炸净化开花";
                case HciMeetingScenario.WhompingWillow:
                    return "A黑暗风  B枝条抽打  C树疤呼吸  D封印宝箱";
                case HciMeetingScenario.PixelCritter:
                    return "A8bit  B卡顿舞  C眩晕  D爆金币";
                case HciMeetingScenario.HistoricStatue:
                    return "A卡通  B伸懒腰  C讲史  D荷花许愿树";
                case HciMeetingScenario.Bedroom:
                    return "A水彩/赛博  B落地窗  C水母灯  D糖果雨";
                case HciMeetingScenario.DeskPlant:
                    return "A选中  B拟人说话  C长大/幼苗  D去掉效果";
                case HciMeetingScenario.MangaPet:
                    return "A漫画描边  B梦境泡  C戳破  D好感+1";
                case HciMeetingScenario.SeasonTree:
                    return "A动漫季  B拟人  C春夏秋冬  D风吹落叶";
                default:
                    return "1-4 施法  N 下一场景";
            }
        }
    }
}
