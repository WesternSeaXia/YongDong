using System;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 弹跳大师技能卡插件。
/// 职责边界：为角色子弹提供额外的墙面反弹次数，并在每次发生反弹时递增子弹伤害与爆发火花特效。
/// </summary>
public class BounceMasterCard : ICardPlugin
{
    public string Id => "card_bounce_master";
    public string Name => "弹跳大师";
    public string Description => "弹射次数 +2\n反弹伤害 +25%";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Bounce;

    public void OnApply(Actor actor)
    {
        actor.BulletBounces += 2;
    }

    public void OnProjectileBounce(Projectile projectile, CollisionManifold manifold, Action<Projectile> spawnProjectile, ParticleSystem particles)
    {
        projectile.Damage *= 1.25f;
        particles.EmitHitSparks(projectile.Body.Position, manifold.Normal, ColorPalette.BouncySurface, 10);
    }
}
