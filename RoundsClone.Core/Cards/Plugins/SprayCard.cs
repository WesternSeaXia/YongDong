using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 泼水加特林技能卡插件 (Spray)。
/// 职责边界：极端强化射速 (+800%) 与弹匣容量 (+10)，瞬间向目标喷涌狂暴弹幕风暴；代价是大幅削减单发伤害 (-75%)、增加微量弹道散布与延长换弹周期。
/// </summary>
public class SprayCard : ICardPlugin
{
    public string Id => "card_spray";
    public string Name => "泼水加特林";
    public string Description => "攻速 +800%\n弹匣容量 +10\n单发伤害 -75%\n换弹时间 +0.35s";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.FireRateCooldown *= 0.11f;
        actor.MaxAmmo += 10;
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.BulletDamage *= 0.25f;
        actor.ReloadDuration += 0.35f;
        actor.BulletSpread += 0.12f;
    }
}
