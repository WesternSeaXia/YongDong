using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：工业机械悬垂竞技场 (Industrial Aerial Arena) - 最新无尖刺流畅交战版。
/// 根据最新 32×18 可视化网格布局构建：
/// 1. 出生点位于左右平原山巅 (第 14 行，第 05 与 28 列)；
/// 2. 中央横跨高空导轨巡回平台 (第 08 行，第 11~22 列，全长 600px 贯通大区间)；
/// 3. 深渊上方加长型 4 连钟摆箱 (第 10~13 行，高 228px，靠近坡底起跳极其顺畅)；
/// 4. 彻底移除尖刺深渊陷阱，改为双侧斜坡平滑汇入中央大基座，支持全地形无阻碍交火奔跑；
/// 5. 中央基座双瞭望战术凸台与中央下沉射击堑壕 (第 14 行，第 13~20 列)；
/// 6. 外侧滑轮吊装巨箱与天顶极寒冰桥。
/// </summary>
public sealed class IndustrialArenaMapPlugin : MapPluginBase
{
    public override string Id => "map_industrial";
    public override string Name => "工业机械悬垂竞技场";
    public override string Description => "滑轮吊装巨箱、高空平移穿梭轨道、加长钟摆与中央双塔堑壕堡垒。";
    public override string EnvironmentalModifier => "高空贯通巡航与多钟摆力学";

