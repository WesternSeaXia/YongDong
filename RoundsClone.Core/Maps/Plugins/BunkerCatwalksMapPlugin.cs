using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：紧凑掩体死斗场 (Bunker Catwalks)。
/// 职责边界：定义厚重混凝土廊道、防爆立柱以及攀爬天井壁的紧凑近战竞技场布局。
/// </summary>
public sealed class BunkerCatwalksMapPlugin : MapPluginBase
{
    public override string Id => "map_bunker";
    public override string Name => "紧凑掩体死斗场";
    public override string Description => "厚重混凝土廊道与防爆立柱，极度考验近战缠斗与走位博弈。";
    public override string EnvironmentalModifier => "封闭空间回音";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层掩体顶板与中央主防爆墙
        map.SolidWalls.Add(new Rectangle(120, 830, 380, 36));
        map.SolidWalls.Add(new Rectangle(1420, 830, 380, 36));
        map.SolidWalls.Add(new Rectangle(930, 750, 60, 280));   // 中央主防爆柱
        map.SolidWalls.Add(new Rectangle(420, 730, 40, 100));   // 左低位射击护栏
        map.SolidWalls.Add(new Rectangle(1460, 730, 40, 100));  // 右低位射击护栏

        // 2. 中层工业走廊与攀爬井
        map.SolidWalls.Add(new Rectangle(220, 630, 320, 32));
        map.SolidWalls.Add(new Rectangle(1380, 630, 320, 32));
        map.SolidWalls.Add(new Rectangle(620, 640, 240, 32));
        map.SolidWalls.Add(new Rectangle(1060, 640, 240, 32));
        map.SolidWalls.Add(new Rectangle(620, 440, 36, 200));   // 攀爬天井壁 A
        map.SolidWalls.Add(new Rectangle(1264, 440, 36, 200));  // 攀爬天井壁 B

        // 3. 高层指挥桥与狙击据点
        map.SolidWalls.Add(new Rectangle(360, 440, 240, 32));
        map.SolidWalls.Add(new Rectangle(1320, 440, 240, 32));
        map.SolidWalls.Add(new Rectangle(740, 440, 440, 36));   // 中央主天桥
        map.SolidWalls.Add(new Rectangle(840, 280, 240, 32));   // 顶层观察塔
        map.SolidWalls.Add(new Rectangle(840, 140, 40, 140));   // 塔顶排气立柱
        map.SolidWalls.Add(new Rectangle(1040, 140, 40, 140));

        // 4. 掩体应急弹射与折射装置
        map.BouncyPillars.Add(new Rectangle(920, 570, 80, 32));   // 中井垂直强力弹射板
        map.BouncyPillars.Add(new Rectangle(480, 160, 160, 32));  // 顶部折射板 L
        map.BouncyPillars.Add(new Rectangle(1280, 160, 160, 32)); // 顶部折射板 R
        map.BouncyPillars.Add(new Rectangle(50, 700, 35, 150));   // 侧门推斥器
        map.BouncyPillars.Add(new Rectangle(1835, 700, 35, 150));
    }
}
