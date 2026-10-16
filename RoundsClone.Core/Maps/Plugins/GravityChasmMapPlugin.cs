using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps.Plugins;

/// <summary>
/// 地图插件：重力冰渊大峡谷 (Gravity Glacial Chasm) - 第三张官方竞技场地图。
/// 架构特征：
/// 1. 巨型 V 字型向心大漏斗：从两侧高台 (第 07 行) 贯穿滑至底部 (第 17 行)，落差达 600px 的对称长坡道；
/// 2. 底部极寒冰晶地面 [I]：长达 480px 的零摩擦冰场，滑降冲入后产生极高动能漂移射击体验；
/// 3. 空中高弹橡胶圆球 [Q] 与倒三角折射伞 [V]：提供多重非欧几何反弹掩护；
/// 4. 双侧 3 连悬挂吊箱 [C][C][C] 与中央台柱堆叠物理箱 [C]：受爆炸冲击后形成物理滚石雪崩；
/// 5. 链条悬挂节点镶嵌可破坏防爆砖块 [B]。
/// </summary>
public sealed class GravityChasmMapPlugin : MapPluginBase
{
    public override string Id => "map_gravity_chasm";
    public override string Name => "重力冰渊大峡谷";
    public override string Description => "巨型向心V字大漏斗、底部极寒滑溜冰场、高空倒三角折射阻挡与悬垂吊箱雪崩。";
    public override string EnvironmentalModifier => "巨型向心坡道与极寒零摩擦漂移";

    // 玩家 [P] 与 AI [A] 出生点：精准对应第 06 行第 02 列与第 31 列 (Y = 330)
    // 居高临下俯瞰整个宏伟的向心大漏斗
    protected override Vector2 DefaultPlayerSpawn => new(90f, 330f);
    protected override Vector2 DefaultAiSpawn => new(1830f, 330f);

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
        // 2. 双侧对称巨型 V 字向心大斜坡桥架 (第 07~17 行，6-边凸多边形空心刚性桥拱)
        // -------------------------------------------------------------
        // 左侧向心大坡道：
        // 顶面站台(60, 360) -> 坡顶(120, 360) -> 谷底坡脚(720, 960) -> 底部触地点(720, 1020) -> 底部内角(660, 1020) -> 顶板底角(60, 420)
        Vector2[] leftChasmRampVerts = new[]
        {
            new Vector2(60f, 360f),
            new Vector2(120f, 360f),
            new Vector2(720f, 960f),
            new Vector2(720f, 1020f),
            new Vector2(660f, 1020f),
            new Vector2(60f, 420f)
        };
        map.AddPolygon(leftChasmRampVerts, TerrainStyle.SolidMetal);

        // 右侧向心大坡道 (镜像对称)：
        // 坡顶(1800, 360) -> 顶面站台(1860, 360) -> 顶板底角(1860, 420) -> 底部内角(1260, 1020) -> 底部触地点(1200, 1020) -> 谷底坡脚(1200, 960)
        Vector2[] rightChasmRampVerts = new[]
        {
            new Vector2(1800f, 360f),
            new Vector2(1860f, 360f),
            new Vector2(1860f, 420f),
            new Vector2(1260f, 1020f),
            new Vector2(1200f, 1020f),
            new Vector2(1200f, 960f)
        };
        map.AddPolygon(rightChasmRampVerts, TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 3. 峡谷最深处极寒冰晶地面 [I] (第 17 行，第 13~20 列，全长 480px，Y: 960..1020)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(720, 960, 480, 60), TerrainStyle.IceCrystal);

        // -------------------------------------------------------------
        // 4. 空中高弹橡胶反弹跳台 [O][O] (第 04 行，第 04~05 列 X: 180..300 与 第 28~29 列 X: 1620..1740，Y: 180..240)
        // -------------------------------------------------------------
        map.AddBox(new Rectangle(180, 180, 120, 60), TerrainStyle.BouncyRubber);
        map.AddBox(new Rectangle(1620, 180, 120, 60), TerrainStyle.BouncyRubber);

        // -------------------------------------------------------------
        // 5. 悬空侧翼阻挡立柱、倒三角折射盾 [V] 与顶端推箱 [C]
        // -------------------------------------------------------------
        // 左上侧翼 (第 04 行 Col 07 刚性方块 + 第 05 行 Col 07 倒三角伞)
        map.AddBox(new Rectangle(360, 180, 60, 60), TerrainStyle.SolidMetal);
        Vector2[] leftV1Verts = new[]
        {
            new Vector2(360f, 240f),
            new Vector2(420f, 240f),
            new Vector2(390f, 300f)
        };
        map.AddPolygon(leftV1Verts, TerrainStyle.SolidMetal);

