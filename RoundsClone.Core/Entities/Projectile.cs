using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 纯力学驱动的程序化子弹弹丸实体。
/// 支持反弹管线、分裂、范围爆炸、追踪引力与连锁状态附加。
/// </summary>
public class Projectile
{
    /// <summary>
    /// 子弹刚体。
    /// </summary>
    public RigidBody2D Body { get; }

    /// <summary>
    /// 子弹碰撞体。
    /// </summary>
    public CircleCollider Collider { get; }

    /// <summary>
    /// 发射拥有者。
    /// </summary>
    public Actor Owner { get; set; }

    /// <summary>
    /// 基础打击伤害。
    /// </summary>
    public float Damage { get; set; } = 25f;

    /// <summary>
    /// 基础击退冲量大小。
    /// </summary>
    public float KnockbackImpulse { get; set; } = 450f;

    /// <summary>
    /// 剩余反弹次数（初始默认为 0，需选配反弹卡牌赋予反弹能力）。
    /// </summary>
    public int RemainingBounces { get; set; } = 0;

    /// <summary>
    /// 每次反弹时的速度提升系数。
    /// </summary>
    public float BounceSpeedMultiplier { get; set; } = 1.05f;

    /// <summary>
    /// 爆炸半径 (0 代表普通直击子弹)。
    /// </summary>
    public float ExplosionRadius { get; set; } = 0f;

    /// <summary>
    /// 是否附带持续剧毒腐蚀效果。
    /// </summary>
    public bool IsPoisonous { get; set; } = false;

    /// <summary>
    /// 是否具有引力坍缩/黑洞牵引力。
    /// </summary>
    public bool HasImplosion { get; set; } = false;

    /// <summary>
    /// 反弹时分裂出子弹的数量。
    /// </summary>
    public int SplitOnBounceCount { get; set; } = 0;

    /// <summary>
    /// 弹丸存活时间与最大生命时长 (秒)。控制有效射程。
    /// </summary>
    public float Lifetime { get; private set; } = 0f;
    public float MaxLifetime { get; set; } = 2.4f;

    /// <summary>
    /// 是否已被销毁。
    /// </summary>
    public bool IsDestroyed { get; private set; } = false;

    /// <summary>
    /// 子弹弹头半径。
    /// </summary>
    public float Radius { get; set; } = 8f;

    /// <summary>
    /// 弹头基准色彩。
    /// </summary>
    public Color BulletColor { get; set; } = Color.Yellow;

    private float _trailTimer = 0f;

    /// <summary>
    /// 创建力学子弹实体。
    /// </summary>
    public Projectile(Actor owner, Vector2 position, Vector2 initialVelocity, float damage, Color color)
    {
        Owner = owner;
        Damage = damage;
        BulletColor = color;

        Body = new RigidBody2D(position, 0.2f)
        {
            Velocity = initialVelocity,
            Drag = 0.006f,               // 轻微空气风阻
            GravityScale = 0.56f,        // 扎实沉重的弹道抛物下坠感
            FallGravityMultiplier = 1.15f,
            Restitution = 0.95f
        };

        Collider = new CircleCollider(Body, Radius)
        {
            BouncinessOverride = 0.95f
        };
    }

    /// <summary>
    /// 帧更新子弹物理轨迹与粒子尾迹。
    /// </summary>
    public void Update(float dt, ParticleSystem particles)
    {
        if (IsDestroyed) return;

        Lifetime += dt;
        if (Lifetime >= MaxLifetime)
        {
            Destroy();
            return;
        }

        // 发射尾焰与飞行光带微粒
        _trailTimer += dt;
        if (_trailTimer >= 0.02f)
        {
            _trailTimer = 0f;
            particles.EmitDashTrail(Body.Position, Body.Velocity * 0.2f, BulletColor);
        }
    }

    /// <summary>
    /// 发生反弹时的速度修正与行为。
    /// </summary>
    public void OnBounce(Vector2 normal)
    {
        RemainingBounces--;
        Body.Velocity *= BounceSpeedMultiplier;

        if (RemainingBounces < 0)
        {
            Destroy();
        }
    }

    /// <summary>
    /// 销毁子弹并标记移除。
    /// </summary>
    public void Destroy()
    {
        IsDestroyed = true;
        Collider.IsEnabled = false;
    }

    /// <summary>
    /// 渲染程序化发光弹丸。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        // 核心白亮，外圈带有霓虹发光光晕
        batcher.DrawGlowCircle(spriteBatch, Body.Position, Radius, Color.White, BulletColor);

        // 前端微小导向高光
        Vector2 headOffset = MathHelper2D.SafeNormalize(Body.Velocity) * (Radius * 0.4f);
        batcher.DrawFilledCircle(spriteBatch, Body.Position + headOffset, Radius * 0.35f, Color.White);
    }
}
