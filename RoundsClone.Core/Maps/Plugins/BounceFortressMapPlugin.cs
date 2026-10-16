using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps.Plugins;

/// <summary>
/// 地图插件：弹跳要塞 (Bounce Fortress / Kinetic Trampoline Citadel) - 第四张官方竞技场地图。
/// 架构特征：
/// 1. 底层全域高弹蹦床 [O]：第 17 行长达 30 格 (1800px) 的超级高弹橡胶地面，失足或高速下落瞬间触发火箭般弹射；
/// 2. 中央宏伟箱塔与承重防爆核：
///    - 第 02~10 行高达 9 层双列堆叠物理推箱 [C][C] (共 18 块)，立于第 11 行金字塔拱顶基座上；
///    - 第 14~16 行中央一体化 120×180 巨型防爆砖块 [B]，击碎后不仅摧毁底部承重支柱，更让内部箱体直接坠入蹦床；
/// 3. 双层双轨巡回移动平台系统：第 09 行与第 13 行左右各部署 2 条往复移动平台（共 4 台），构筑高低纵深立体机动通道；
/// 4. 两侧双联悬挂飞台与吊装箱：第 06~07 行斜角飞台，顶面各堆叠 4 块推箱，底面悬挂双链重箱；
/// 5. 两侧金属定滑轮 [U] 与下层折射橡胶球 [Q]。
/// </summary>
public sealed class BounceFortressMapPlugin : MapPluginBase
{
    public override string Id => "map_bounce_fortress";
    public override string Name => "弹跳要塞";
    public override string Description => "全域超级蹦床地面、中央箱塔与防爆承重核、双层双轨移动平台与飞天吊箱。";
    public override string EnvironmentalModifier => "全域高弹动能反应与连锁崩塌";

    // 玩家 [P] 与 AI [A] 出生点：位于第 08 行两侧高台 (Y = 440)
    // 居高临下俯瞰整个竞技场，可轻松跃上巡回移动平台或直冲中央防线
    protected override Vector2 DefaultPlayerSpawn => new(90f, 440f);
    protected override Vector2 DefaultAiSpawn => new(1830f, 440f);

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
        // 2. 底层全域高弹橡胶蹦床 [O] (第 17 行，第 02~31 列，全长 1800px)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(60, 960, 1800, 60), TerrainStyle.BouncyRubber);

        // -------------------------------------------------------------
        // 3. 外侧天顶垂吊立柱与定滑轮圆球 [U] (第 02~05 行，第 04 与 29 列)
        // -------------------------------------------------------------
        // 左立柱与滑轮
        map.AddBox(new Rectangle(180, 60, 60, 180), TerrainStyle.SolidMetal);
        map.AddCircle(new Vector2(210f, 270f), 26f, TerrainStyle.SolidMetal, isStatic: true);

        // 右立柱与滑轮
        map.AddBox(new Rectangle(1680, 60, 60, 180), TerrainStyle.SolidMetal);
        map.AddCircle(new Vector2(1710f, 270f), 26f, TerrainStyle.SolidMetal, isStatic: true);

        // -------------------------------------------------------------
        // 4. 双侧出生点高台掩体 (第 09~10 行，第 02~03 列 与 第 30~31 列)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(60, 480, 120, 120), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1740, 480, 120, 120), TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 5. 双层双轨巡回移动平台系统 (第 09 行与第 13 行，左右各 2 台共 4 台)
        // -------------------------------------------------------------
        // 5.1 上层双轨移动平台 (第 09 行，Y=510，巡回于出生高台与中央飞台之间)
        // 左上移动平台 (第 04~07 列，X: 230..370)
        map.AddMovingPlatform(
            startPos: new Vector2(230f, 510f),
            endPos: new Vector2(370f, 510f),
            size: new Vector2(90f, 22f),
            duration: 3.0f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.SolidMetal
        );

        // 右上移动平台 (第 26~29 列，X: 1690..1550)
        map.AddMovingPlatform(
            startPos: new Vector2(1690f, 510f),
            endPos: new Vector2(1550f, 510f),
            size: new Vector2(90f, 22f),
            duration: 3.0f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.SolidMetal
        );

        // 5.2 下层双轨移动平台 (第 13 行，Y=750，巡回于下层外侧空腔与斜坡之间)
        // 左下移动平台 (第 04~07 列，X: 230..370)
        map.AddMovingPlatform(
            startPos: new Vector2(230f, 750f),
            endPos: new Vector2(370f, 750f),
            size: new Vector2(90f, 22f),
            duration: 3.0f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.SolidMetal
        );

