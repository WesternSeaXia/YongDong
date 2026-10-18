using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 泰坦装甲技能卡插件。
/// 职责边界：大幅提升角色最大生命值上限，并联动角色的体型与刚体质量缩放体系。
/// </summary>
public class TitanArmorCard : ICardPlugin
{
    public string Id => "card_titan_armor";
    public string Name => "泰坦装甲";
    public string Description => "最大生命 +100\n移动推力 -20%";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Heart;

    public void OnApply(Actor actor)
    {
        actor.MaxHealth += 100f;
        actor.CurrentHealth += 100f;
        actor.RecalculateHealthScaling();
        actor.MoveForce *= 0.80f;
    }
}
