using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：立体工业重工塔 (Vertical Factory)。
/// 拥有垂直机动升降电梯、行车高空吊装大梁、斜角多边形料斗以及可击破的排气通风闸门。
/// </summary>
public sealed class VerticalFactoryMapPlugin : MapPluginBase
{
    public override string Id => "map_factory";
    public override string Name => "立体工业塔";
    public override string Description => "中央垂直巡回升降梯、重型高空行车大梁、多边形滑槽与通风闸门。";
    public override string EnvironmentalModifier => "垂直重力场";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层物流踏板与侧翼引桥 (Y=870, 垂直台阶 160px)
        map.AddBox(new Rectangle(120, 870, 260, 36), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1540, 870, 260, 36), TerrainStyle.SolidMetal);

        // 2. 左右多边形工业下料斗斜槽 (引导子弹与角色平滑滑行)
        Vector2[] hopperL = new[]
        {
            new Vector2(120f, 690f),
            new Vector2(360f, 690f),
            new Vector2(300f, 740f),
            new Vector2(120f, 740f)
        };
        map.AddPolygon(hopperL, TerrainStyle.SolidMetal);

        Vector2[] hopperR = new[]
        {
            new Vector2(1560f, 690f),
            new Vector2(1800f, 690f),
            new Vector2(1800f, 740f),
            new Vector2(1620f, 740f)
        };
        map.AddPolygon(hopperR, TerrainStyle.SolidMetal);

        // 3. 左右双侧登高竖井 (井宽 170px，支持蹬墙跳快速攀爬)
        map.AddBox(new Rectangle(460, 420, 36, 440), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(630, 420, 36, 440), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1254, 420, 36, 440), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1424, 420, 36, 440), TerrainStyle.SolidMetal);

        // 4. 竖井内部可击碎排气通风闸门 (可破坏砖块材质，打穿后可以直接穿井突袭)
        map.AddBox(new Rectangle(496, 620, 134, 28), TerrainStyle.DestructibleBrick, destructible: true, hp: 100f);
        map.AddBox(new Rectangle(1290, 620, 134, 28), TerrainStyle.DestructibleBrick, destructible: true, hp: 100f);

        // 5. 中央垂直机动巡回升降电梯 (从底层直达顶层天车，平滑缓动并驻留 0.7 秒，提供完美的立体纵深交通)
        map.AddMovingPlatform(
            startPos: new Vector2(960f, 850f),
            endPos: new Vector2(960f, 420f),
            size: new Vector2(220f, 32f),
            duration: 3.5f,
            curve: MotionCurve.PingPongWait,
            style: TerrainStyle.SolidMetal
        );

        // 6. 中高层检修走道与控制室 (Y=560 & Y=380，跳跃高度 160-180px)
        map.AddBox(new Rectangle(720, 680, 480, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(760, 420, 400, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(160, 480, 220, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1540, 480, 220, 32), TerrainStyle.SolidMetal);

        // 7. 顶部高空起重行车动态大梁 (钢链悬挂物理吊装箱，在顶层自由摆动)
        map.AddHangingBox(
            worldAnchor: new Vector2(960f, 50f),
            boxCenter: new Vector2(960f, 220f),
            boxSize: new Vector2(240f, 48f),
            mass: 4.0f,
            ropeLength: 145f,
            style: TerrainStyle.SolidMetal
        );

        // 8. 顶层天车导轨与安全弹跳垫
        map.AddBox(new Rectangle(680, 160, 560, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(860, 1000, 200, 30), TerrainStyle.BouncyRubber); // 地面蒸汽喷射垫
        map.AddCircle(new Vector2(960f, 300f), 35f, TerrainStyle.BouncyRubber);     // 中空弹射球
    }
}
