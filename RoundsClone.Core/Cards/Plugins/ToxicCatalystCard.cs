using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 剧毒催化技能卡插件。
/// 职责边界：缩短角色换弹时间，并在子弹命中目标时施加持续腐蚀扣血的中毒状态。
/// </summary>
public class ToxicCatalystCard : ICardPlugin
{
    public string Id => "card_toxic_catalyst";
    public string Name => "剧毒催化";
    public string Description => "附带 3.2秒持续剧毒\n换弹速度 +20%\n初始直接伤害 -15%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Poison;

    public void OnApply(Actor actor)
    {
        actor.PoisonBullets = true;
        actor.ReloadDuration *= 0.80f;
        actor.BulletDamage *= 0.85f;
    }

    public void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        if (target != null)
        {
            target.PoisonTimer = MathF.Max(target.PoisonTimer, 3.2f);
            particles.EmitHitSparks(target.Body.Position, Vector2.UnitY, ColorPalette.PoisonGreen, 16);
        }
        else
        {
            particles.EmitHitSparks(projectile.Body.Position, manifold.Normal, ColorPalette.PoisonGreen, 8);
        }
    }
}