        // 右下移动平台 (第 26~29 列，X: 1690..1550)
        map.AddMovingPlatform(
            startPos: new Vector2(1690f, 750f),
            endPos: new Vector2(1550f, 750f),
            size: new Vector2(90f, 22f),
            duration: 3.0f,
            curve: MotionCurve.SmoothSine,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 6. 两侧悬浮飞台、顶面堆叠箱 [C] 与下方悬挂重箱 (第 06~10 行，第 08~11 与 22~25 列)
        // -------------------------------------------------------------
        // --- 左侧飞台组 ---
        map.AddBox(new Rectangle(420, 360, 240, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(480, 300, 120, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(420f, 360f), new Vector2(480f, 300f), new Vector2(480f, 360f) }, TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(600f, 300f), new Vector2(660f, 360f), new Vector2(600f, 360f) }, TerrainStyle.SolidMetal);

        // 飞台顶面 4 块推箱 (第 04~05 行，第 09~10 列，底座顶面 Y=300，箱体精确贴合堆叠)
        map.AddDynamicBox(new Vector2(510f, 275f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(570f, 275f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(510f, 225f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(570f, 225f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 飞台下方悬挂双联箱 (第 08~10 行，第 09~10 列)
        map.AddHangingBox(
            worldAnchor: new Vector2(540f, 420f),
            boxCenter: new Vector2(540f, 570f),
            boxSize: new Vector2(110f, 54f),
            mass: 4.5f,
            ropeLength: 123f,
            style: TerrainStyle.SolidMetal
        );

        // --- 右侧飞台组 ---
        map.AddBox(new Rectangle(1260, 360, 240, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1320, 300, 120, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1260f, 360f), new Vector2(1320f, 300f), new Vector2(1320f, 360f) }, TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1440f, 300f), new Vector2(1500f, 360f), new Vector2(1440f, 360f) }, TerrainStyle.SolidMetal);

        // 飞台顶面 4 块推箱 (第 04~05 行，第 23~24 列，底座顶面 Y=300，箱体精确贴合堆叠)
        map.AddDynamicBox(new Vector2(1350f, 275f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(1410f, 275f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(1350f, 225f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(1410f, 225f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 飞台下方悬挂双联箱 (第 08~10 行，第 23~24 列)
        map.AddHangingBox(
            worldAnchor: new Vector2(1380f, 420f),
            boxCenter: new Vector2(1380f, 570f),
            boxSize: new Vector2(110f, 54f),
            mass: 4.5f,
            ropeLength: 123f,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 7. 中央宏伟箱塔与刚性分隔横梁 (第 02~10 行，第 16~17 列)
        // -------------------------------------------------------------
        // 第 04 行与第 07 行增设固定金属横梁 [#][#]，构建稳定的三层物理推箱舱
        map.AddBox(new Rectangle(900, 180, 120, 60), TerrainStyle.SolidMetal);  // 第 04 行横梁 (顶面 Y=180)
        map.AddBox(new Rectangle(900, 360, 120, 60), TerrainStyle.SolidMetal);  // 第 07 行横梁 (顶面 Y=360)

        // 上层推箱舱 (第 02~03 行，底面贴紧第 04 行横梁 Y=180)
        map.AddDynamicBox(new Vector2(930f, 155f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 155f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 105f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 105f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 中层推箱舱 (第 05~06 行，底面贴紧第 07 行横梁 Y=360)
        map.AddDynamicBox(new Vector2(930f, 335f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 335f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 285f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 285f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 下层推箱舱 (第 08~10 行，底面贴紧第 11 行基座顶面 Y=600，六块箱子严丝合缝静止承托)
        map.AddDynamicBox(new Vector2(930f, 575f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 575f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 525f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 525f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 475f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 475f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 8. 中央下层台基与斜坡拱顶 (第 11~13 行，第 13~20 列)
        // -------------------------------------------------------------
        // 第 11 行
        map.AddBox(new Rectangle(900, 600, 120, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(840f, 660f), new Vector2(900f, 600f), new Vector2(900f, 660f) }, TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1020f, 600f), new Vector2(1080f, 660f), new Vector2(1020f, 660f) }, TerrainStyle.SolidMetal);

        // 第 12 行
        map.AddPolygon(new[] { new Vector2(780f, 720f), new Vector2(840f, 660f), new Vector2(840f, 720f) }, TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(840, 660, 60, 60), TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 705f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 705f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1020, 660, 60, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1080f, 660f), new Vector2(1140f, 720f), new Vector2(1080f, 720f) }, TerrainStyle.SolidMetal);

        // 第 13 行
        map.AddPolygon(new[] { new Vector2(720f, 780f), new Vector2(780f, 720f), new Vector2(780f, 780f) }, TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(780, 720, 120, 60), TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 755f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 755f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1020, 720, 120, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1140f, 720f), new Vector2(1200f, 780f), new Vector2(1140f, 780f) }, TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 9. 中央一体化可破坏防爆承重核 [B] (第 14~16 行，第 16~17 列，尺寸 120×180，按单块血量算)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(900, 780, 120, 180), TerrainStyle.DestructibleBrick, destructible: true, hp: 80f);

        // 第 14 行侧翼阻挡体
        map.AddBox(new Rectangle(720, 780, 60, 60), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1140, 780, 60, 60), TerrainStyle.SolidMetal);

        // 第 15 行侧翼斜坡
        map.AddPolygon(new[] { new Vector2(600f, 900f), new Vector2(660f, 840f), new Vector2(660f, 900f) }, TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1260f, 840f), new Vector2(1320f, 900f), new Vector2(1260f, 900f) }, TerrainStyle.SolidMetal);

        // 第 16 行侧翼斜坡、基座与高弹折射圆球 [Q]
        map.AddPolygon(new[] { new Vector2(540f, 960f), new Vector2(600f, 900f), new Vector2(600f, 960f) }, TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(600, 900, 60, 60), TerrainStyle.SolidMetal);
        map.AddCircle(new Vector2(810f, 930f), 26f, TerrainStyle.BouncyRubber, isStatic: true);

        map.AddCircle(new Vector2(1110f, 930f), 26f, TerrainStyle.BouncyRubber, isStatic: true);
        map.AddBox(new Rectangle(1260, 900, 60, 60), TerrainStyle.SolidMetal);
        map.AddPolygon(new[] { new Vector2(1320f, 900f), new Vector2(1380f, 960f), new Vector2(1320f, 960f) }, TerrainStyle.SolidMetal);
    }
}