        // 右上侧翼 (第 04 行 Col 26 刚性方块 + 第 05 行 Col 26 倒三角伞)
        map.AddBox(new Rectangle(1500, 180, 60, 60), TerrainStyle.SolidMetal);
        Vector2[] rightV1Verts = new[]
        {
            new Vector2(1500f, 240f),
            new Vector2(1560f, 240f),
            new Vector2(1530f, 300f)
        };
        map.AddPolygon(rightV1Verts, TerrainStyle.SolidMetal);

        // 左中侧翼 (第 06~07 行 Col 10 双格立柱 + 第 08 行 Col 10 倒三角伞)
        map.AddBox(new Rectangle(540, 300, 60, 120), TerrainStyle.SolidMetal);
        Vector2[] leftV2Verts = new[]
        {
            new Vector2(540f, 420f),
            new Vector2(600f, 420f),
            new Vector2(570f, 480f)
        };
        map.AddPolygon(leftV2Verts, TerrainStyle.SolidMetal);
        // 左中侧翼立柱顶端动态推箱 [C] (第 05 行 Col 10，X=570, Y=270)
        map.AddDynamicBox(new Vector2(570f, 270f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 右中侧翼 (第 06~07 行 Col 23 双格立柱 + 第 08 行 Col 23 倒三角伞)
        map.AddBox(new Rectangle(1320, 300, 60, 120), TerrainStyle.SolidMetal);
        Vector2[] rightV2Verts = new[]
        {
            new Vector2(1320f, 420f),
            new Vector2(1380f, 420f),
            new Vector2(1350f, 480f)
        };
        map.AddPolygon(rightV2Verts, TerrainStyle.SolidMetal);
        // 右中侧翼立柱顶端动态推箱 [C] (第 05 行 Col 23，X=1350, Y=270)
        map.AddDynamicBox(new Vector2(1350f, 270f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 6. 中央悬空三段垂直立柱 (第 16~17 列，X: 900..1020)
        // -------------------------------------------------------------
        // 天顶立柱 (第 02~03 行，Y: 60..180)
        map.AddBox(new Rectangle(900, 60, 120, 120), TerrainStyle.SolidMetal);
        // 空中浮台 (第 07~08 行，Y: 360..480)
        map.AddBox(new Rectangle(900, 360, 120, 120), TerrainStyle.SolidMetal);
        // 峡谷深渊浮柱 (第 12~14 行，Y: 660..840)
        map.AddBox(new Rectangle(900, 660, 120, 180), TerrainStyle.SolidMetal);

        // -------------------------------------------------------------
        // 7. 纯悬挂锁链 [|] 与加长 3 连吊箱 [C][C][C] (第 13 与 20 列)
        // -------------------------------------------------------------
        // 左链条吊装大型 3 连横向重箱 (第 11 行，Col 12~14，Y=630)
        map.AddHangingBox(
            worldAnchor: new Vector2(750f, 60f),
            boxCenter: new Vector2(750f, 630f),
            boxSize: new Vector2(170f, 54f),
            mass: 4.8f,
            ropeLength: 570f,
            style: TerrainStyle.SolidMetal
        );

        // 右链条吊装大型 3 连横向重箱 (第 11 行，Col 19~21，Y=630)
        map.AddHangingBox(
            worldAnchor: new Vector2(1170f, 60f),
            boxCenter: new Vector2(1170f, 630f),
            boxSize: new Vector2(170f, 54f),
            mass: 4.8f,
            ropeLength: 570f,
            style: TerrainStyle.SolidMetal
        );

        // -------------------------------------------------------------
        // 8. 中央立柱错位堆叠物理箱 [C] (对角平衡布局，极易受爆炸冲击崩塌滚下坡道)
        // -------------------------------------------------------------
        // 左上堆叠箱 (立于第 07 行立柱顶端，Col 16 X=930，第 06 行 Y=330 与 第 05 行 Y=270)
        map.AddDynamicBox(new Vector2(930f, 330f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(930f, 270f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);

        // 右下堆叠箱 (立于第 12 行立柱顶端，Col 17 X=990，第 11 行 Y=630 与 第 10 行 Y=570)
        map.AddDynamicBox(new Vector2(990f, 630f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
        map.AddDynamicBox(new Vector2(990f, 570f), new Vector2(50f, 50f), mass: 3.2f, style: TerrainStyle.SolidMetal);
    }
}
