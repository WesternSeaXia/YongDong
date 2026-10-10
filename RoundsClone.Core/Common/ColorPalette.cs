using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Common;

/// <summary>
/// 游戏调色板定义，提供 Rounds 风格的高对比度、极简霓虹色彩体系。
/// </summary>
public static class ColorPalette
{
    // 背景色系
    public static readonly Color BackgroundDark = new(18, 20, 26);
    public static readonly Color BackgroundCard = new(28, 32, 42);
    public static readonly Color GridLine = new(35, 40, 52, 100);

    // 基础角色色彩
    public static readonly Color PlayerBlue = new(50, 160, 255);
    public static readonly Color PlayerRed = new(255, 65, 80);
    public static readonly Color PlayerGreen = new(45, 230, 140);
    public static readonly Color PlayerYellow = new(255, 215, 0);

    // 战斗与状态色彩
    public static readonly Color WhiteFlash = new(255, 255, 255);
    public static readonly Color BlockShield = new(80, 220, 255);
    public static readonly Color DashTrail = new(255, 180, 50);
    public static readonly Color PoisonGreen = new(80, 255, 100);
    public static readonly Color ExplosiveOrange = new(255, 110, 30);
    public static readonly Color FreezeCyan = new(120, 230, 255);
    public static readonly Color ElectricPurple = new(190, 80, 255);

    // 地图与环境色彩
    public static readonly Color WallSolid = new(42, 48, 62);
    public static readonly Color WallOutline = new(75, 85, 110);
    public static readonly Color BouncySurface = new(255, 100, 180);
    public static readonly Color HazardPit = new(255, 50, 50, 180);

    // 卡牌品质色彩
    public static readonly Color TierCommon = new(180, 190, 205);
    public static readonly Color TierRare = new(70, 170, 255);
    public static readonly Color TierEpic = new(180, 80, 255);
    public static readonly Color TierLegendary = new(255, 190, 40);

    // UI 文字与交互
    public static readonly Color TextWhite = new(245, 245, 250);
    public static readonly Color TextGray = new(150, 160, 175);
    public static readonly Color ButtonHover = new(45, 52, 68);
    public static readonly Color ButtonActive = new(60, 70, 92);
}
