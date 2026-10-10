using System;
using System.Collections.Generic;
using System.Linq;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 卡牌注册表与卡池管理器。
/// 提供卡牌实例化原型库、卡池管理以及三选一随机卡池抽取功能。
/// </summary>
public static class CardRegistry
{
    private static readonly List<Func<ICardPlugin>> _cardFactories = new()
    {
        () => new BounceMasterCard(),
        () => new ClusterBombCard(),
        () => new ToxicCatalystCard(),
        () => new HeavyArtilleryCard(),
        () => new RapidFireCard(),
        () => new VampiricLeechCard(),
        () => new ImplosionNovaCard(),
        () => new ThunderDashCard(),
        () => new AbsoluteParryCard(),
        () => new TitanArmorCard(),
        () => new AcceleratingRocketCard(),
        () => new FrostFreezeCard()
    };

    /// <summary>
    /// 获取当前卡池中所有可用卡牌的原型实例。
    /// </summary>
    public static List<ICardPlugin> GetAllCards()
    {
        return _cardFactories.Select(f => f()).ToList();
    }

    /// <summary>
    /// 从卡池中抽取指定数量的不重复候选卡牌（用于技能三选一界面）。
    /// </summary>
    /// <param name="count">抽卡数量 (通常为 3)</param>
    /// <param name="excludeIds">需排除的卡牌 ID 列表 (如已装备满的排他性卡)</param>
    /// <returns>随机抽取的卡牌列表</returns>
    public static List<ICardPlugin> DraftCards(int count = 3, IEnumerable<string>? excludeIds = null)
    {
        var pool = GetAllCards();
        if (excludeIds != null)
        {
            var excludeSet = new HashSet<string>(excludeIds);
            pool = pool.Where(c => !excludeSet.Contains(c.Id)).ToList();
        }

        // 洗牌算法
        var rand = new Random();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int swapIdx = rand.Next(i + 1);
            (pool[i], pool[swapIdx]) = (pool[swapIdx], pool[i]);
        }

        return pool.Take(Math.Min(count, pool.Count)).ToList();
    }
}
