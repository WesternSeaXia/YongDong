using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 火箭助推技能卡插件。
/// 职责边界：提升角色发射的子弹初速度，并消除子弹受重力影响产生的抛物线下坠，实现直线高速狙击。
/// </summary>
public class AcceleratingRocketCard : ICardPlugin
{
    public string Id => "card_rocket_thruster";
    public string Name => "火箭助推";
    public string Description => "弹速 +45%\n子弹无重力下坠\n换弹时间 +0.25s";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.BulletSpeed *= 1.45f;
        actor.ReloadDuration += 0.25f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.Body.GravityScale = 0f;
        projectile.BulletColor = ColorPalette.ExplosiveOrange;
    }
}
