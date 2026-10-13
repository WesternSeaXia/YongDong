using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：经典对称竞技场 (Classic Arena)。
/// 职责边界：定义多层对称台阶、中央战斗堡垒以及双侧攀爬天井的经典竞技场地图布局。
/// </summary>
public sealed class ClassicArenaMapPlugin : MapPluginBase
{
    public override string Id => "map_classic";
    public override string Name => "经典对称竞技场";
    public override string Description => "多层对称台阶与中央战斗堡垒，具有立体掩体与攀爬天井。";
    public override string EnvironmentalModifier => "标准力学环境";

    protected override Vector2 DefaultPlayerSpawn => new(280f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1640f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 底层侧翼起跳台阶
        map.SolidWalls.Add(new Rectangle(120, 880, 240, 32));
        map.SolidWalls.Add(new Rectangle(1560, 880, 240, 32));
        map.SolidWalls.Add(new Rectangle(340, 770, 200, 32));
        map.SolidWalls.Add(new Rectangle(1380, 770, 200, 32));

        // 2. 中层两翼平台与防弹立柱掩体
        map.SolidWalls.Add(new Rectangle(160, 600, 280, 32));
        map.SolidWalls.Add(new Rectangle(1480, 600, 280, 32));
        map.SolidWalls.Add(new Rectangle(400, 480, 40, 120));   // 左翼掩体立柱
        map.SolidWalls.Add(new Rectangle(1480, 480, 40, 120));  // 右翼掩体立柱

        // 3. 高层悬浮翼台
        map.SolidWalls.Add(new Rectangle(440, 390, 220, 32));
        map.SolidWalls.Add(new Rectangle(1260, 390, 220, 32));

        // 4. 中央多层战斗堡垒与双侧攀爬竖井 (竖井宽 180px，支持连续蹬墙跳)
        map.SolidWalls.Add(new Rectangle(740, 800, 440, 32));   // 中央底层桥梁
        map.SolidWalls.Add(new Rectangle(710, 580, 40, 250));   // 堡垒左立柱
        map.SolidWalls.Add(new Rectangle(1170, 580, 40, 250));  // 堡垒右立柱
        map.SolidWalls.Add(new Rectangle(660, 580, 600, 32));   // 中央主战平台
        map.SolidWalls.Add(new Rectangle(920, 430, 80, 150));   // 中央分火掩体方块
        map.SolidWalls.Add(new Rectangle(780, 380, 360, 32));   // 顶层狙击连廊

        // 5. 反弹垫与动能弹射器
        map.BouncyPillars.Add(new Rectangle(890, 1000, 140, 30)); // 地面中央发射蹦床
        map.BouncyPillars.Add(new Rectangle(885, 230, 150, 36));  // 顶部天花板折射板
        map.BouncyPillars.Add(new Rectangle(50, 420, 35, 180));   // 左外侧墙面反弹板
        map.BouncyPillars.Add(new Rectangle(1835, 420, 35, 180)); // 右外侧墙面反弹板
    }
}
