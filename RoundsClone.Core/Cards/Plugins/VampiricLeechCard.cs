using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 生命虹吸技能卡插件。
/// 职责边界：提升角色最大生命值上限，并在子弹命中敌人时按造成伤害的一定比例恢复自身生命值。
/// </summary>
public class VampiricLeechCard : ICardPlugin
{
    public string Id => "card_vampiric_leech";
    public string Name => "生命虹吸";
    public string Description => "最大生命 +25\n伤害吸血 +30%\n换弹时间 +0.35s";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Leech;

    public void OnApply(Actor actor)
    {
        actor.MaxHealth += 25f;
        actor.CurrentHealth = MathF.Min(actor.CurrentHealth + 25f, actor.MaxHealth);
        actor.ReloadDuration += 0.35f;
        actor.RecalculateHealthScaling();
    }

    public void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        if (target != null && projectile.Owner != null && !projectile.Owner.IsDead)
        {
            float heal = projectile.Damage * 0.30f;
            projectile.Owner.CurrentHealth = MathF.Min(projectile.Owner.MaxHealth, projectile.Owner.CurrentHealth + heal);
            particles.EmitHitSparks(projectile.Owner.Body.Position, -Vector2.UnitY, ColorPalette.PlayerGreen, 8);
        }
    }
}
