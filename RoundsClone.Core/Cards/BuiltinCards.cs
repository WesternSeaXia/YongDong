using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 弹跳大师卡牌：增加反弹次数与反弹伤害。
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

/// <summary>
/// 集束爆破卡牌：子弹碰撞时产生范围爆炸。
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
        actor.ExplosionRadius = MathF.Max(actor.ExplosionRadius, 85f);
        actor.MaxAmmo += 1;
        actor.CurrentAmmo = actor.MaxAmmo;
    }

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        TriggerCluster(projectile, particles, shake);
    }

    public void OnProjectileBounce(Projectile projectile, CollisionManifold manifold, Action<Projectile> spawnProjectile, ParticleSystem particles)
    {
        particles.EmitExplosion(projectile.Body.Position, 70f, ColorPalette.ExplosiveOrange, 12);
    }

    private void TriggerCluster(Projectile projectile, ParticleSystem particles, ScreenShake shake)
    {
        particles.EmitExplosion(projectile.Body.Position, 110f, ColorPalette.ExplosiveOrange, 26);
        shake.AddTrauma(0.35f);
    }
}

/// <summary>
/// 剧毒催化卡牌：子弹附带持续毒素伤害。
/// </summary>
public class ToxicCatalystCard : ICardPlugin
{
    public string Id => "card_toxic_catalyst";
    public string Name => "剧毒催化";
    public string Description => "附带 3 秒中毒伤害\n换弹速度 +20%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Poison;

    public void OnApply(Actor actor)
    {
        actor.PoisonBullets = true;
        actor.ReloadDuration *= 0.80f;
    }

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        target.PoisonTimer = MathF.Max(target.PoisonTimer, 3.2f);
        particles.EmitHitSparks(target.Body.Position, Vector2.UnitY, ColorPalette.PoisonGreen, 16);
    }
}

/// <summary>
/// 重装火炮卡牌：大幅提升单发伤害与击退。
/// </summary>
public class HeavyArtilleryCard : ICardPlugin
{
    public string Id => "card_heavy_artillery";
    public string Name => "重装火炮";
    public string Description => "伤害 +85%\n弹丸体积 +70%\n换弹时间 +25%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Heavy;

    public void OnApply(Actor actor)
    {
        actor.BulletDamage *= 1.85f;
        actor.RecoilImpulse *= 1.8f;
        actor.FireRateCooldown *= 1.35f;
        actor.ReloadDuration *= 1.25f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.Radius *= 1.7f;
        projectile.Collider.Radius *= 1.7f;
        projectile.KnockbackImpulse *= 2.2f;
    }
}

/// <summary>
/// 极速连发卡牌：提升射速、散射与弹匣容量。
/// </summary>
public class RapidFireCard : ICardPlugin
{
    public string Id => "card_rapid_fire";
    public string Name => "极速连发";
    public string Description => "射速 +65%\n每次发射子弹 +1\n弹匣容量 +3";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.FireRateCooldown *= 0.55f;
        actor.BulletsPerShot += 1;
        actor.BulletSpread += 0.12f;
        actor.MaxAmmo += 3;
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.ReloadDuration *= 0.85f;
    }
}

/// <summary>
/// 生命虹吸卡牌：生命提升与吸血恢复。
/// </summary>
public class VampiricLeechCard : ICardPlugin
{
    public string Id => "card_vampiric_leech";
    public string Name => "生命虹吸";
    public string Description => "最大生命 +25\n伤害吸血 +30%";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Leech;

    public void OnApply(Actor actor)
    {
        actor.MaxHealth += 25f;
        actor.CurrentHealth = MathF.Min(actor.CurrentHealth + 25f, actor.MaxHealth);
        actor.RecalculateHealthScaling();
    }

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        if (projectile.Owner != null && !projectile.Owner.IsDead)
        {
            float heal = projectile.Damage * 0.30f;
            projectile.Owner.CurrentHealth = MathF.Min(projectile.Owner.MaxHealth, projectile.Owner.CurrentHealth + heal);
            particles.EmitHitSparks(projectile.Owner.Body.Position, -Vector2.UnitY, ColorPalette.PlayerGreen, 8);
        }
    }
}

