using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地形材质与物理样式类型。
/// </summary>
public enum TerrainStyle
{
    /// <summary>
    /// 标准高强合金金属（稳固、摩擦力大、标准反弹）。
    /// </summary>
    SolidMetal,

    /// <summary>
    /// 高弹性霓虹橡胶（极高反弹系数，适合弹跳板与反弹圆球）。
    /// </summary>
    BouncyRubber,

    /// <summary>
    /// 可破坏强化砖石/掩体墙（承受子弹攻击后破裂粉碎，开辟新战术通道）。
    /// </summary>
    DestructibleBrick,

    /// <summary>
    /// 极寒冰霜晶体（极低摩擦阻力，滑行加速）。
    /// </summary>
    IceCrystal,

    /// <summary>
    /// 高危尖刺/危险地形（角色接触受创并被击退）。
    /// </summary>
    HazardSpike
}

/// <summary>
/// 运动平台曲线类型。
/// </summary>
public enum MotionCurve
{
    /// <summary>
    /// 平滑正弦缓动（两端自然减速回弹）。
    /// </summary>
    SmoothSine,

    /// <summary>
    /// 匀速线性往复。
    /// </summary>
    Linear,

    /// <summary>
    /// 往复运动且在端点短暂驻留停靠。
    /// </summary>
    PingPongWait
}

/// <summary>
/// 地形样式视觉与物理参数配置辅助类。
/// </summary>
public static class TerrainStyleHelper
{
    public static Color GetFillColor(TerrainStyle style) => style switch
    {
        TerrainStyle.SolidMetal => ColorPalette.WallSolid,
        TerrainStyle.BouncyRubber => ColorPalette.BouncySurface,
        TerrainStyle.DestructibleBrick => new Color(138, 54, 38), // 砖红陶土色
        TerrainStyle.IceCrystal => new Color(35, 95, 140, 220),   // 冰霜半透靛蓝
        TerrainStyle.HazardSpike => new Color(75, 25, 25),        // 危险暗红
        _ => ColorPalette.WallSolid
    };

    public static Color GetOutlineColor(TerrainStyle style) => style switch
    {
        TerrainStyle.SolidMetal => ColorPalette.WallOutline,
        TerrainStyle.BouncyRubber => Color.White,
        TerrainStyle.DestructibleBrick => new Color(245, 140, 80), // 砖块磨损明橙描边
        TerrainStyle.IceCrystal => new Color(175, 235, 255),       // 冰晶高光亮青描边
        TerrainStyle.HazardSpike => new Color(255, 60, 60),        // 警戒红
        _ => ColorPalette.WallOutline
    };

    public static float GetBounciness(TerrainStyle style) => style switch
    {
        TerrainStyle.BouncyRubber => 0.84f,
        TerrainStyle.IceCrystal => 0.25f,
        TerrainStyle.DestructibleBrick => 0.10f,
        _ => 0.20f
    };

    public static float GetFriction(TerrainStyle style) => style switch
    {
        TerrainStyle.IceCrystal => 0.05f,
        TerrainStyle.BouncyRubber => 0.50f,
        _ => 0.85f
    };
}
