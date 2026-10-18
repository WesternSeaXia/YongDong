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
/// 职责边界：模拟子弹刚体弹道、反弹裂变、范围爆炸判定，以及具有方向感的流线型流线弹头与其克制微弱的淡雅拖尾渲染。
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
    /// 子弹截面基准半径与伤害基数。
    /// </summary>
    public float Radius { get; set; } = 5.2f;
    public float BaseRadius { get; set; } = 5.2f;
    public float BaseDamage { get; set; } = 25f;

    /// <summary>
    /// 新机制标志位。
    /// </summary>
    public bool IsGrowBullet { get; set; } = false;
    public bool IsSneakyBullet { get; set; } = false;
    public bool HasTargetBounce { get; set; } = false;
    public bool HasTargetBounced { get; set; } = false;
    public bool SpawnsToxicCloud { get; set; } = false;
    public bool CanScavengeAmmo { get; set; } = true;

    /// <summary>
    /// 弹头基准色彩（跟随发射者主题色）。
    /// </summary>
    public Color BulletColor { get; set; } = ColorPalette.PlayerBlue;

    // 历史位置轨迹记录，用于绘制微弱淡雅的短尾迹
    private readonly List<Vector2> _trailHistory = new(8);
    private float _trailSampleTimer = 0f;
    private float _particleTimer = 0f;

    /// <summary>
    /// 创建力学子弹实体。
    /// </summary>
    public Projectile(Actor owner, Vector2 position, Vector2 initialVelocity, float damage, Color color)
    {
        Owner = owner;
        Damage = damage;
        BaseDamage = damage;
        BaseRadius = Radius;
        BulletColor = color;

        Body = new RigidBody2D(position, 0.16f)
        {
            Velocity = initialVelocity,
            Drag = 0.005f,
            GravityScale = 0.54f,
            FallGravityMultiplier = 1.15f,
            Restitution = 0.95f,
            ManualPositionIntegration = true
        };

        Collider = new CircleCollider(Body, Radius)
        {
            BouncinessOverride = 0.95f,
            IsProjectile = true
        };

        _trailHistory.Add(position);
    }

    /// <summary>
    /// 帧更新子弹物理轨迹、贴地飞行探测、飞行增殖与克制的尾迹采样。
    /// </summary>
    public void Update(float dt, ParticleSystem particles, CollisionWorld? world = null)
    {
        if (IsDestroyed) return;

        Lifetime += dt;
        if (Lifetime >= MaxLifetime)
        {
            Destroy();
            return;
        }

        // 1. 飞行增殖机制 (Grow)：在空中每 0.5 秒增加 25% 体积，每 0.5 秒增加 12.5% 伤害
        if (IsGrowBullet)
        {
            float scale = 1.0f + (Lifetime / 0.5f) * 0.25f;
            Radius = BaseRadius * scale;
            Collider.Radius = Radius;
            Damage = BaseDamage * (1.0f + (Lifetime / 0.5f) * 0.125f);
        }

        // 2. 贴地飞行机制 (Sneaky)：外层气垫排斥力场（探测地表并产生向外规避推力，让子弹在触地前优雅弧线扬起并沿地形滑翔）
        if (IsSneakyBullet && world != null && !IsDestroyed)
        {
            const float FieldRadius = 38f;
            if (world.QueryClosestSurface(Body.Position, FieldRadius, out float dist, out Vector2 surfNorm, out Vector2 surfPt, Collider))
            {
                // 仅对具有向上支撑分量的地表/斜坡产生地面规避推力 (坡度 < 90°，即 surfNorm.Y <= -0.05f)
                // 垂直立墙由物理碰撞 CCD 负责触发反弹
                if (surfNorm.Y <= -0.05f && dist > Radius)
                {
                    float t = 1.0f - (dist - Radius) / (FieldRadius - Radius);
                    t = MathHelper.Clamp(t, 0f, 1f);

                    // 距离地表越近，向外的推力越强 (二次方递增，产生“想要逃离地面”的强烈气垫感)
                    float repelAccel = 11000f * (t * t);
                    Vector2 repelForce = surfNorm * repelAccel;

                    Vector2 currentVel = Body.Velocity;
                    float currentSpeed = currentVel.Length();

                    // 施加排斥推力并保持总速率恒定 (纯方向平滑偏转，不损耗动能)
                    Vector2 newVel = currentVel + repelForce * dt;
                    if (currentSpeed > 10f)
                    {
                        Body.Velocity = MathHelper2D.SafeNormalize(newVel) * currentSpeed;
                    }

                    // 靠近地表时喷射轻柔的气垫微光粒子
                    if (t > 0.4f && Random.Shared.NextDouble() < 0.25)
                    {
                        particles.EmitHitSparks(surfPt, surfNorm, BulletColor * 0.45f, 1);
                    }
                }
            }
        }

        // 3. 低频采样简短历史节点（仅保留 4 个点，极短极淡的微弱拖尾）
        _trailSampleTimer += dt;
        if (_trailSampleTimer >= 0.024f)
        {
            _trailSampleTimer = 0f;
            _trailHistory.Add(Body.Position);
            if (_trailHistory.Count > 4)
            {
                _trailHistory.RemoveAt(0);
            }
        }

        // 4. 降低尾焰粒子喷发频率，避免拖尾视觉喧宾夺主
        _particleTimer += dt;
        if (_particleTimer >= 0.08f)
        {
            _particleTimer = 0f;
            particles.EmitProjectileTrail(Body.Position, Body.Velocity, BulletColor);
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
    /// 渲染流线型细长弹头与克制微弱的短拖尾。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        Vector2 vel = Body.Velocity;
        Vector2 dir = MathHelper2D.SafeNormalize(vel);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;

        // 1. 绘制极淡、微弱的细线短拖尾
        int count = _trailHistory.Count;
        for (int i = 0; i < count - 1; i++)
        {
            float t = (float)(i + 1) / count;
            float alpha = t * 0.28f; // 低不透明度
            float trailWidth = MathHelper.Lerp(0.8f, 2.2f, t);
            Color trailColor = BulletColor * alpha;

            batcher.DrawLine(spriteBatch, _trailHistory[i], _trailHistory[i + 1], trailColor, trailWidth);
        }

        if (count > 0)
        {
            batcher.DrawLine(spriteBatch, _trailHistory[^1], Body.Position, BulletColor * 0.35f, 2.4f);
        }

        // 2. 绘制流线型细长子弹外形（沿飞行方向延伸的流线胶囊弹头，消除纯圆球感）
        float halfLen = Radius * 1.5f; // 约 7.8 像素
        Vector2 tipPos = Body.Position + dir * halfLen;
        Vector2 tailPos = Body.Position - dir * (halfLen * 0.8f);

        // 外层弹壳（角色主题色）
        batcher.DrawLine(spriteBatch, tailPos, tipPos, BulletColor, Radius * 1.25f);
        batcher.DrawFilledCircle(spriteBatch, tailPos, Radius * 0.55f, BulletColor);

        // 内部炽热白芯
        Vector2 coreTail = Body.Position - dir * (halfLen * 0.4f);
        batcher.DrawLine(spriteBatch, coreTail, tipPos, Color.White, Radius * 0.65f);
        batcher.DrawFilledCircle(spriteBatch, tipPos, Radius * 0.40f, Color.White);
    }
}
