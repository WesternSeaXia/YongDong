using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 绝对反弹技能卡插件。
/// 职责边界：大幅缩减力场格挡等副武器冷却时间，并在成功格挡偏转敌方子弹时立即刷新冷却、提升反弹子弹伤害。
/// </summary>
public class AbsoluteParryCard : ICardPlugin
{
    public string Id => "card_absolute_parry";
    public string Name => "绝对反弹";
    public string Description => "副武器冷却 -35%\n反弹伤害 +50%\n最大生命 -15";
    public CardTier Tier => CardTier.Legendary;
    public IconType Icon => IconType.Shield;

    public void OnApply(Actor actor)
    {
        if (actor.SubWeapon is BlockSubWeapon block)
        {
            block.Cooldown *= 0.65f;
        }
        else if (actor.SubWeapon != null)
        {
            actor.SubWeapon.Cooldown *= 0.8f;
        }

        actor.MaxHealth = System.MathF.Max(30f, actor.MaxHealth - 15f);
        actor.CurrentHealth = System.MathF.Min(actor.CurrentHealth, actor.MaxHealth);
        actor.RecalculateHealthScaling();
    }
}
