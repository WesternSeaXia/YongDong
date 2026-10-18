using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 黑洞引力技能卡插件。
/// 职责边界：增加弹匣容量，在子弹命中目标时产生指向爆心的强力引力拉扯冲量与大范围紫白电光坍缩爆炸特效。
/// </summary>
public class ImplosionNovaCard : ICardPlugin
{
    public const float PullRadius = 100f;

    public string Id => "card_implosion_nova";
    public string Name => "黑洞引力";
    public string Description => "命中强力黑洞牵引\n弹匣容量 +2\n射击间隔 +25%";
    public CardTier Tier => CardTier.Legendary;
    public IconType Icon => IconType.Crosshair;

    public void OnApply(Actor actor)
    {
        actor.ImplosionBullets = true;
        actor.MaxAmmo += 2;
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.FireRateCooldown *= 1.25f;
    }

    public void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        Vector2 hitCenter = projectile.Body.Position;
        if (target != null)
        {
            Vector2 pullDir = MathHelper2D.SafeNormalize(hitCenter - target.Body.Position);
            target.Body.ApplyImpulse(pullDir * 750f);
        }
        // 特效半径为 75px
        particles.EmitBlackHoleImplosion(hitCenter, 75f, ColorPalette.ElectricPurple);
        shake.AddTrauma(0.42f);
    }
}
