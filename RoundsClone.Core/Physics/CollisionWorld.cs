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
                col.Body.WallBody = null;
                col.Body.IsOnBouncySurface = false;
                col.Body.GroundVelocity = Vector2.Zero;
                col.Body.GroundBody = null;
                col.Body.GroundNormal = -Vector2.UnitY;
                col.Body.Integrate(dt, Gravity);
            }
        }

        // 步骤 1.5: 求解绳索与吊链的长度拉伸约束
        for (int r = 0; r < _ropes.Count; r++)
        {
            _ropes[r].Solve(dt);
        }

        // 步骤 2: 遍历所有碰撞对并进行窄相与多轮接触求解 (3 轮迭代使多层堆叠箱体的受力与位置排斥完美传递收敛)
        const int SolverIterations = 3;
        for (int iter = 0; iter < SolverIterations; iter++)
        {
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
                        // 派发接触通知 (仅在第 0 轮迭代触发，避免逻辑事件多次触发)
                        if (iter == 0)
                        {
                            onCollision?.Invoke(manifold);
                        }

                        // 若物体在碰撞事件中被禁用（例如子弹被销毁），跳过后续物理冲量解算
                        if (!a.IsEnabled || !b.IsEnabled) continue;

                        // 非触发器则进行刚体反弹冲量与位置排斥解算
                        if (!a.IsTrigger && !b.IsTrigger)
                        {
                            ResolveCollision(ref manifold, dt, iter);
                        }
                    }
                }
            }
        }

        // 步骤 3: 碰撞冲量解算完成后再次修正绳索约束，杜绝碰撞把箱子砸出绳长
        for (int r = 0; r < _ropes.Count; r++)
        {
            _ropes[r].Solve(dt);
        }

        // 步骤 4: 竞技场全局硬边界约束，杜绝任何实体在极限冲击/反弹下穿透外墙飞出地图
        for (int i = 0; i < _colliders.Count; i++)
        {
            var col = _colliders[i];
            if (!col.IsEnabled || col.Body == null || col.Body.IsStatic || col.IsProjectile) continue;

            float halfW = col is BoxCollider box ? box.HalfSize.X : (col is CircleCollider circle ? circle.Radius : 25f);
            float halfH = col is BoxCollider box2 ? box2.HalfSize.Y : (col is CircleCollider circle2 ? circle2.Radius : 25f);

            // 竞技场外墙内边缘为 [60, 60] 到 [1860, 1020]
            float minX = 60f + halfW;
            float maxX = 1860f - halfW;
            float minY = 60f + halfH;
            float maxY = 1020f - halfH;

            var pos = col.Body.Position;
            var vel = col.Body.Velocity;

            if (pos.X < minX)
            {
                pos.X = minX;
                if (vel.X < 0f) vel.X = -vel.X * 0.4f;
            }
            else if (pos.X > maxX)
            {
                pos.X = maxX;
                if (vel.X > 0f) vel.X = -vel.X * 0.4f;
            }

            if (pos.Y < minY)
            {
                pos.Y = minY;
                if (vel.Y < 0f) vel.Y = -vel.Y * 0.4f;
            }
            else if (pos.Y > maxY)
            {
                pos.Y = maxY;
                if (vel.Y > 0f) vel.Y = -vel.Y * 0.4f;
            }

            col.Body.Position = pos;
            col.Body.Velocity = vel;
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

    private void ResolveCollision(ref CollisionManifold m, float dt, int iter)
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
                bodyA.GroundNormal = m.Normal;

                // 站在移动平台上时，直接跟随平台位移平滑移动 (仅在第 0 轮迭代位移一次，避免多轮迭代重复累加超速)
                if (iter == 0 && bodyB.Velocity != Vector2.Zero)
                {
                    bodyA.Position += bodyB.Velocity * dt;
                }
            }
            else if (MathF.Abs(m.Normal.X) > 0.6f && MathF.Abs(m.Normal.Y) < 0.6f && (bodyB.IsStatic || bodyB.Mass >= 1.5f))
            {
                bodyA.IsTouchingWall = true;
                bodyA.WallNormal = new Vector2(MathF.Sign(m.Normal.X), 0f);
                bodyA.WallBody = bodyB;
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
                bodyB.GroundNormal = -m.Normal;

                if (iter == 0 && bodyA.Velocity != Vector2.Zero)
                {
                    bodyB.Position += bodyA.Velocity * dt;
                }
            }
            else if (MathF.Abs(m.Normal.X) > 0.6f && MathF.Abs(m.Normal.Y) < 0.6f && (bodyA.IsStatic || bodyA.Mass >= 1.5f))
            {
                bodyB.IsTouchingWall = true;
                bodyB.WallNormal = new Vector2(-MathF.Sign(m.Normal.X), 0f);
                bodyB.WallBody = bodyA;
            }
        }

        // 计算相对速度与沿碰撞法线的分量
        Vector2 relVel = bodyA.Velocity - bodyB.Velocity;
        float velAlongNormal = Vector2.Dot(relVel, m.Normal);

        // 计算牛顿恢复冲量大小
        float totalInvMass = bodyA.InverseMass + bodyB.InverseMass;
        if (totalInvMass > 0.0001f)
        {
            // 仅在第 0 轮迭代且相向运动时施加弹性冲量，避免多轮迭代重复累加导致速度暴增起飞
            if (iter == 0 && velAlongNormal < 0f)
            {
                bool isSlopeGripA = !bodyA.IsStatic && bodyA.SlopeGrip > 0f && !bodyA.IsOnBouncySurface && bodyB.IsStatic && m.Normal.Y < -0.6f && MathF.Abs(m.Normal.X) > 0.05f;
                bool isSlopeGripB = !bodyB.IsStatic && bodyB.SlopeGrip > 0f && !bodyB.IsOnBouncySurface && bodyA.IsStatic && m.Normal.Y > 0.6f && MathF.Abs(m.Normal.X) > 0.05f;

                if (isSlopeGripA)
                {
                    // 角色在斜坡着陆/站立：将冲量改为垂直承托，杜绝斜向法线冲量将角色甩向坡下
                    float j = -(1f + restitution) * velAlongNormal / totalInvMass;
                    bodyA.Velocity += new Vector2(0f, j * m.Normal.Y * bodyA.InverseMass);
                    if (bodyA.Velocity.Y > 0f) bodyA.Velocity = new Vector2(bodyA.Velocity.X, 0f);
                }
                else if (isSlopeGripB)
                {
                    float j = -(1f + restitution) * velAlongNormal / totalInvMass;
                    bodyB.Velocity -= new Vector2(0f, j * m.Normal.Y * bodyB.InverseMass);
                    if (bodyB.Velocity.Y > 0f) bodyB.Velocity = new Vector2(bodyB.Velocity.X, 0f);
                }
                else
                {
                    float j = -(1f + restitution) * velAlongNormal / totalInvMass;
                    Vector2 impulse = j * m.Normal;

                    bodyA.ApplyImpulse(impulse);
                    bodyB.ApplyImpulse(-impulse);

                    // 弹性限制：避免反弹能量叠加越弹越快导致穿墙
                    if (restitution >= 0.5f)
                    {
                        if (bodyA.Velocity.LengthSquared() > 1350f * 1350f)
                        {
                            bodyA.Velocity = Vector2.Normalize(bodyA.Velocity) * 1350f;
                        }
                        if (bodyB.Velocity.LengthSquared() > 1350f * 1350f)
                        {
                            bodyB.Velocity = Vector2.Normalize(bodyB.Velocity) * 1350f;
                        }
                    }
                }
            }

            // 位置穿透纠偏 (Baumgarte 稳定算法，每轮迭代均推开多层箱体，消除堆叠镶嵌陷入！)
            float percent = 0.5f;
            float slop = 0.02f;
            float correctionMagnitude = MathF.Max(m.Penetration - slop, 0f) / totalInvMass * percent;
            // 单次迭代纠偏位移上限（防止深层穿透时产生爆炸式瞬间位移飞出地图）
            correctionMagnitude = MathF.Min(correctionMagnitude, 15f);

            if (correctionMagnitude > 0f)
            {
                bool isSlopeGripA = !bodyA.IsStatic && bodyA.SlopeGrip > 0f && !bodyA.IsOnBouncySurface && bodyB.IsStatic && m.Normal.Y < -0.6f && MathF.Abs(m.Normal.X) > 0.05f;
                bool isSlopeGripB = !bodyB.IsStatic && bodyB.SlopeGrip > 0f && !bodyB.IsOnBouncySurface && bodyA.IsStatic && m.Normal.Y > 0.6f && MathF.Abs(m.Normal.X) > 0.05f;

                if (isSlopeGripA)
                {
                    // 斜坡角色垂直托起，彻底避免横向向坡下排挤位移
                    float verticalOffset = -correctionMagnitude / MathF.Max(0.2f, MathF.Abs(m.Normal.Y));
                    bodyA.Position += new Vector2(0f, verticalOffset * bodyA.InverseMass);
                }
                else if (isSlopeGripB)
                {
                    float verticalOffset = -correctionMagnitude / MathF.Max(0.2f, MathF.Abs(m.Normal.Y));
                    bodyB.Position += new Vector2(0f, verticalOffset * bodyB.InverseMass);
                }
                else
                {
                    Vector2 correction = correctionMagnitude * m.Normal;
                    if (!bodyA.IsStatic) bodyA.Position += correction * bodyA.InverseMass;
                    if (!bodyB.IsStatic) bodyB.Position -= correction * bodyB.InverseMass;
                }
            }
        }
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
