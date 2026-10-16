using System;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 纯矢量力驱动的 2D 刚体物理组件。
/// 严格遵守牛顿经典力学，不采用坐标直移，一切运动均由外力累加与瞬时冲量积分驱动。
/// </summary>
public class RigidBody2D
{
    /// <summary>
    /// 刚体质量 (kg)。质量为 0 时判定为无穷大质量的固定静态地形。
    /// </summary>
    public float Mass { get; set; } = 1.0f;

    /// <summary>
    /// 逆质量缓存 (1/m)，以乘法代替除法提升高频物理步长效率。
    /// </summary>
    public float InverseMass => Mass > 0.0001f ? 1.0f / Mass : 0f;

    /// <summary>
    /// 是否为固定不动物体。
    /// </summary>
    public bool IsStatic => Mass <= 0.0001f;

    /// <summary>
    /// 刚体中心世界坐标。
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 刚体当前速度矢量 (像素/秒)。
    /// </summary>
    public Vector2 Velocity { get; set; }

    /// <summary>
    /// 空气与地面线性阻力阻尼系数 (0.0 到 1.0)。
    /// </summary>
    public float Drag { get; set; } = 0.08f;

    /// <summary>
    /// 重力影响倍率（0 为失重子弹，1 为常规角色）。
    /// </summary>
    public float GravityScale { get; set; } = 1.0f;

    /// <summary>
    /// 下落阶段的额外重力倍率（使角色下落更干脆沉稳，告别漂浮感）。
    /// </summary>
    public float FallGravityMultiplier { get; set; } = 1.25f;

    /// <summary>
    /// 碰撞弹性恢复系数 (0 为完全非弹性碰撞，1 为完全弹性碰撞)。
    /// </summary>
    public float Restitution { get; set; } = 0.2f;

    /// <summary>
    /// 是否接触地面。
    /// </summary>
    public bool IsGrounded { get; set; }

    /// <summary>
    /// 是否正落在高弹性反弹构件（弹跳床/橡胶球）上，免受坠落撞击伤害。
    /// </summary>
    public bool IsOnBouncySurface { get; set; }

    /// <summary>
    /// 站立其上的地面/运动平台当前物理移动速度。
    /// </summary>
    public Vector2 GroundVelocity { get; set; } = Vector2.Zero;

    /// <summary>
    /// 承托站立的地面刚体。
    /// </summary>
    public RigidBody2D? GroundBody { get; set; }

    /// <summary>
    /// 站立地面的表面法线（平地为 (0, -1)，斜坡带有倾斜分量）。
    /// </summary>
    public Vector2 GroundNormal { get; set; } = -Vector2.UnitY;

    /// <summary>
    /// 坡道抓地力倍率（0 为纯物理自由溜坡滑落，1 为消除沿斜坡重力下滑分量，保持如平地般站立稳定与均速移动）。
    /// </summary>
    public float SlopeGrip { get; set; } = 0f;

    /// <summary>
    /// 是否接触垂直墙壁（左墙或右墙）。
    /// </summary>
    public bool IsTouchingWall { get; set; }

    /// <summary>
    /// 接触的墙壁法线（指向脱离墙壁方向，如贴左墙为 (1, 0)，贴右墙为 (-1, 0)）。
    /// </summary>
    public Vector2 WallNormal { get; set; } = Vector2.Zero;

    /// <summary>
    /// 当前贴附的墙壁、高台或悬挂箱刚体。
    /// </summary>
    public RigidBody2D? WallBody { get; set; }

    // 帧合力累加器
    private Vector2 _accumulatedForce = Vector2.Zero;

    /// <summary>
    /// 构造刚体。
    /// </summary>
    public RigidBody2D(Vector2 position, float mass = 1.0f)
    {
        Position = position;
        Mass = mass;
    }

    /// <summary>
    /// 向刚体施加持续作用力 (牛顿)。此力将在下一次积分更新中转换为加速度。
    /// </summary>
    public void AddForce(Vector2 force)
    {
        if (IsStatic) return;
        _accumulatedForce += force;
    }

    /// <summary>
    /// 向刚体施加瞬时冲量 (如后坐力、撞击力、弹射冲量)。
    /// 改变速度: Δv = J / m。
    /// </summary>
    public void ApplyImpulse(Vector2 impulse)
    {
        if (IsStatic) return;
        Velocity += impulse * InverseMass;
    }

    /// <summary>
    /// 执行半隐式欧拉物理数值积分更新。
    /// </summary>
    /// <param name="dt">时间步长 (秒)</param>
    /// <param name="globalGravity">全局重力场加速度</param>
    public void Integrate(float dt, Vector2 globalGravity)
    {
        if (IsStatic) return;

        // 1. 计算合力产生的加速度 a = F / m + g * scale (下落时应用额外重力倍率)
        float effectiveGravityScale = GravityScale * (Velocity.Y > 0f ? FallGravityMultiplier : 1.0f);
        Vector2 totalAcceleration = (_accumulatedForce * InverseMass) + (globalGravity * effectiveGravityScale);

        // 2. 积分计算新速度
        Velocity += totalAcceleration * dt;

        // 3. 应用环境线性阻尼 (垂直方向仅保留极微弱空气阻力，确保自由落体干脆利落)
        float dragFactorX = MathF.Max(0f, 1f - Drag * dt * 60f);
        float dragFactorY = MathF.Max(0f, 1f - (Drag * 0.025f) * dt * 60f);
        Velocity = new Vector2(Velocity.X * dragFactorX, Velocity.Y * dragFactorY);

        // 4. 速度上限保护（防止超高冲击力或连续反弹使刚体在单帧内位移超过墙体厚度导致穿墙，即防隧穿保护）
        const float MaxSpeed = 2200f;
        if (Velocity.LengthSquared() > MaxSpeed * MaxSpeed)
        {
            Velocity = Vector2.Normalize(Velocity) * MaxSpeed;
        }

        // 5. 积分计算新位移
        Position += Velocity * dt;

        // 5. 重置合力积蓄
        _accumulatedForce = Vector2.Zero;
    }
}
