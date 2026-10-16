using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps.Plugins;

/// <summary>
/// 地图插件：重装悬垂工厂 (Vertical Kinetic Foundry) - 第二张官方竞技场地图。
/// 架构特征：
/// 1. 三层立体空间架构：高空跑酷钟摆层 + 中层战术地堡与通天竖井 + 底层推箱矿洞；
/// 2. 高热度开局：出生点设于第 08 行空中，正下方紧邻高弹橡胶块 [O]，开局直接火箭般弹射入空；
/// 3. 六联阶梯式悬垂钟摆：内侧4格、中间6格、外侧8格绳长，构成自然的抛物线上扬滑翔阶梯；
/// 4. 中央三层贯通可破坏砖块 [B]：击碎后诞生直通天际的垂直突破竖井；
/// 5. 底层散落 6 个自由受重力影响的物理推箱 [C]，支持近身推箱掩护与阻挡子弹。
/// </summary>
public sealed class VerticalFactoryMapPlugin : MapPluginBase
{
    public override string Id => "map_vertical_factory";
    public override string Name => "重装悬垂工厂";
    public override string Description => "三层立体机械车间、阶梯六钟摆跑酷网、高弹橡胶发射台与通天竖井。";
    public override string EnvironmentalModifier => "立体跳跃与高弹动能反应";

    // 玩家 [P] 与 AI [A] 出生点：精准对应第 08 行第 06 列与第 27 列 (Y = 440)
    // 正下方即为高弹橡胶弹跳台，开局瞬间触发动能反弹飞跃
    protected override Vector2 DefaultPlayerSpawn => new(330f, 440f);
    protected override Vector2 DefaultAiSpawn => new(1590f, 440f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // -------------------------------------------------------------
        // 1. 外围封闭边界钢墙 (1920 × 1080)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(0, 0, 1920, 60), TerrainStyle.SolidMetal);       // 天花板 (第 01 行)
        map.AddBox(new Rectangle(0, 1020, 1920, 60), TerrainStyle.SolidMetal);    // 底部基岩 (第 18 行)
        map.AddBox(new Rectangle(0, 60, 60, 960), TerrainStyle.SolidMetal);       // 左外墙 (第 01 列)
        map.AddBox(new Rectangle(1860, 60, 60, 960), TerrainStyle.SolidMetal);   // 右外墙 (第 32 列)

        // -------------------------------------------------------------
        // 2. 中央天顶垂直隔离阻挡块 (第 04~05 行 与 第 09 行，第 16~17 列)
        // -------------------------------------------------------------
        // 天顶悬垂阻挡体 (X: 900..1020, Y: 180..300)
        map.AddBox(new Rectangle(900, 180, 120, 120), TerrainStyle.SolidMetal);
        // 高空中层悬浮阻挡板 (X: 900..1020, Y: 480..540)
        map.AddBox(new Rectangle(900, 480, 120, 60), TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 3. 高弹橡胶弹跳台 [O] (Bouncy Rubber Pads)
        // -------------------------------------------------------------
        // 开局发射跳台 (第 09 行，第 06~07 列 与 第 26~27 列，位于出生点正下方)：
        map.AddBox(new Rectangle(300, 480, 120, 60), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1500, 480, 120, 60), TerrainStyle.BouncyRubber);

        // 高空中层反弹跳板 (第 07 行，第 11~12 列 与 第 21~22 列)：
        map.AddBox(new Rectangle(600, 360, 120, 60), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1200, 360, 120, 60), TerrainStyle.BouncyRubber);

        // -------------------------------------------------------------
        // 4. 六联阶梯式高空悬垂钟摆阵列 ([D] + 钢链 + 悬垂重箱 [C])
        // -------------------------------------------------------------
        // 菱形锚点在第 03 行 (Y=150)，挂点底端 Y=176

