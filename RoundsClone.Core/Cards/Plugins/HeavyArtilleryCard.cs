using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 重装火炮技能卡插件。
/// 职责边界：大幅提升单发子弹伤害、弹丸体积与击退冲量，同时增加射击后坐力与换弹周期。
/// </summary>
public class HeavyArtilleryCard : ICardPlugin
{
    public string Id => "card_heavy_artillery";
    public string Name => "重装火炮";
    public string Description => "伤害 +85%\n弹丸体积 +100%\n弹匣容量 -1\n射速 -40%\n后坐力 +85%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Heavy;

    public void OnApply(Actor actor)
    {
        actor.BulletDamage *= 1.85f;
        actor.BulletSizeMultiplier += 1.0f;
        actor.MaxAmmo = Math.Max(1, actor.MaxAmmo - 1);
        actor.CurrentAmmo = Math.Min(actor.CurrentAmmo, actor.MaxAmmo);
        actor.FireRateCooldown *= 1.40f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.KnockbackImpulse *= 2.2f;
    }
}