    // 玩家 [P] 与 AI [A] 出生点：精准对应第 14 行第 05 列与第 28 列 (Y = 800)
    protected override Vector2 DefaultPlayerSpawn => new(270f, 800f);
    protected override Vector2 DefaultAiSpawn => new(1650f, 800f);

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
        // 2. 双侧阶梯式工业掩体墙与悬垂立柱 (第 08~12 行)
        // -------------------------------------------------------------
        // 左侧阶梯平台 (Y=540, 600, 660)
        map.AddBox(new Rectangle(60, 540, 180, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(60, 600, 120, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(60, 660, 60, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(360, 420, 60, 180), TerrainStyle.SolidMetal);    // 左侧悬垂立柱 (第 07 列，第 08~10 行)

        // 右侧阶梯平台 (镜像对称)
        map.AddBox(new Rectangle(1680, 540, 180, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1740, 600, 120, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1800, 660, 60, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1500, 420, 60, 180), TerrainStyle.SolidMetal);  // 右侧悬垂立柱 (第 26 列，第 08~10 行)

        // -------------------------------------------------------------
        // 3. 双侧 3 段平滑冲刺山丘 (梯形凸多边形，绝无内缝卡顿)
        // -------------------------------------------------------------
        // 左山丘：底角(60, 1020) -> 上坡坡顶(240, 840) -> 平顶(360, 840) -> 下坡坡脚(540, 1020)
        Vector2[] leftHillVerts = new[]
        {
            new Vector2(60f, 1020f),
            new Vector2(240f, 840f),
            new Vector2(360f, 840f),
            new Vector2(540f, 1020f)
        };
        map.AddPolygon(leftHillVerts, TerrainStyle.SolidMetal);

        // 右山丘：底角(1380, 1020) -> 上坡坡顶(1560, 840) -> 平顶(1680, 840) -> 下坡坡脚(1860, 1020)
        Vector2[] rightHillVerts = new[]
        {
            new Vector2(1380f, 1020f),
            new Vector2(1560f, 840f),
            new Vector2(1680f, 840f),
            new Vector2(1860f, 1020f)
        };
        map.AddPolygon(rightHillVerts, TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 4. 中央核心大基座与双瞭望凸台战术堑壕 (第 14~17 行，第 10~23 列，全面取代旧尖刺深渊)
        // -------------------------------------------------------------
        // 中央大基座 (梯形大坡面，两翼平滑向上收窄至中央平台)：
        // 底角左(540, 1020) -> 坡顶左(720, 840) -> 坡顶右(1200, 840) -> 底角右(1380, 1020)
        Vector2[] centerBaseVerts = new[]
        {
            new Vector2(540f, 1020f),
            new Vector2(720f, 840f),
            new Vector2(1200f, 840f),
            new Vector2(1380f, 1020f)
        };
        map.AddPolygon(centerBaseVerts, TerrainStyle.SolidMetal);

        // 左侧瞭望战术凸台 (第 14 行，第 13~15 列)：
        // 底角左(720, 840) -> 坡顶左(780, 780) -> 坡顶右(840, 780) -> 底角右(900, 840)
        Vector2[] leftTowerVerts = new[]
        {
            new Vector2(720f, 840f),
            new Vector2(780f, 780f),
            new Vector2(840f, 780f),
            new Vector2(900f, 840f)
        };
        map.AddPolygon(leftTowerVerts, TerrainStyle.SolidMetal);

        // 右侧瞭望战术凸台 (第 14 行，第 18~20 列)：
        // 底角左(1020, 840) -> 坡顶左(1080, 780) -> 坡顶右(1140, 780) -> 底角右(1200, 840)
        Vector2[] rightTowerVerts = new[]
        {
            new Vector2(1020f, 840f),
            new Vector2(1080f, 780f),
            new Vector2(1140f, 780f),
            new Vector2(1200f, 840f)
        };
        map.AddPolygon(rightTowerVerts, TerrainStyle.SolidMetal);

        // 注：X=900..1020 (第 16~17 列) 处自然形成中央下沉战术射击堑壕 (宽 120px，地表 Y=840)

        // -------------------------------------------------------------
        // 5. 天顶悬空极寒冰霜天桥 (第 05 行，第 13~20 列，全长 480px)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(720, 240, 480, 36), TerrainStyle.IceCrystal);

        // -------------------------------------------------------------
        // 6. 外侧滑轮重型吊装集装箱机构 ([U] + 钢链 + 3连横向箱 [C][C][C])
        // -------------------------------------------------------------
        // 左侧滑轮金属圆球 (第 03 行，第 04 列)
        map.AddCircle(new Vector2(210f, 150f), 26f, TerrainStyle.SolidMetal, isStatic: true);
        // 左侧悬挂大型重型箱 (第 07 行，第 03~05 列)
        map.AddHangingBox(
            worldAnchor: new Vector2(210f, 150f),
            boxCenter: new Vector2(210f, 390f),
            boxSize: new Vector2(170f, 54f),
            mass: 4.5f,
            ropeLength: 213f,
            style: TerrainStyle.SolidMetal
        );

        // 右侧滑轮金属圆球 (第 03 行，第 29 列)
        map.AddCircle(new Vector2(1710f, 150f), 26f, TerrainStyle.SolidMetal, isStatic: true);
        // 右侧悬挂大型重型箱 (第 07 行，第 28~30 列)
        map.AddHangingBox(
            worldAnchor: new Vector2(1710f, 150f),
            boxCenter: new Vector2(1710f, 390f),
            boxSize: new Vector2(170f, 54f),
            mass: 4.5f,
            ropeLength: 213f,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 7. 加长型 4 连吊装钟摆 ([D] + 钢链 + 4连竖向箱 [C][C][C][C])
        // -------------------------------------------------------------
        // 左侧几何菱形 (第 06 行，第 10 列)
        Vector2[] leftDiamondVerts = new[]
        {
            new Vector2(570f, 304f),
            new Vector2(598f, 330f),
            new Vector2(570f, 356f),
            new Vector2(542f, 330f)
        };
        map.AddPolygon(leftDiamondVerts, TerrainStyle.SolidMetal);

        // 4 连竖向箱：涵盖第 10、11、12、13 行，总高度 228px，靠近山谷坡底，跳跃攀爬极其顺畅
        map.AddHangingBox(
            worldAnchor: new Vector2(570f, 356f),
            boxCenter: new Vector2(570f, 660f),
            boxSize: new Vector2(54f, 228f),
            mass: 4.8f,
            ropeLength: 189f,
            style: TerrainStyle.SolidMetal
        );

        // 右侧几何菱形 (第 06 行，第 23 列)
        Vector2[] rightDiamondVerts = new[]
        {
            new Vector2(1350f, 304f),
            new Vector2(1378f, 330f),
            new Vector2(1350f, 356f),
            new Vector2(1322f, 330f)
        };
        map.AddPolygon(rightDiamondVerts, TerrainStyle.SolidMetal);

        // 右侧 4 连竖向钟摆箱
        map.AddHangingBox(
            worldAnchor: new Vector2(1350f, 356f),
            boxCenter: new Vector2(1350f, 660f),
            boxSize: new Vector2(54f, 228f),
            mass: 4.8f,
            ropeLength: 189f,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 8. 中央高空贯通巡航移动平台与导轨线 (第 08 行，第 11~22 列，全长 600px)
        // -------------------------------------------------------------
        // 移动平台在第 08 行 (Y=450) 的导轨区间平滑往复巡游
        map.AddMovingPlatform(
            startPos: new Vector2(660f, 450f),
            endPos: new Vector2(1260f, 450f),
            size: new Vector2(110f, 24f),
            duration: 3.8f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.SolidMetal
        );
    }
}
