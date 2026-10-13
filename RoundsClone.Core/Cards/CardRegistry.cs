using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 技能卡牌插件注册中心与卡池管理器。
/// 职责边界：通过反射自动扫描并注册所有实现了 <see cref="ICardPlugin"/> 的技能插件类，
/// 同时支持运行时动态注册外部卡牌插件，并提供选卡阶段的不重复随机抽取服务。
/// </summary>
public static class CardRegistry
{
    private static readonly Dictionary<string, Func<ICardPlugin>> _cardFactories = new(StringComparer.OrdinalIgnoreCase);

    static CardRegistry()
    {
        // 自动扫描当前核心程序集中所有实现 ICardPlugin 的技能插件
        DiscoverPluginsFromAssembly(typeof(CardRegistry).Assembly);
    }

    /// <summary>
    /// 扫描指定程序集中所有非抽象且具有无参构造函数的 <see cref="ICardPlugin"/> 实现类并注册到卡池。
    /// </summary>
    public static void DiscoverPluginsFromAssembly(Assembly assembly)
    {
        var pluginInterface = typeof(ICardPlugin);
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsAbstract && !type.IsInterface && pluginInterface.IsAssignableFrom(type))
            {
                if (type.GetConstructor(Type.EmptyTypes) != null)
                {
                    RegisterPlugin(() => (ICardPlugin)Activator.CreateInstance(type)!);
                }
            }
        }
    }

    /// <summary>
    /// 手动注册一个技能卡牌插件工厂。
    /// </summary>
    public static void RegisterPlugin(Func<ICardPlugin> factory)
    {
        var instance = factory();
        _cardFactories[instance.Id] = factory;
    }

    /// <summary>
    /// 手动注册一个技能卡牌插件类型。
    /// </summary>
    public static void RegisterPlugin<T>() where T : ICardPlugin, new()
    {
        RegisterPlugin(() => new T());
    }

    /// <summary>
    /// 获取当前卡池中所有已注册卡牌插件的全新实例列表。
    /// </summary>
    public static List<ICardPlugin> GetAllCards()
    {
        return _cardFactories.Values.Select(f => f()).ToList();
    }

    /// <summary>
    /// 从卡池中抽取指定数量的不重复候选卡牌（用于技能三选一界面）。
    /// </summary>
    /// <param name="count">抽卡数量（默认 3 张）</param>
    /// <param name="excludeIds">需排除的卡牌 ID 集合</param>
    public static List<ICardPlugin> DraftCards(int count = 3, IEnumerable<string>? excludeIds = null)
    {
        var pool = GetAllCards();
        if (excludeIds != null)
        {
            var excludeSet = new HashSet<string>(excludeIds, StringComparer.OrdinalIgnoreCase);
            pool = pool.Where(c => !excludeSet.Contains(c.Id)).ToList();
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int swapIdx = Random.Shared.Next(i + 1);
            (pool[i], pool[swapIdx]) = (pool[swapIdx], pool[i]);
        }

        return pool.Take(Math.Min(count, pool.Count)).ToList();
    }
}
