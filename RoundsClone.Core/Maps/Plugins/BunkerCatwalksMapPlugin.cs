using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：地下战备堡垒 (Bunker Catwalks)。
/// 包含相互连通融合的无缝混凝土走廊、可轰破开启捷径的防爆钢砖大门以及中庭双索吊装重型集装箱。
/// </summary>
public sealed class BunkerCatwalksMapPlugin : MapPluginBase
{
    public override string Id => "map_bunker";
    public override string Name => "地下战备堡垒";
    public override string Description => "无缝互通重工业廊道、可击碎破拆的防爆大门与高空悬吊重型集装箱。";
    public override string EnvironmentalModifier => "封闭空间回音";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层掩体顶板与中央主立柱 (相互接触连接，由 MapRenderer 自动消隐缝隙融合成一体)
        map.AddBox(new Rectangle(120, 850, 420, 36), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1380, 850, 420, 36), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(930, 740, 60, 290), TerrainStyle.SolidMetal); // 中央主防爆柱

        // 2. 可破坏防爆砖石大门 (阻塞底层与中层的核心射击走廊，破坏后直接打通跨场射界)
        map.AddBox(new Rectangle(540, 850, 40, 180), TerrainStyle.DestructibleBrick, destructible: true, hp: 120f);
        map.AddBox(new Rectangle(1340, 850, 40, 180), TerrainStyle.DestructibleBrick, destructible: true, hp: 120f);

        // 3. 中层走廊平台 (Y=690, 距底层台阶差 160px)
        map.AddBox(new Rectangle(220, 690, 340, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1360, 690, 340, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(640, 690, 240, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1040, 690, 240, 32), TerrainStyle.SolidMetal);

        // 4. 双侧蹬墙攀爬竖井天井 (井宽 180px，支持流畅连续蹬墙跳跃直通高层)
        map.AddBox(new Rectangle(600, 470, 36, 220), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1284, 470, 36, 220), TerrainStyle.SolidMetal);

        // 5. 中层核心可破坏挡板
        map.AddBox(new Rectangle(920, 690, 80, 28), TerrainStyle.DestructibleBrick, destructible: true, hp: 90f);

        // 6. 高层指挥天桥与狙击据点 (Y=470, 距中层 220px，通过攀爬井或高台登顶)
        map.AddBox(new Rectangle(340, 470, 260, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1320, 470, 260, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(720, 470, 480, 36), TerrainStyle.SolidMetal);  // 中央主天桥
        map.AddBox(new Rectangle(840, 310, 240, 32), TerrainStyle.SolidMetal);  // 顶层观察塔

        // 7. 中庭悬吊重型物理集装箱 (挂载于双链吊钩下，受到弹火倾泻时大幅度摆动并可阻挡流弹)
        map.AddHangingBox(
            worldAnchor: new Vector2(960f, 50f),
            boxCenter: new Vector2(960f, 200f),
            boxSize: new Vector2(210f, 72f),
            mass: 4.5f,
            ropeLength: 125f,
            style: TerrainStyle.SolidMetal
        );

        // 8. 危险尖刺地带与应急回弹装置
        map.AddBox(new Rectangle(900, 560, 120, 28), TerrainStyle.BouncyRubber); // 天桥下方垂直发射垫
        map.AddBox(new Rectangle(50, 710, 35, 160), TerrainStyle.BouncyRubber);  // 侧翼回弹
        map.AddBox(new Rectangle(1835, 710, 35, 160), TerrainStyle.BouncyRubber);
    }
}
