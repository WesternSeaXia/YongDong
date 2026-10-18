using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 嗜血追猎技能卡插件 (Bloodhunt)。
/// 职责边界：赋予角色 30% 常驻生命吸取，并在子弹命中敌方目标时提供持续 3.0 秒的 50% 移动速度爆发。
/// </summary>
public class BloodhuntCard : ICardPlugin
{
    public string Id => "card_bloodhunt";
    public string Name => "嗜血追猎";
    public string Description => "常驻吸血 +30%\n子弹命中敌人移速 +50% (持续 3s)";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Leech;

    public void OnApply(Actor actor)
    {
        actor.LifestealRatio += 0.30f;
    }

    public void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        if (target != null && target != projectile.Owner && !projectile.Owner.IsDead)
        {
            projectile.Owner.ApplySpeedBoost(3.0f, 1.50f);
            particles.EmitHitSparks(projectile.Owner.Body.Position, -Vector2.UnitY, ColorPalette.PlayerGreen, 8);
        }
    }
}
