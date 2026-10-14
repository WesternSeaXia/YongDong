using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：深渊浮空岛 (Floating Islands)。
/// 包含多边形岩石浮岛群、高空摆渡巡回浮桥、空岛悬挂动态补给箱与虚空防跌落弹跳网。
/// </summary>
public sealed class FloatingIslandsMapPlugin : MapPluginBase
{
    public override string Id => "map_islands";
    public override string Name => "深渊浮空群岛";
    public override string Description => "斜角多边形岩石空岛、高空往复摆渡飞艇、悬挂宝箱与深渊救生蹦床。";
    public override string EnvironmentalModifier => "高空虚空强风";

    protected override Vector2 DefaultPlayerSpawn => new(280f, 750f);
    protected override Vector2 DefaultAiSpawn => new(1640f, 750f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层左右梯形岩石浮岛 (凸多边形下底呈倒梯形收拢，营造悬空立体岩层感)
        // 左底层浮岛: 顶面 (120, 810) -> (420, 810), 底角 (360, 880) -> (180, 880)
        Vector2[] islandL1 = new[]
        {
            new Vector2(120f, 810f),
            new Vector2(420f, 810f),
            new Vector2(360f, 880f),
            new Vector2(180f, 880f)
        };
        map.AddPolygon(islandL1, TerrainStyle.SolidMetal);

        // 右底层浮岛
        Vector2[] islandR1 = new[]
        {
            new Vector2(1500f, 810f),
            new Vector2(1800f, 810f),
            new Vector2(1740f, 880f),
            new Vector2(1560f, 880f)
        };
        map.AddPolygon(islandR1, TerrainStyle.SolidMetal);

        // 中央底层过渡浮岛 (Y=840)
        map.AddBox(new Rectangle(810, 840, 300, 36), TerrainStyle.SolidMetal);

        // 2. 中层浮空岛台 (Y=660, 距底层仅 150px，单次跳跃即达)
        Vector2[] islandL2 = new[]
        {
            new Vector2(260f, 660f),
            new Vector2(540f, 660f),
            new Vector2(480f, 720f),
            new Vector2(320f, 720f)
        };
        map.AddPolygon(islandL2, TerrainStyle.SolidMetal);

        Vector2[] islandR2 = new[]
        {
            new Vector2(1380f, 660f),
            new Vector2(1660f, 660f),
            new Vector2(1600f, 720f),
            new Vector2(1440f, 720f)
        };
        map.AddPolygon(islandR2, TerrainStyle.SolidMetal);

        // 3. 中空摆渡巡回运动浮桥 (在左右中层空岛之间平稳往复横渡，停靠 0.6 秒便于玩家平稳登艇)
        map.AddMovingPlatform(
            startPos: new Vector2(680f, 660f),
            endPos: new Vector2(1240f, 660f),
            size: new Vector2(180f, 30f),
            duration: 3.2f,
            curve: MotionCurve.PingPongWait,
            style: TerrainStyle.SolidMetal
        );

        // 4. 高层圣殿主岛 (Y=480, 距中层 180px，支持舒适直跳)
        map.AddBox(new Rectangle(700, 480, 520, 36), TerrainStyle.SolidMetal);

        // 圣殿左立柱与右立柱 (柱间距 180px，支持天井蹬墙跳跃)
        map.AddBox(new Rectangle(760, 320, 36, 160), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1124, 320, 36, 160), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(720, 320, 480, 32), TerrainStyle.SolidMetal); // 圣殿顶层阁楼

        // 5. 圣殿下方悬挂的动态物理货箱 (钟摆物理约束，提供穿过中空时的动态踏脚点)
        map.AddHangingBox(
            worldAnchor: new Vector2(960f, 480f),
            boxCenter: new Vector2(960f, 570f),
            boxSize: new Vector2(130f, 55f),
            mass: 3.5f,
            ropeLength: 75f,
            style: TerrainStyle.SolidMetal
        );

        // 6. 可破坏冰霜/晶体脆弱掩体 (DestructibleBrick / 晶石)
        map.AddBox(new Rectangle(420, 580, 40, 80), TerrainStyle.DestructibleBrick, destructible: true, hp: 90f);
        map.AddBox(new Rectangle(1460, 580, 40, 80), TerrainStyle.DestructibleBrick, destructible: true, hp: 90f);

        // 7. 深渊防跌落强力安全蹦床 (不慎落入深渊时被极速弹射回高空，保障战斗连续性)
        map.AddBox(new Rectangle(260, 1000, 340, 30), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1320, 1000, 340, 30), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(790, 1000, 340, 30), TerrainStyle.BouncyRubber);

        // 8. 侧翼回弹贴板与高空浮空弹力球
        map.AddCircle(new Vector2(960f, 210f), 36f, TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(50, 480, 35, 160), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1835, 480, 35, 160), TerrainStyle.BouncyRubber);
    }
}
