using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 终击之盾技能卡插件 (Shields Up)。
/// 职责边界：射出弹匣中最后一颗子弹时，自动瞬间激发球形力场护盾偏转敌方弹幕并提供短暂无敌庇护；代价是换弹时间增加 0.4 秒。
/// </summary>
public class ShieldsUpCard : ICardPlugin
{
    public string Id => "card_shields_up";
    public string Name => "终击之盾";
    public string Description => "打空弹匣自动触发力场\n反弹来袭弹幕/暂免伤害\n换弹时间 +0.4s";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Shield;

    public void OnApply(Actor actor)
    {
        actor.HasShieldsUp = true;
        actor.ReloadDuration += 0.40f;
    }
}
