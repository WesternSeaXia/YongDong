namespace RoundsClone.Core.Maps;

/// <summary>
/// 插件化战斗地图核心接口。
/// 职责边界：定义单张竞技场地图的元数据标识与实例构建契约，每张地图作为一个独立插件类实现本接口。
/// </summary>
public interface IMapPlugin
{
    /// <summary>
    /// 地图唯一英文标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 地图显示中文名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 地图战术结构简述。
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 环境物理特性说明。
    /// </summary>
    string EnvironmentalModifier { get; }

    /// <summary>
    /// 构建并返回包含全部墙体、高弹反弹柱与出生点坐标的 <see cref="MapData"/> 实例。
    /// </summary>
    MapData CreateMap();
}
