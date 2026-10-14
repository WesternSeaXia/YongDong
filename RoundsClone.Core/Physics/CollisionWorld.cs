using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 2D 物理世界管理器。
/// 统一调度重力场、刚体欧拉积分、窄相碰撞检测、弹回冲量解算以及穿透修正。
/// </summary>
public class CollisionWorld
{
    private readonly List<Collider> _colliders = new();
    private readonly List<RopeConstraint> _ropes = new();

    /// <summary>
    /// 全局重力场矢量 (向下 4000 像素/秒²，提供扎实沉稳的下落手感)。
    /// </summary>
    public Vector2 Gravity { get; set; } = new(0f, 4000f);

    /// <summary>
    /// 注册一个碰撞体到物理世界。
    /// </summary>
    public void AddCollider(Collider collider)
    {
        if (!_colliders.Contains(collider))
        {
            _colliders.Add(collider);
        }
    }

    /// <summary>
    /// 从物理世界中注销碰撞体。
    /// </summary>
    public void RemoveCollider(Collider collider)
    {
        _colliders.Remove(collider);
    }

    /// <summary>
    /// 注册一根绳索/吊链约束。
    /// </summary>
    public void AddRope(RopeConstraint rope)
    {
        if (!_ropes.Contains(rope))
        {
            _ropes.Add(rope);
        }
    }

    /// <summary>
    /// 注销一根绳索/吊链约束。
    /// </summary>
    public void RemoveRope(RopeConstraint rope)
    {
        _ropes.Remove(rope);
    }

    /// <summary>
    /// 清除所有碰撞体与绳索约束。
    /// </summary>
    public void Clear()
    {
        _colliders.Clear();
        _ropes.Clear();
    }

