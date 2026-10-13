using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：高能反弹迷宫 (Bumper Maze)。
/// 职责边界：定义错落交织的锯齿状平台以及高弹力缓冲墙与反弹蹦床的地图布局。
/// </summary>
public sealed class BumperMazeMapPlugin : MapPluginBase
{
    public override string Id => "map_bumper";
    public override string Name => "高能反弹迷宫";
    public override string Description => "错落交织的锯齿状平台，布满高弹力缓冲墙与反弹蹦床。";
    public override string EnvironmentalModifier => "全场高弹性反弹";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层与中层错位台阶 (Z 字形攀爬动线)
        map.SolidWalls.Add(new Rectangle(140, 870, 240, 32));
        map.SolidWalls.Add(new Rectangle(1540, 870, 240, 32));
        map.SolidWalls.Add(new Rectangle(380, 750, 240, 32));
        map.SolidWalls.Add(new Rectangle(1300, 750, 240, 32));
        map.SolidWalls.Add(new Rectangle(160, 610, 260, 32));
        map.SolidWalls.Add(new Rectangle(1500, 610, 260, 32));
        map.SolidWalls.Add(new Rectangle(420, 480, 240, 32));
        map.SolidWalls.Add(new Rectangle(1260, 480, 240, 32));
        map.SolidWalls.Add(new Rectangle(200, 340, 220, 32));
        map.SolidWalls.Add(new Rectangle(1500, 340, 220, 32));

        // 2. 中央战区多层桥梁与掩体柱
        map.SolidWalls.Add(new Rectangle(760, 730, 400, 32));
        map.SolidWalls.Add(new Rectangle(700, 520, 520, 32));
        map.SolidWalls.Add(new Rectangle(820, 320, 280, 32));
        map.SolidWalls.Add(new Rectangle(935, 352, 50, 168));   // 中央垂立阻隔柱

        // 3. 高能弹性反弹构件
        map.BouncyPillars.Add(new Rectangle(660, 995, 200, 35)); // 地面左蹦床
        map.BouncyPillars.Add(new Rectangle(1060, 995, 200, 35));// 地面右蹦床
        map.BouncyPillars.Add(new Rectangle(560, 360, 40, 240)); // 左垂直反弹柱
        map.BouncyPillars.Add(new Rectangle(1320, 360, 40, 240));// 右垂直反弹柱
        map.BouncyPillars.Add(new Rectangle(860, 180, 200, 36)); // 顶层悬空反弹块
        map.BouncyPillars.Add(new Rectangle(50, 540, 35, 160));  // 侧翼反弹贴片
        map.BouncyPillars.Add(new Rectangle(1835, 540, 35, 160));
    }
}
