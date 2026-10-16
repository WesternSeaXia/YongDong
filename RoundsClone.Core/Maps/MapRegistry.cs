using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图插件注册中心与竞技场抽取管理器。
/// 职责边界：通过反射自动发现并注册所有实现了 <see cref="IMapPlugin"/> 的独立地图插件，
/// 同时支持运行时动态注册新地图插件，并提供关卡三选一随机抽取服务。
/// </summary>
public static class MapRegistry
{
    private static readonly Dictionary<string, IMapPlugin> _mapPlugins = new(StringComparer.OrdinalIgnoreCase);

    static MapRegistry()
    {
        // 自动扫描当前核心程序集中所有实现 IMapPlugin 的地图插件
        DiscoverPluginsFromAssembly(typeof(MapRegistry).Assembly);
    }

    /// <summary>
    /// 扫描指定程序集中所有非抽象且具有无参构造函数的 <see cref="IMapPlugin"/> 实现类并注册。
    /// </summary>
    public static void DiscoverPluginsFromAssembly(Assembly assembly)
    {
        var pluginInterface = typeof(IMapPlugin);
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsAbstract && !type.IsInterface && pluginInterface.IsAssignableFrom(type))
            {
                if (type.GetConstructor(Type.EmptyTypes) != null)
                {
                    RegisterPlugin((IMapPlugin)Activator.CreateInstance(type)!);
                }
            }
        }
    }

    /// <summary>
    /// 手动注册一个地图插件实例。
    /// </summary>
    public static void RegisterPlugin(IMapPlugin plugin)
    {
        _mapPlugins[plugin.Id] = plugin;
    }

    /// <summary>
    /// 手动注册一个地图插件类型。
    /// </summary>
    public static void RegisterPlugin<T>() where T : IMapPlugin, new()
    {
        RegisterPlugin(new T());
    }

    /// <summary>
    /// 生成所有已注册地图插件的 <see cref="MapData"/> 实例列表。
    /// </summary>
    public static List<MapData> CreateAllMaps()
    {
        return _mapPlugins.Values.Select(p => p.CreateMap()).ToList();
    }

    /// <summary>
    /// 根据地图 ID 获取对应的地图实例；若未找到则返回默认第一张地图。
    /// </summary>
    public static MapData CreateById(string id)
    {
        if (_mapPlugins.TryGetValue(id, out var plugin))
        {
            return plugin.CreateMap();
        }
        return _mapPlugins.Values.First().CreateMap();
    }

    /// <summary>
    /// 创建工业机械悬垂竞技场地图实例。
    /// </summary>
    public static MapData CreateIndustrialArena() => CreateById("map_industrial");

    /// <summary>
    /// 创建重装悬垂工厂（第二张地图）实例。
    /// </summary>
    public static MapData CreateVerticalFactory() => CreateById("map_vertical_factory");

    /// <summary>
    /// 创建重力冰渊大峡谷（第三张地图）实例。
    /// </summary>
    public static MapData CreateGravityChasm() => CreateById("map_gravity_chasm");

    /// <summary>
    /// 创建弹跳要塞（第四张地图）实例。
    /// </summary>
    public static MapData CreateBounceFortress() => CreateById("map_bounce_fortress");

    /// <summary>
    /// 兼容接口：创建默认竞技场地图实例。
    /// </summary>
    public static MapData CreateClassicArena() => CreateIndustrialArena();

    /// <summary>
    /// 兼容接口：创建默认竞技场地图实例。
    /// </summary>
    public static MapData CreateBumperMaze() => CreateIndustrialArena();

    /// <summary>
    /// 从已注册的地图插件池中随机抽取指定数量的不重复关卡（用于关卡三选一界面）。
    /// </summary>
    public static List<MapData> DraftMaps(int count = 3)
    {
        var all = CreateAllMaps();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int swap = Random.Shared.Next(i + 1);
            (all[i], all[swap]) = (all[swap], all[i]);
        }
        return all.Take(Math.Min(count, all.Count)).ToList();
    }
}
