using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 极寒冰冻技能卡插件。
/// 职责边界：提升换弹速度，并在子弹命中目标时大幅削减敌方当前运动速度矢量并迸发冰霜碎屑特效。
/// </summary>
public class FrostFreezeCard : ICardPlugin
{
    public string Id => "card_frost_freeze";
    public string Name => "极寒冰冻";
    public string Description => "命中减速 50%\n换弹速度 +15%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Frost;

    public void OnApply(Actor actor)
    {
        actor.ReloadDuration *= 0.85f;
    }

    public void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        if (target != null)
        {
            target.Body.Velocity *= 0.5f;
            particles.EmitHitSparks(target.Body.Position, Vector2.UnitY, ColorPalette.FreezeCyan, 15);
        }
        else
        {
            particles.EmitHitSparks(projectile.Body.Position, manifold.Normal, ColorPalette.FreezeCyan, 8);
        }
    }
}