    /// <summary>
    /// 执行一次固定或动态物理帧计算。
    /// </summary>
    /// <param name="dt">物理时间步长 (秒)</param>
    /// <param name="onCollision">碰撞接触回调事件</param>
    public void Step(float dt, Action<CollisionManifold>? onCollision = null)
    {
        // 步骤 1: 重置运动状态并积分刚体物理
        HashSet<RigidBody2D> updatedBodies = new();
        foreach (var col in _colliders)
        {
            if (col.Body != null && !col.Body.IsStatic && updatedBodies.Add(col.Body))
            {
                col.Body.IsGrounded = false;
                col.Body.IsTouchingWall = false;
                col.Body.WallNormal = Vector2.Zero;
                col.Body.IsOnBouncySurface = false;
                col.Body.GroundVelocity = Vector2.Zero;
                col.Body.GroundBody = null;
                col.Body.Integrate(dt, Gravity);
            }
        }

        // 步骤 1.5: 求解绳索与吊链的长度拉伸约束
        for (int r = 0; r < _ropes.Count; r++)
        {
            _ropes[r].Solve(dt);
        }

        // 步骤 2: 遍历所有碰撞对并进行窄相解算
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = i + 1; j < _colliders.Count; j++)
            {
                var a = _colliders[i];
                var b = _colliders[j];

                if (!a.IsEnabled || !b.IsEnabled) continue;

                // 子弹与子弹之间互不碰撞、互不抵消
                if (a.IsProjectile && b.IsProjectile) continue;

                // 两个静态物体之间不解算
                if (a.Body.IsStatic && b.Body.IsStatic) continue;

                if (DetectCollision(a, b, out var manifold))
                {
                    // 派发接触通知
                    onCollision?.Invoke(manifold);

                    // 若物体在碰撞事件中被禁用（例如子弹被销毁），跳过后续物理冲量解算
                    if (!a.IsEnabled || !b.IsEnabled) continue;

                    // 非触发器则进行刚体反弹冲量与位置排斥解算
                    if (!a.IsTrigger && !b.IsTrigger)
                    {
                        ResolveCollision(ref manifold, dt);
                    }
                }
            }
        }

        // 步骤 3: 碰撞冲量解算完成后再次修正绳索约束，杜绝碰撞把箱子砸出绳长
        for (int r = 0; r < _ropes.Count; r++)
        {
            _ropes[r].Solve(dt);
        }
    }

    /// <summary>
    /// 窄相几何碰撞检测。
    /// </summary>
    public bool DetectCollision(Collider a, Collider b, out CollisionManifold manifold)
    {
        manifold = default;

        if (a is CircleCollider circleA && b is CircleCollider circleB)
        {
            return TestCircleVsCircle(circleA, circleB, out manifold);
        }
        if (a is CircleCollider cA && b is BoxCollider bB)
        {
            return TestCircleVsBox(cA, bB, out manifold);
        }
        if (a is BoxCollider bA && b is CircleCollider cB)
        {
            bool hit = TestCircleVsBox(cB, bA, out manifold);
            if (hit)
            {
                manifold.Normal = -manifold.Normal;
                (manifold.ColliderA, manifold.ColliderB) = (manifold.ColliderB, manifold.ColliderA);
            }
            return hit;
        }
        if (a is CircleCollider cA2 && b is PolygonCollider pB)
        {
            return SatCollision.TestCircleVsPolygon(cA2, pB, out manifold);
        }
        if (a is PolygonCollider pA && b is CircleCollider cB2)
        {
            bool hit = SatCollision.TestCircleVsPolygon(cB2, pA, out manifold);
            if (hit)
            {
                manifold.Normal = -manifold.Normal;
                (manifold.ColliderA, manifold.ColliderB) = (manifold.ColliderB, manifold.ColliderA);
            }
            return hit;
        }
        if (a is BoxCollider bA2 && b is PolygonCollider pB2)
        {
            return SatCollision.TestBoxVsPolygon(bA2, pB2, out manifold);
        }
        if (a is PolygonCollider pA2 && b is BoxCollider bB2)
        {
            bool hit = SatCollision.TestBoxVsPolygon(bB2, pA2, out manifold);
            if (hit)
            {
                manifold.Normal = -manifold.Normal;
                (manifold.ColliderA, manifold.ColliderB) = (manifold.ColliderB, manifold.ColliderA);
            }
            return hit;
        }
        if (a is PolygonCollider pA3 && b is PolygonCollider pB3)
        {
            return SatCollision.TestPolygonVsPolygon(pA3, pB3, out manifold);
        }
        if (a is BoxCollider bA3 && b is BoxCollider bB3)
        {
            return SatCollision.TestBoxVsBox(bA3, bB3, out manifold);
        }

        return false;
    }

    private bool TestCircleVsCircle(CircleCollider a, CircleCollider b, out CollisionManifold manifold)
    {
        manifold = default;
        Vector2 delta = a.Body.Position - b.Body.Position;
        float distSq = delta.LengthSquared();
        float radiusSum = a.Radius + b.Radius;

        if (distSq > radiusSum * radiusSum) return false;

        float dist = MathF.Sqrt(distSq);
        Vector2 normal = dist > 0.0001f ? delta / dist : -Vector2.UnitY;
        float penetration = radiusSum - dist;

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = a,
            ColliderB = b,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = b.Body.Position + normal * b.Radius
        };
        return true;
    }

    private bool TestCircleVsBox(CircleCollider circle, BoxCollider box, out CollisionManifold manifold)
    {
        manifold = default;
        Vector2 cPos = circle.Body.Position;
        Vector2 bPos = box.Body.Position;
        Vector2 half = box.HalfSize;

        // 计算圆心在 Box 上的最近投影点
        float closestX = MathHelper.Clamp(cPos.X, bPos.X - half.X, bPos.X + half.X);
        float closestY = MathHelper.Clamp(cPos.Y, bPos.Y - half.Y, bPos.Y + half.Y);
        Vector2 closest = new(closestX, closestY);

        Vector2 delta = cPos - closest;
        float distSq = delta.LengthSquared();

        // 检查圆心是否在矩形外部且超出半径
        if (distSq > circle.Radius * circle.Radius) return false;

        float dist = MathF.Sqrt(distSq);
        Vector2 normal;
        float penetration;

        if (dist > 0.0001f)
        {
            normal = delta / dist;
            penetration = circle.Radius - dist;
        }
        else
        {
            // 圆心在矩形内部时，挑选穿透距离最短的轴向推出
            float leftPen = cPos.X - (bPos.X - half.X);
            float rightPen = (bPos.X + half.X) - cPos.X;
            float topPen = cPos.Y - (bPos.Y - half.Y);
            float botPen = (bPos.Y + half.Y) - cPos.Y;

            float minPen = Math.Min(Math.Min(leftPen, rightPen), Math.Min(topPen, botPen));
            if (minPen == topPen) normal = -Vector2.UnitY;
            else if (minPen == botPen) normal = Vector2.UnitY;
            else if (minPen == leftPen) normal = -Vector2.UnitX;
            else normal = Vector2.UnitX;

            penetration = circle.Radius + minPen;
        }

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = circle,
            ColliderB = box,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = closest
        };
        return true;
    }

    private void ResolveCollision(ref CollisionManifold m, float dt)
    {
        var bodyA = m.ColliderA.Body;
        var bodyB = m.ColliderB.Body;

        // 决定反弹弹性系数
        float restA = m.ColliderA.BouncinessOverride >= 0f ? m.ColliderA.BouncinessOverride : bodyA.Restitution;
        float restB = m.ColliderB.BouncinessOverride >= 0f ? m.ColliderB.BouncinessOverride : bodyB.Restitution;
        float restitution = MathF.Max(restA, restB);

        // 判定地面着陆支撑与垂直墙面接触
        if (!bodyA.IsStatic)
        {
            if (m.Normal.Y < -0.6f)
            {
                if (restitution >= 0.5f)
                {
                    bodyA.IsOnBouncySurface = true;
                    // 若正在以高速向上强力弹起，不锁定地面状态
                    if (bodyA.Velocity.Y > -120f)
                    {
                        bodyA.IsGrounded = true;
                    }
                }
                else
                {
                    bodyA.IsGrounded = true;
                }

                bodyA.GroundBody = bodyB;
                bodyA.GroundVelocity = bodyB.Velocity;

                // 站在移动平台上时，直接跟随平台位移平滑移动
                if (bodyB.Velocity != Vector2.Zero)
                {
                    bodyA.Position += bodyB.Velocity * dt;
                }
            }
            else if (MathF.Abs(m.Normal.X) > 0.6f && MathF.Abs(m.Normal.Y) < 0.6f && bodyB.IsStatic)
            {
                bodyA.IsTouchingWall = true;
                bodyA.WallNormal = new Vector2(MathF.Sign(m.Normal.X), 0f);
            }
        }

        if (!bodyB.IsStatic)
        {
            if (m.Normal.Y > 0.6f)
            {
                if (restitution >= 0.5f)
                {
                    bodyB.IsOnBouncySurface = true;
                    if (bodyB.Velocity.Y > -120f)
                    {
                        bodyB.IsGrounded = true;
                    }
                }
                else
                {
                    bodyB.IsGrounded = true;
                }

                bodyB.GroundBody = bodyA;
                bodyB.GroundVelocity = bodyA.Velocity;

                if (bodyA.Velocity != Vector2.Zero)
                {
                    bodyB.Position += bodyA.Velocity * dt;
                }
            }
            else if (MathF.Abs(m.Normal.X) > 0.6f && MathF.Abs(m.Normal.Y) < 0.6f && bodyA.IsStatic)
            {
                bodyB.IsTouchingWall = true;
                bodyB.WallNormal = new Vector2(-MathF.Sign(m.Normal.X), 0f);
            }
        }

        // 计算相对速度与沿碰撞法线的分量
        Vector2 relVel = bodyA.Velocity - bodyB.Velocity;
        float velAlongNormal = Vector2.Dot(relVel, m.Normal);

        // 两物体正在彼此远离，无需处理冲量
        if (velAlongNormal > 0f) return;

        // 计算牛顿恢复冲量大小
        float totalInvMass = bodyA.InverseMass + bodyB.InverseMass;
        if (totalInvMass <= 0.0001f) return;

        float j = -(1f + restitution) * velAlongNormal / totalInvMass;
        Vector2 impulse = j * m.Normal;

        bodyA.ApplyImpulse(impulse);
        bodyB.ApplyImpulse(-impulse);

        // 弹性限制：避免反弹无限累加越弹越高
        if (restitution >= 0.5f)
        {
            if (bodyA.Velocity.Y < -1150f) bodyA.Velocity = new Vector2(bodyA.Velocity.X, -1150f);
            if (bodyB.Velocity.Y < -1150f) bodyB.Velocity = new Vector2(bodyB.Velocity.X, -1150f);
        }

        // 位置穿透纠偏 (Baumgarte 稳定算法，防止沉降陷入)
        float percent = 0.75f;
        float slop = 0.05f;
        float correctionMagnitude = MathF.Max(m.Penetration - slop, 0f) / totalInvMass * percent;
        Vector2 correction = correctionMagnitude * m.Normal;

        if (!bodyA.IsStatic) bodyA.Position += correction * bodyA.InverseMass;
        if (!bodyB.IsStatic) bodyB.Position -= correction * bodyB.InverseMass;
    }

    /// <summary>
    /// 从起点向指定方向投射射线，测试与世界中方块、多边形或圆形障碍物的相交情况。
    /// </summary>
    public RaycastHit Raycast(Vector2 origin, Vector2 direction, float maxDistance, Collider? ignoreCollider = null)
    {
        RaycastHit closestHit = default;
        closestHit.Distance = maxDistance;

        Vector2 normDir = MathHelper2D.SafeNormalize(direction);
        if (normDir == Vector2.Zero) return closestHit;

        foreach (var col in _colliders)
        {
            if (col == ignoreCollider || !col.IsEnabled || col.IsTrigger || col.IsProjectile) continue;

            if (col is BoxCollider box)
            {
                if (RayIntersectBox(origin, normDir, box, out float dist, out Vector2 normal))
                {
                    if (dist >= 0f && dist < closestHit.Distance)
                    {
                        closestHit.HasHit = true;
                        closestHit.Distance = dist;
                        closestHit.Point = origin + normDir * dist;
                        closestHit.Normal = normal;
                        closestHit.HitCollider = box;
                    }
                }
            }
            else if (col is PolygonCollider poly)
            {
                if (SatCollision.RayIntersectPolygon(origin, normDir, poly, out float dist, out Vector2 normal))
                {
                    if (dist >= 0f && dist < closestHit.Distance)
                    {
                        closestHit.HasHit = true;
                        closestHit.Distance = dist;
                        closestHit.Point = origin + normDir * dist;
                        closestHit.Normal = normal;
                        closestHit.HitCollider = poly;
                    }
                }
            }
            else if (col is CircleCollider circle)
            {
                if (SatCollision.RayIntersectCircle(origin, normDir, circle, out float dist, out Vector2 normal))
                {
                    if (dist >= 0f && dist < closestHit.Distance)
                    {
                        closestHit.HasHit = true;
                        closestHit.Distance = dist;
                        closestHit.Point = origin + normDir * dist;
                        closestHit.Normal = normal;
                        closestHit.HitCollider = circle;
                    }
                }
            }
        }

        return closestHit;
    }

    private bool RayIntersectBox(Vector2 origin, Vector2 dir, BoxCollider box, out float distance, out Vector2 normal)
    {
        distance = -1f;
        normal = Vector2.Zero;

        Rectangle bounds = box.Bounds;
        if (bounds.Contains(origin)) return false;

        float tmin = (bounds.Left - origin.X) / (dir.X != 0 ? dir.X : 0.00001f);
        float tmax = (bounds.Right - origin.X) / (dir.X != 0 ? dir.X : 0.00001f);
        Vector2 nmin = -Vector2.UnitX;
        Vector2 nmax = Vector2.UnitX;

        if (tmin > tmax)
        {
            (tmin, tmax) = (tmax, tmin);
            (nmin, nmax) = (nmax, nmin);
        }

        float tymin = (bounds.Top - origin.Y) / (dir.Y != 0 ? dir.Y : 0.00001f);
        float tymax = (bounds.Bottom - origin.Y) / (dir.Y != 0 ? dir.Y : 0.00001f);
        Vector2 nymin = -Vector2.UnitY;
        Vector2 nymax = Vector2.UnitY;

        if (tymin > tymax)
        {
            (tymin, tymax) = (tymax, tymin);
            (nymin, nymax) = (nymax, nymin);
        }

        if (tmin > tymax || tymin > tmax) return false;

        float tEnter = tmin;
        Vector2 nEnter = nmin;

        if (tymin > tmin)
        {
            tEnter = tymin;
            nEnter = nymin;
        }

        float tExit = Math.Min(tmax, tymax);
        if (tExit < 0f) return false;

        distance = tEnter > 0f ? tEnter : tExit;
        normal = nEnter;
        return true;
    }
}
