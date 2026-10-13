using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件抽象基类。
/// 职责边界：封装竞技场通用的四周封闭边界墙生成与基础元数据注入，使具体的地图插件文件仅需关注内部平台与机关拓扑布局。
/// </summary>
public abstract class MapPluginBase : IMapPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string EnvironmentalModifier => "标准力学环境";

    protected virtual Vector2 DefaultPlayerSpawn => new(260f, 960f);
    protected virtual Vector2 DefaultAiSpawn => new(1660f, 960f);

    /// <summary>
    /// 创建并初始化地图数据实例。
    /// </summary>
    public MapData CreateMap()
    {
        var map = new MapData
        {
            Id = Id,
            Name = Name,
            Description = Description,
            EnvironmentalModifier = EnvironmentalModifier,
            PlayerSpawn = DefaultPlayerSpawn,
            AiSpawn = DefaultAiSpawn
        };

        AddArenaBorders(map);
        BuildArenaLayout(map);
        return map;
    }

    /// <summary>
    /// 由具体地图插件子类实现，向地图中添加平台墙体 (<see cref="MapData.SolidWalls"/>) 与高弹反弹柱 (<see cref="MapData.BouncyPillars"/>)。
    /// </summary>
    protected abstract void BuildArenaLayout(MapData map);

    /// <summary>
    /// 为 1920x1080 逻辑竞技场注入标准的四周封闭边界墙。
    /// </summary>
    protected static void AddArenaBorders(MapData map, int thickness = 50)
    {
        int w = VirtualViewport.VirtualWidth;
        int h = VirtualViewport.VirtualHeight;

        map.SolidWalls.Add(new Rectangle(0, 0, w, thickness));             // 顶部天花板
        map.SolidWalls.Add(new Rectangle(0, h - thickness, w, thickness)); // 底部地面
        map.SolidWalls.Add(new Rectangle(0, 0, thickness, h));             // 左侧外墙
        map.SolidWalls.Add(new Rectangle(w - thickness, 0, thickness, h)); // 右侧外墙
    }
}
