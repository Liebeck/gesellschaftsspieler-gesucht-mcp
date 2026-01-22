namespace Gesellschaftsspieler.MCPServer;

using System.ComponentModel;

public enum GamePlayTime
{
    [Description("1 - 15 Minuten")]
    MinutesMax15 = 1,

    [Description("15 - 30 Minuten")]
    MinutesMax30 = 2,

    [Description("30 - 60 Minuten")]
    MinutesMax60 = 3,

    [Description("1 - 2 Stunden")]
    HoursMax2 = 4,

    [Description("2 - 4 Stunden")]
    HoursMax4 = 5,

    [Description("4 - 8 Stunden")]
    HoursMoreMax8 = 6,

    [Description("> 8 Stunden")]
    HoursMoreThan8 = 7,
}