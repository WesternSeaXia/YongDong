using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 坚韧体魄技能卡插件 (Vitality)。
/// 职责边界：纯粹强化角色的生存能力，直接提升 50 点最大生命值，无任何负面惩罚。
/// </summary>
public class VitalityCard : ICardPlugin
{
    public string Id => "card_vitality";
    public string Name => "坚韧体魄";
    public string Description => "最大生命 +50";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Heart;

    public void OnApply(Actor actor)
    {
        actor.MaxHealth += 50f;
        actor.CurrentHealth += 50f;
        actor.RecalculateHealthScaling();
    }
}
