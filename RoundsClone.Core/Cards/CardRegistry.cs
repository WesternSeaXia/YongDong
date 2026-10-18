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
    /// 各稀有度在单卡槽位的基础抽取概率权重 (百分比，总和 100%)。
    /// 普通 (Common): 55% | 稀有 (Rare): 30% | 史诗 (Epic): 12% | 传说 (Legendary): 3%
    /// </summary>
    public static readonly IReadOnlyDictionary<CardTier, int> TierDrawWeights = new Dictionary<CardTier, int>
    {
        { CardTier.Common, 55 },
        { CardTier.Rare, 30 },
        { CardTier.Epic, 12 },
        { CardTier.Legendary, 3 }
    };

    /// <summary>
    /// 从卡池中按照稀有度概率加权抽取指定数量的不重复候选卡牌（用于技能三选一界面）。
    /// 单槽位概率：普通 55% / 稀有 30% / 史诗 12% / 传说 3%。
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

        List<ICardPlugin> result = new();
        int targetCount = Math.Min(count, pool.Count);

        while (result.Count < targetCount && pool.Count > 0)
        {
            // 依据权重轮盘随机抽取目标稀有度
            CardTier targetTier = RollRandomTier(pool);

            // 在候选池中选取该稀有度的卡牌
            var candidateCards = pool.Where(c => c.Tier == targetTier).ToList();
            if (candidateCards.Count == 0)
            {
                // 该稀有度已被抽尽，退化为池中剩余卡牌随机
                candidateCards = pool;
            }

            int pickIdx = Random.Shared.Next(candidateCards.Count);
            var chosen = candidateCards[pickIdx];

            result.Add(chosen);
            pool.RemoveAll(c => string.Equals(c.Id, chosen.Id, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static CardTier RollRandomTier(List<ICardPlugin> currentPool)
    {
        var availableTiers = new HashSet<CardTier>(currentPool.Select(c => c.Tier));
        int totalWeight = 0;
        foreach (var tier in availableTiers)
        {
            if (TierDrawWeights.TryGetValue(tier, out int w)) totalWeight += w;
        }

        if (totalWeight <= 0) return CardTier.Common;

        int roll = Random.Shared.Next(totalWeight);
        int accum = 0;
        foreach (var tier in availableTiers)
        {
            if (TierDrawWeights.TryGetValue(tier, out int w))
            {
                accum += w;
                if (roll < accum) return tier;
            }
        }

        return availableTiers.First();
    }
}
