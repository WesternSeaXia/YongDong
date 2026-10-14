using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 卡牌稀有度品质。
/// </summary>
public enum CardTier
{
    Common,      // 普通
    Rare,        // 稀有
    Epic,        // 史诗
    Legendary    // 传说
}

/// <summary>
/// 插件化技能卡牌核心接口。
/// 挂载各种战斗生命周期拦截钩子，支持任意多张卡牌属性的无缝连锁叠乘。
/// </summary>
public interface ICardPlugin
{
    /// <summary>
    /// 卡牌唯一英文标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 卡牌显示中文名。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 卡牌功能描述。
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 品质稀有度。
    /// </summary>
    CardTier Tier { get; }

    /// <summary>
    /// 矢量图标类型。
    /// </summary>
    IconType Icon { get; }

    /// <summary>
    /// 当玩家在选卡界面选择此卡时触发，用于永久修改角色的基础属性或机制参数。
    /// </summary>
    void OnApply(Actor actor);

    /// <summary>
    /// 当开火生成子弹时触发拦截器，可为生成的子弹赋予新属性、修改初速度或挂载状态。
    /// </summary>
    void OnProjectileSpawn(Actor actor, Projectile projectile) { }

    /// <summary>
    /// 当子弹发生墙体或障碍物反弹时触发，可派生分裂弹、生成范围地雷或加速。
    /// </summary>
    void OnProjectileBounce(Projectile projectile, CollisionManifold manifold, Action<Projectile> spawnProjectile, ParticleSystem particles) { }

    /// <summary>
    /// 当子弹命中目标（敌人、地形、掩体或墙体）时触发，可执行吸血、中毒、爆炸、引力黑洞等连锁打击。
    /// </summary>
    void OnProjectileHit(Projectile projectile, Actor? target, CollisionManifold manifold, ParticleSystem particles, ScreenShake shake) { }

    /// <summary>
    /// 当角色释放副武器 (格挡/冲刺) 时触发，可产生副武器连锁附加效果。
    /// </summary>
    void OnSecondaryUsed(Actor actor, ISubWeapon weapon) { }
}
