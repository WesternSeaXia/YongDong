using System;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 碰撞体几何形状类型。
/// </summary>
public enum ColliderType
{
    Circle,
    Box
}

/// <summary>
/// 基础碰撞体抽象类。
/// </summary>
public abstract class Collider
{
    /// <summary>
    /// 绑定的刚体对象。
    /// </summary>
    public RigidBody2D Body { get; set; }

    /// <summary>
    /// 碰撞形状类型。
    /// </summary>
    public abstract ColliderType Type { get; }

    /// <summary>
    /// 是否作为触发器（仅产生接触事件，不参与物理排斥与反弹）。
    /// </summary>
    public bool IsTrigger { get; set; }

    /// <summary>
    /// 表面弹性覆盖（若大于0则优先于刚体自身弹性）。
    /// </summary>
    public float BouncinessOverride { get; set; } = -1f;

    /// <summary>
    /// 是否参与物理碰撞检测。禁用后不再产生任何碰撞事件与解算。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否为子弹碰撞体（子弹与子弹之间互不碰撞抵消）。
    /// </summary>
    public bool IsProjectile { get; set; } = false;

    protected Collider(RigidBody2D body)
    {
        Body = body;
    }
}

/// <summary>
/// 圆形碰撞体（用于角色与子弹）。
/// </summary>
public class CircleCollider : Collider
{
    public float Radius { get; set; }
    public override ColliderType Type => ColliderType.Circle;

    public CircleCollider(RigidBody2D body, float radius) : base(body)
    {
        Radius = radius;
    }
}

/// <summary>
/// 轴对齐包围盒碰撞体（用于平台、立柱与墙体）。
/// </summary>
public class BoxCollider : Collider
{
    public Vector2 Size { get; set; }
    public override ColliderType Type => ColliderType.Box;

    public Vector2 HalfSize => Size * 0.5f;

    public Rectangle Bounds => new(
        (int)(Body.Position.X - HalfSize.X),
        (int)(Body.Position.Y - HalfSize.Y),
        (int)Size.X,
        (int)Size.Y
    );

    public BoxCollider(RigidBody2D body, Vector2 size) : base(body)
    {
        Size = size;
    }
}

/// <summary>
/// 物理碰撞流形结果，包含碰撞法线与重叠穿透深度。
/// </summary>
public struct CollisionManifold
{
    public bool HasCollision;
    public Vector2 Normal;           // 碰撞法线 (从 B 指向 A)
    public float Penetration;        // 穿透深度
    public Vector2 ContactPoint;     // 接触点世界坐标
    public Collider ColliderA;
    public Collider ColliderB;
}

/// <summary>
/// 射线投射测试结果。
/// </summary>
public struct RaycastHit
{
    public bool HasHit;
    public Vector2 Point;
    public Vector2 Normal;
    public float Distance;
    public Collider HitCollider;
}
