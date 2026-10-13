using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：深渊浮空岛 (Floating Islands)。
/// 职责边界：定义多层天空浮岛群、天空圣殿以及底层深渊救生蹦床的地图布局。
/// </summary>
public sealed class FloatingIslandsMapPlugin : MapPluginBase
{
    public override string Id => "map_islands";
    public override string Name => "深渊浮空岛";
    public override string Description => "多层天空浮岛群与深渊虚空，底层救生蹦床带来极速回弹。";
    public override string EnvironmentalModifier => "高空虚空强风";

    protected override Vector2 DefaultPlayerSpawn => new(280f, 760f);
    protected override Vector2 DefaultAiSpawn => new(1640f, 760f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层多块浮空岛屿
        map.SolidWalls.Add(new Rectangle(140, 810, 280, 36));
        map.SolidWalls.Add(new Rectangle(1500, 810, 280, 36));
        map.SolidWalls.Add(new Rectangle(490, 840, 240, 36));
        map.SolidWalls.Add(new Rectangle(1190, 840, 240, 36));
        map.SolidWalls.Add(new Rectangle(820, 870, 280, 36));

        // 2. 中层连环浮空岛
        map.SolidWalls.Add(new Rectangle(260, 650, 260, 36));
        map.SolidWalls.Add(new Rectangle(1400, 650, 260, 36));
        map.SolidWalls.Add(new Rectangle(620, 620, 280, 36));
        map.SolidWalls.Add(new Rectangle(1020, 620, 280, 36));

        // 3. 高层圣殿与空岛石柱
        map.SolidWalls.Add(new Rectangle(400, 460, 220, 36));
        map.SolidWalls.Add(new Rectangle(1300, 460, 220, 36));
        map.SolidWalls.Add(new Rectangle(720, 430, 480, 36));   // 天空圣殿主平台
        map.SolidWalls.Add(new Rectangle(810, 260, 300, 36));   // 圣殿顶层阁楼
        map.SolidWalls.Add(new Rectangle(740, 296, 36, 134));   // 圣殿左立柱
        map.SolidWalls.Add(new Rectangle(1144, 296, 36, 134));  // 圣殿右立柱
        map.SolidWalls.Add(new Rectangle(140, 420, 160, 32));   // 左边缘高空哨所
        map.SolidWalls.Add(new Rectangle(1620, 420, 160, 32));  // 右边缘高空哨所

        // 4. 深渊防跌落反弹蹦床与空中折射点
        map.BouncyPillars.Add(new Rectangle(320, 995, 240, 35)); // 深渊左救生垫
        map.BouncyPillars.Add(new Rectangle(1360, 995, 240, 35));// 深渊右救生垫
        map.BouncyPillars.Add(new Rectangle(840, 995, 240, 35)); // 深渊中救生垫
        map.BouncyPillars.Add(new Rectangle(50, 280, 35, 160));  // 高空侧翼回弹
        map.BouncyPillars.Add(new Rectangle(1835, 280, 35, 160));
    }
}
