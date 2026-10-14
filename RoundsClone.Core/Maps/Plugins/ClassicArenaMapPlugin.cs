using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件：经典对称竞技场 (Classic Arena)。
/// 包含斜坡多边形引桥、中央钢链吊装物理摆动箱、可破坏战术砖墙以及严谨校验的跳跃动线。
/// </summary>
public sealed class ClassicArenaMapPlugin : MapPluginBase
{
    public override string Id => "map_classic";
    public override string Name => "经典对称竞技场";
    public override string Description => "双侧多边形斜坡冲刺道、中央高空吊链物理摆动箱与可击碎掩体。";
    public override string EnvironmentalModifier => "标准力学环境";

    protected override Vector2 DefaultPlayerSpawn => new(240f, 960f);
    protected override Vector2 DefaultAiSpawn => new(1680f, 960f);

    /// <inheritdoc />
    protected override void BuildArenaLayout(MapData map)
    {
        // 1. 双侧地面凸多边形平滑冲刺斜坡 (让角色从地面平滑跑上第一层侧翼平台，无需起跳)
        // 左斜坡: (100, 1030) -> (360, 1030) -> (360, 870)
        Vector2[] leftRampVerts = new[]
        {
            new Vector2(100f, 1030f),
            new Vector2(360f, 1030f),
            new Vector2(360f, 870f)
        };
        map.AddPolygon(leftRampVerts, TerrainStyle.SolidMetal);

        // 右斜坡: (1820, 1030) -> (1560, 1030) -> (1560, 870)
        Vector2[] rightRampVerts = new[]
        {
            new Vector2(1820f, 1030f),
            new Vector2(1560f, 870f),
            new Vector2(1560f, 1030f)
        };
        map.AddPolygon(rightRampVerts, TerrainStyle.SolidMetal);

        // 2. 底层与中层侧翼平台 (起跳台阶差控制在 160px，极大保证跳跃舒适度)
        map.AddBox(new Rectangle(360, 870, 220, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1340, 870, 220, 32), TerrainStyle.SolidMetal);

        map.AddBox(new Rectangle(180, 710, 260, 32), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1480, 710, 260, 32), TerrainStyle.SolidMetal);

        // 3. 可破坏强化砖墙掩体 (承受子弹射击后破碎，产生漫天碎屑并开通贯通直射通路)
        map.AddBox(new Rectangle(400, 590, 40, 120), TerrainStyle.DestructibleBrick, destructible: true, hp: 110f);
        map.AddBox(new Rectangle(1480, 590, 40, 120), TerrainStyle.DestructibleBrick, destructible: true, hp: 110f);

        // 4. 高层悬空跳台 (Y=540, 距中层 170px)
        map.AddBox(new Rectangle(440, 540, 200, 30), TerrainStyle.SolidMetal);
        map.AddBox(new Rectangle(1280, 540, 200, 30), TerrainStyle.SolidMetal);

        // 5. 中央主战斗高台与天井立柱 (竖井宽度 180px，支持流畅蹬墙跳上顶层)
        map.AddBox(new Rectangle(720, 720, 480, 32), TerrainStyle.SolidMetal); // 中央中继平台
        map.AddBox(new Rectangle(700, 490, 36, 230), TerrainStyle.SolidMetal); // 堡垒左柱
        map.AddBox(new Rectangle(1184, 490, 36, 230), TerrainStyle.SolidMetal);// 堡垒右柱
        map.AddBox(new Rectangle(660, 490, 600, 32), TerrainStyle.SolidMetal); // 中央主平台

        // 6. 中央悬挂动力学物理摆动箱 (挂靠在天花板钢索上，受到子弹与角色碰撞会真实摆动与倾斜)
        map.AddHangingBox(
            worldAnchor: new Vector2(960f, 50f),
            boxCenter: new Vector2(960f, 290f),
            boxSize: new Vector2(170f, 65f),
            mass: 3.5f,
            ropeLength: 205f,
            style: TerrainStyle.SolidMetal
        );

        // 7. 顶部狙击天桥
        map.AddBox(new Rectangle(780, 320, 360, 30), TerrainStyle.SolidMetal);

        // 8. 弹性构件与反弹跳板
        map.AddBox(new Rectangle(890, 1000, 140, 30), TerrainStyle.BouncyRubber); // 中央地面发射垫
        map.AddCircle(new Vector2(960f, 150f), 32f, TerrainStyle.BouncyRubber);     // 顶部天顶弹力球
        map.AddBox(new Rectangle(50, 460, 35, 160), TerrainStyle.BouncyRubber);    // 左外墙回弹板
        map.AddBox(new Rectangle(1835, 460, 35, 160), TerrainStyle.BouncyRubber);  // 右外墙回弹板
    }
}