/// <summary>
/// 黑洞引力卡牌：命中产生引力拉扯。
/// </summary>
public class ImplosionNovaCard : ICardPlugin
{
    public string Id => "card_implosion_nova";
    public string Name => "黑洞引力";
    public string Description => "命中产生强力牵引\n弹匣容量 +2";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Crosshair;

    public void OnApply(Actor actor)
    {
        actor.ImplosionBullets = true;
        actor.MaxAmmo += 2;
        actor.CurrentAmmo = actor.MaxAmmo;
    }

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        Vector2 hitCenter = projectile.Body.Position;
        Vector2 pullDir = MathHelper2D.SafeNormalize(hitCenter - target.Body.Position);
        target.Body.ApplyImpulse(pullDir * 650f);
        particles.EmitExplosion(hitCenter, 80f, ColorPalette.ElectricPurple, 18);
    }
}

/// <summary>
/// 雷霆冲刺卡牌：冲刺副武器强化。
/// </summary>
public class ThunderDashCard : ICardPlugin
{
    public string Id => "card_thunder_dash";
    public string Name => "雷霆冲刺";
    public string Description => "冲刺速度 +40%\n副武器冷却 -30%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Dash;

    public void OnApply(Actor actor)
    {
        if (actor.SubWeapon is DashSubWeapon dash)
        {
            dash.DashSpeed *= 1.4f;
            dash.Cooldown *= 0.70f;
        }
        else if (actor.SubWeapon != null)
        {
            actor.SubWeapon.Cooldown *= 0.8f;
        }
    }

    public void OnSecondaryUsed(Actor actor, ISubWeapon weapon)
    {
        actor.Body.ApplyImpulse(actor.Body.Velocity * 0.25f);
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.ReloadTimer = 0f;
    }
}

/// <summary>
/// 绝对弹反卡牌：格挡副武器强化。
/// </summary>
public class AbsoluteParryCard : ICardPlugin
{
    public string Id => "card_absolute_parry";
    public string Name => "绝对反弹";
    public string Description => "副武器冷却 -35%\n反弹伤害 +50%";
    public CardTier Tier => CardTier.Legendary;
    public IconType Icon => IconType.Shield;

    public void OnApply(Actor actor)
    {
        if (actor.SubWeapon is BlockSubWeapon block)
        {
            block.Cooldown *= 0.65f;
        }
        else if (actor.SubWeapon != null)
        {
            actor.SubWeapon.Cooldown *= 0.8f;
        }
    }
}

/// <summary>
/// 泰坦装甲卡牌：大幅增加生命上限。
/// </summary>
public class TitanArmorCard : ICardPlugin
{
    public string Id => "card_titan_armor";
    public string Name => "泰坦装甲";
    public string Description => "最大生命 +80";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Heart;

    public void OnApply(Actor actor)
    {
        actor.MaxHealth += 80f;
        actor.CurrentHealth += 80f;
        actor.RecalculateHealthScaling();
    }
}

/// <summary>
/// 火箭助推卡牌：子弹提速且无重力下坠。
/// </summary>
public class AcceleratingRocketCard : ICardPlugin
{
    public string Id => "card_rocket_thruster";
    public string Name => "火箭助推";
    public string Description => "弹速 +45%\n子弹无重力下坠";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.BulletSpeed *= 1.45f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.Body.GravityScale = 0f;
        projectile.BulletColor = ColorPalette.ExplosiveOrange;
    }
}

/// <summary>
/// 极寒冰冻卡牌：子弹命中减速。
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

    public void OnProjectileHit(Projectile projectile, Actor target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake)
    {
        target.Body.Velocity *= 0.5f;
        particles.EmitHitSparks(target.Body.Position, Vector2.UnitY, ColorPalette.FreezeCyan, 15);
    }
}
