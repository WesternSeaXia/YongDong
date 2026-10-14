using System;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 集束爆破技能卡插件。
/// 职责边界：扩充角色弹匣容量，使子弹在反弹及命中目标时触发大范围强光爆炸冲击波特效与剧烈震动。
/// </summary>
public class ClusterBombCard : ICardPlugin
{
    public string Id => "card_cluster_bomb";
    public string Name => "集束爆破";
    public string Description => "命中触发范围爆破\n弹匣容量 +1";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Bomb;

    public void OnApply(Actor actor)
    {
        actor.ExplosionRadius = MathF.Max(actor.ExplosionRadius, 110f);
        actor.MaxAmmo += 1;
        actor.CurrentAmmo = actor.MaxAmmo;
    }

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        particles.EmitExplosion(projectile.Body.Position, 140f, ColorPalette.ExplosiveOrange, 40);
        shake.AddTrauma(0.50f);
    }

    public void OnProjectileBounce(Projectile projectile, CollisionManifold manifold, Action<Projectile> spawnProjectile, ParticleSystem particles)
    {
        particles.EmitExplosion(projectile.Body.Position, 95f, ColorPalette.ExplosiveOrange, 22);
    }
}
