using Microsoft.Xna.Framework;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：高能反弹迷宫 (Bumper Maze)。
/// 融入霓虹反弹圆球、空中正弦往复巡回蹦床、高空摆动弹力重锤球以及严密阶梯式跳跃动线。
/// </summary>
public sealed class BumperMazeMapPlugin : MapPluginBase
{
    public override string Id => "map_bumper";
    public override string Name => "高能反弹迷宫";
    public override string Description => "霓虹圆形弹跳力场球、横向巡回运动蹦床与悬空钟摆弹球。";
    public override string EnvironmentalModifier => "全场高弹性反弹";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层稳健侧翼阶梯 (Y=870, 垂直台阶仅 160px，轻松起跳)
        map.AddBox(new Rectangle(140, 870, 260, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1520, 870, 260, 32), TerrainStyle.SolidMetal);

        // 2. 中层错位平台 (Y=710, 距下层 160px)
        map.AddBox(new Rectangle(360, 710, 240, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1320, 710, 240, 32), TerrainStyle.SolidMetal);

        // 3. 高层悬空跳台 (Y=550, 距中层 160px)
        map.AddBox(new Rectangle(160, 550, 240, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1520, 550, 240, 32), TerrainStyle.SolidMetal);

        // 4. 顶层狙击哨台 (Y=390, 距高层 160px)
        map.AddBox(new Rectangle(380, 390, 220, 30), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1320, 390, 220, 30), TerrainStyle.SolidMetal);

        // 5. 中央主平台与下层悬空横梁
        map.AddBox(new Rectangle(760, 750, 400, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(700, 540, 520, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(840, 340, 240, 30), TerrainStyle.SolidMetal);

        // 6. 巡回运动弹性蹦床 (在中央底层缝隙水平往复平滑移动，既可作为代步浮桥，又可提供强力纵向发射)
        map.AddMovingPlatform(
            startPos: new Vector2(700f, 910f),
            endPos: new Vector2(1220f, 910f),
            size: new Vector2(200f, 32f),
            duration: 3.2f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.BouncyRubber
        );

        // 7. 标志性霓虹圆形反弹力场球 (Circular Bumpers)
        map.AddCircle(new Vector2(620f, 440f), 48f, TerrainStyle.BouncyRubber);
        map.AddCircle(new Vector2(1300f, 440f), 48f, TerrainStyle.BouncyRubber);
        map.AddCircle(new Vector2(480f, 840f), 42f, TerrainStyle.BouncyRubber);
        map.AddCircle(new Vector2(1440f, 840f), 42f, TerrainStyle.BouncyRubber);

        // 8. 顶部天花板悬挂的动态物理摆动弹力球 (钟摆物理约束)
        var pendulum = map.AddCircle(new Vector2(960f, 240f), 45f, TerrainStyle.BouncyRubber, isStatic: false, mass: 3.0f);
        pendulum.AttachedRope = new RopeConstraint(new Vector2(960f, 50f), pendulum.Body, Vector2.Zero, 190f);

        // 9. 顶部天顶与侧翼斜切折射多边形
        Vector2[] topDeflectorL = new[]
        {
            new Vector2(50f, 50f),
            new Vector2(220f, 50f),
            new Vector2(50f, 180f)
        };
        map.AddPolygon(topDeflectorL, TerrainStyle.BouncyRubber);

        Vector2[] topDeflectorR = new[]
        {
            new Vector2(1870f, 50f),
            new Vector2(1870f, 180f),
            new Vector2(1700f, 50f)
        };
        map.AddPolygon(topDeflectorR, TerrainStyle.BouncyRubber);

        // 10. 地面双弹射蹦床
        map.AddBox(new Rectangle(660, 1000, 180, 30), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1080, 1000, 180, 30), TerrainStyle.BouncyRubber);
    }
}