        // 4.1 内侧轻快钟摆 (第 14 列 X=810 与 第 19 列 X=1110，箱在第 08 行 Y=450，绳长 247px)
        AddDiamondAnchor(map, new Vector2(810f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(810f, 176f),
            boxCenter: new Vector2(810f, 450f),
            boxSize: new Vector2(54f, 54f),
            mass: 3.8f,
            ropeLength: 247f,
            style: TerrainStyle.SolidMetal
        );

        AddDiamondAnchor(map, new Vector2(1110f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(1110f, 176f),
            boxCenter: new Vector2(1110f, 450f),
            boxSize: new Vector2(54f, 54f),
            mass: 3.8f,
            ropeLength: 247f,
            style: TerrainStyle.SolidMetal
        );

        // 4.2 中间中程钟摆 (第 09 列 X=510 与 第 24 列 X=1410，箱在第 10 行 Y=570，绳长 367px)
        AddDiamondAnchor(map, new Vector2(510f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(510f, 176f),
            boxCenter: new Vector2(510f, 570f),
            boxSize: new Vector2(54f, 54f),
            mass: 4.2f,
            ropeLength: 367f,
            style: TerrainStyle.SolidMetal
        );

        AddDiamondAnchor(map, new Vector2(1410f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(1410f, 176f),
            boxCenter: new Vector2(1410f, 570f),
            boxSize: new Vector2(54f, 54f),
            mass: 4.2f,
            ropeLength: 367f,
            style: TerrainStyle.SolidMetal
        );

        // 4.3 外侧超长沉重钟摆 (第 04 列 X=210 与 第 29 列 X=1710，箱在第 12 行 Y=690，绳长 487px)
        AddDiamondAnchor(map, new Vector2(210f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(210f, 176f),
            boxCenter: new Vector2(210f, 690f),
            boxSize: new Vector2(54f, 54f),
            mass: 4.6f,
            ropeLength: 487f,
            style: TerrainStyle.SolidMetal
        );

        AddDiamondAnchor(map, new Vector2(1710f, 150f));
        map.AddHangingBox(
            worldAnchor: new Vector2(1710f, 176f),
            boxCenter: new Vector2(1710f, 690f),
            boxSize: new Vector2(54f, 54f),
            mass: 4.6f,
            ropeLength: 487f,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 5. 中层悬挂金属长桥与中央可破坏通天竖井 [B] (第 12~14 行)
        // -------------------------------------------------------------
        // 第 12 行左侧悬浮翼板 (梯形斜坡 + 平台，X: 600..900, Y: 660..720)
        Vector2[] midBridgeLeftVerts = new[]
        {
            new Vector2(600f, 720f),
            new Vector2(660f, 660f),
            new Vector2(900f, 660f),
            new Vector2(900f, 720f)
        };
        map.AddPolygon(midBridgeLeftVerts, TerrainStyle.SolidMetal);

        // 第 12 行右侧悬浮翼板 (平台 + 梯形斜坡，X: 1020..1320, Y: 660..720)
        Vector2[] midBridgeRightVerts = new[]
        {
            new Vector2(1020f, 720f),
            new Vector2(1020f, 660f),
            new Vector2(1260f, 660f),
            new Vector2(1320f, 720f)
        };
        map.AddPolygon(midBridgeRightVerts, TerrainStyle.SolidMetal);

        // 中央 3 层高通天竖井一体化可破坏防爆闸门 [B] (第 12~14 行，第 16~17 列，整体合为一块，X: 900..1020, Y: 660..840，宽 120px，高 180px)
        // 血量按单块砖石基准计算 (90 HP)，击破后整体碎裂瓦解，瞬间打通上下通天竖井
        map.AddBox(new Rectangle(900, 660, 120, 180), TerrainStyle.DestructibleBrick, destructible: true, hp: 90f);

        // -------------------------------------------------------------
        // 6. 中层主平台 / 底层矿洞天花板 (第 15 行，第 08~25 列，X: 420..1500, Y: 840..900)
        // -------------------------------------------------------------
        Vector2[] caveCeilingVerts = new[]
        {
            new Vector2(420f, 900f),
            new Vector2(480f, 840f),
            new Vector2(1440f, 840f),
            new Vector2(1500f, 900f)
        };
        map.AddPolygon(caveCeilingVerts, TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 7. 双侧 4 段大斜坡 (第 14~17 行，第 02~05 列 与 28~31 列)
        // -------------------------------------------------------------
        // 左侧山壁大斜坡直通底面谷底 (直角三角形凸多边形)
        Vector2[] leftSlopeVerts = new[]
        {
            new Vector2(60f, 780f),
            new Vector2(300f, 1020f),
            new Vector2(60f, 1020f)
        };
        map.AddPolygon(leftSlopeVerts, TerrainStyle.SolidMetal);

        // 右侧山壁大斜坡直通底面谷底 (直角三角形凸多边形)
        Vector2[] rightSlopeVerts = new[]
        {
            new Vector2(1860f, 780f),
            new Vector2(1860f, 1020f),
            new Vector2(1620f, 1020f)
        };
        map.AddPolygon(rightSlopeVerts, TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 8. 底层矿洞天花板下垂战术隔断掩体 (第 16 行，第 12 列 X: 660..720 与 第 21 列 X: 1200..1260，Y: 900..960)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(660, 900, 60, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1200, 900, 60, 60), TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 9. 底层矿洞散落自由物理推箱 [C] (第 17 行，散落于地面 Y=960..1020)
        // -------------------------------------------------------------
        // 4 个独立受重力、可推击、阻挡子弹的物理金属推箱 (第 09、15、18、24 列)
        int[] crateCols = { 9, 15, 18, 24 };
        foreach (int col in crateCols)
        {
            float cx = (col - 0.5f) * 60f;
            map.AddDynamicBox(
                center: new Vector2(cx, 990f),
                size: new Vector2(50f, 50f),
                mass: 3.5f,
                style: TerrainStyle.SolidMetal
            );
        }
    }

    /// <summary>
    /// 辅助添加菱形悬挂固定锚点。
    /// </summary>
    private static void AddDiamondAnchor(MapData map, Vector2 center)
    {
        Vector2[] verts = new[]
        {
            new Vector2(center.X, center.Y - 26f),
            new Vector2(center.X + 26f, center.Y),
            new Vector2(center.X, center.Y + 26f),
            new Vector2(center.X - 26f, center.Y)
        };
        map.AddPolygon(verts, TerrainStyle.SolidMetal, isStatic: true);
    }
}
