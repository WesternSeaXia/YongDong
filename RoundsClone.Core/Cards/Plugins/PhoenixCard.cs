using System;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 凤凰涅槃技能卡插件 (Phoenix)。
/// 职责边界：受到致死伤害时满血满弹涅槃复活 1 次，并附带 1.2 秒金身无敌与滞空冲击；代价是平时最大生命值上限永久削减 35%。
/// </summary>
public class PhoenixCard : ICardPlugin
{
    public string Id => "card_phoenix";
    public string Name => "凤凰涅槃";
    public string Description => "致死满血满弹复活 1次\n复活附带 1.2s 无敌金身\n最大生命 -35%";
    public CardTier Tier => CardTier.Legendary;
    public IconType Icon => IconType.Heart;

    public void OnApply(Actor actor)
    {
        actor.HasPhoenixRevive = true;
        actor.MaxHealth *= 0.65f;
        actor.CurrentHealth = MathF.Min(actor.CurrentHealth, actor.MaxHealth);
        actor.RecalculateHealthScaling();
    }
}
