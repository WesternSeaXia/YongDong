using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：立体工业塔 (Vertical Factory)。
/// 职责边界：定义双垂直电梯竖井、高空行车横梁以及工业排气蹦床的立体纵深竞技场布局。
/// </summary>
public sealed class VerticalFactoryMapPlugin : MapPluginBase
{
    public override string Id => "map_factory";
    public override string Name => "立体工业塔";
    public override string Description => "拥有双垂直电梯竖井与高空行车横梁，展现极高纵深作战能力。";
    public override string EnvironmentalModifier => "垂直力场";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层物流踏板
        map.SolidWalls.Add(new Rectangle(120, 860, 240, 36));
        map.SolidWalls.Add(new Rectangle(1560, 860, 240, 36));
        map.SolidWalls.Add(new Rectangle(300, 750, 160, 36));
        map.SolidWalls.Add(new Rectangle(1460, 750, 160, 36));

        // 2. 左右双垂直电梯竖井 (竖井间距 160px，极佳的蹬墙跳连续上升通道)
        // 左竖井
        map.SolidWalls.Add(new Rectangle(460, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(640, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(500, 600, 140, 26));  // 井内中继歇脚点
        // 右竖井
        map.SolidWalls.Add(new Rectangle(1240, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(1420, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(1280, 600, 140, 26)); // 井内中继歇脚点

        // 3. 中央车间生产线与高空桁架
        map.SolidWalls.Add(new Rectangle(740, 780, 440, 32));  // 输送带天桥
        map.SolidWalls.Add(new Rectangle(680, 580, 560, 32));  // 主检修走道
        map.SolidWalls.Add(new Rectangle(760, 380, 400, 32));  // 控制中枢平层
        map.SolidWalls.Add(new Rectangle(640, 200, 640, 36));  // 顶层起重天车大梁
        map.SolidWalls.Add(new Rectangle(930, 236, 60, 80));   // 吊装行车方块掩体

        // 4. 外侧高位停机坪
        map.SolidWalls.Add(new Rectangle(140, 480, 220, 32));
        map.SolidWalls.Add(new Rectangle(1560, 480, 220, 32));
        map.SolidWalls.Add(new Rectangle(180, 280, 180, 32));
        map.SolidWalls.Add(new Rectangle(1560, 280, 180, 32));

        // 5. 工业排气蹦床与升降反弹垫
        map.BouncyPillars.Add(new Rectangle(860, 995, 200, 35));  // 地面高压蒸汽弹跳口
        map.BouncyPillars.Add(new Rectangle(885, 480, 150, 32));  // 中空反弹板
        map.BouncyPillars.Add(new Rectangle(500, 820, 140, 28));  // 左竖井底反弹加速块
        map.BouncyPillars.Add(new Rectangle(1280, 820, 140, 28)); // 右竖井底反弹加速块
    }
}
