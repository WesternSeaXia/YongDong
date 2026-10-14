using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 基于分离轴定理 (SAT, Separating Axis Theorem) 的 2D 窄相碰撞与几何投影求解器。
/// 职责边界：专门负责圆形、AABB矩形与凸多边形之间的碰撞检测、法线计算、最小穿透深度判定以及射线相交解算。
/// </summary>
public static class SatCollision
{
    /// <summary>
    /// 圆形与凸多边形碰撞检测。
    /// </summary>
    public static bool TestCircleVsPolygon(CircleCollider circle, PolygonCollider poly, out CollisionManifold manifold)
    {
        manifold = default;
        Vector2 cPos = circle.Body.Position;
        float radius = circle.Radius;

        var vertices = poly.WorldVertices;
        int n = vertices.Length;
        if (n < 3) return false;

        // 快速 AABB 粗筛
        Rectangle b = poly.Bounds;
        if (cPos.X + radius < b.Left || cPos.X - radius > b.Right ||
            cPos.Y + radius < b.Top || cPos.Y - radius > b.Bottom)
        {
            return false;
        }

        float minOverlap = float.MaxValue;
        Vector2 smallestAxis = Vector2.Zero;

        // 1. 测试多边形各边法线轴
        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = vertices[i];
            Vector2 p2 = vertices[(i + 1) % n];
            Vector2 edge = p2 - p1;
            Vector2 axis = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));

            ProjectPolygon(vertices, axis, out float minP, out float maxP);
            ProjectCircle(cPos, radius, axis, out float minC, out float maxC);

            if (maxP < minC || maxC < minP)
            {
                return false; // 存在分离轴
            }

            float overlap = MathF.Min(maxP, maxC) - MathF.Max(minP, minC);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // 2. 找到距离圆心最近的多边形顶点，测试圆心到该顶点的轴
        float closestDistSq = float.MaxValue;
        Vector2 closestVertex = vertices[0];
        for (int i = 0; i < n; i++)
        {
            float distSq = Vector2.DistanceSquared(cPos, vertices[i]);
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestVertex = vertices[i];
            }
        }

        Vector2 vertexAxis = MathHelper2D.SafeNormalize(cPos - closestVertex);
        if (vertexAxis != Vector2.Zero)
        {
            ProjectPolygon(vertices, vertexAxis, out float minP, out float maxP);
            ProjectCircle(cPos, radius, vertexAxis, out float minC, out float maxC);

            if (maxP < minC || maxC < minP)
            {
                return false; // 存在分离轴
            }

            float overlap = MathF.Min(maxP, maxC) - MathF.Max(minP, minC);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = vertexAxis;
            }
        }

        // 确保法线从多边形指向圆 (从 B 指向 A)
        Vector2 toCircle = cPos - poly.Body.Position;
        if (Vector2.Dot(toCircle, smallestAxis) < 0f)
        {
            smallestAxis = -smallestAxis;
        }

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = circle,
            ColliderB = poly,
            Normal = smallestAxis,
            Penetration = minOverlap,
            ContactPoint = cPos - smallestAxis * radius
        };
        return true;
    }

    /// <summary>
    /// AABB 矩形与凸多边形碰撞检测。
    /// </summary>
    public static bool TestBoxVsPolygon(BoxCollider box, PolygonCollider poly, out CollisionManifold manifold)
    {
        manifold = default;
        var polyVerts = poly.WorldVertices;
        int pn = polyVerts.Length;
        if (pn < 3) return false;

        // AABB 粗筛
        Rectangle boxBounds = box.Bounds;
        Rectangle polyBounds = poly.Bounds;
        if (!boxBounds.Intersects(polyBounds))
        {
            return false;
        }

        // 构造 Box 的 4 个世界顶点
        Span<Vector2> boxVerts = stackalloc Vector2[4];
        Vector2 bPos = box.Body.Position;
        Vector2 half = box.HalfSize;
        boxVerts[0] = new Vector2(bPos.X - half.X, bPos.Y - half.Y);
        boxVerts[1] = new Vector2(bPos.X + half.X, bPos.Y - half.Y);
        boxVerts[2] = new Vector2(bPos.X + half.X, bPos.Y + half.Y);
        boxVerts[3] = new Vector2(bPos.X - half.X, bPos.Y + half.Y);

        float minOverlap = float.MaxValue;
        Vector2 smallestAxis = Vector2.Zero;

        // 1. Box 的两个分离轴 (X 轴与 Y 轴)
        Span<Vector2> boxAxes = stackalloc Vector2[2] { Vector2.UnitX, Vector2.UnitY };
        for (int i = 0; i < 2; i++)
        {
            Vector2 axis = boxAxes[i];
            ProjectPolygon(polyVerts, axis, out float minP, out float maxP);
            ProjectBoxFast(bPos, half, axis, out float minB, out float maxB);

            if (maxP < minB || maxB < minP) return false;

            float overlap = MathF.Min(maxP, maxB) - MathF.Max(minP, minB);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // 2. 多边形各边外向法线轴
        for (int i = 0; i < pn; i++)
        {
            Vector2 p1 = polyVerts[i];
            Vector2 p2 = polyVerts[(i + 1) % pn];
            Vector2 edge = p2 - p1;
            Vector2 axis = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));

            ProjectPolygon(polyVerts, axis, out float minP, out float maxP);
            ProjectBoxFast(bPos, half, axis, out float minB, out float maxB);

            if (maxP < minB || maxB < minP) return false;

            float overlap = MathF.Min(maxP, maxB) - MathF.Max(minP, minB);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // 确保法线从多边形指向矩形 (从 B 指向 A)
        Vector2 toBox = bPos - poly.Body.Position;
        if (Vector2.Dot(toBox, smallestAxis) < 0f)
        {
            smallestAxis = -smallestAxis;
        }

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = box,
            ColliderB = poly,
            Normal = smallestAxis,
            Penetration = minOverlap,
            ContactPoint = bPos - smallestAxis * (half.Length() * 0.5f)
        };
        return true;
    }

    /// <summary>
    /// 凸多边形与凸多边形碰撞检测。
    /// </summary>
    public static bool TestPolygonVsPolygon(PolygonCollider polyA, PolygonCollider polyB, out CollisionManifold manifold)
    {
        manifold = default;
        var vertsA = polyA.WorldVertices;
        var vertsB = polyB.WorldVertices;

        // AABB 粗筛
        if (!polyA.Bounds.Intersects(polyB.Bounds)) return false;

        float minOverlap = float.MaxValue;
        Vector2 smallestAxis = Vector2.Zero;

        // 1. 测试 A 的法线轴
        for (int i = 0; i < vertsA.Length; i++)
        {
            Vector2 p1 = vertsA[i];
            Vector2 p2 = vertsA[(i + 1) % vertsA.Length];
            Vector2 edge = p2 - p1;
            Vector2 axis = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));

            ProjectPolygon(vertsA, axis, out float minA, out float maxA);
            ProjectPolygon(vertsB, axis, out float minB, out float maxB);

            if (maxA < minB || maxB < minA) return false;

            float overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // 2. 测试 B 的法线轴
        for (int i = 0; i < vertsB.Length; i++)
        {
            Vector2 p1 = vertsB[i];
            Vector2 p2 = vertsB[(i + 1) % vertsB.Length];
            Vector2 edge = p2 - p1;
            Vector2 axis = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));

            ProjectPolygon(vertsA, axis, out float minA, out float maxA);
            ProjectPolygon(vertsB, axis, out float minB, out float maxB);

            if (maxA < minB || maxB < minA) return false;

            float overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // 确保法线从 B 指向 A
        Vector2 toA = polyA.Body.Position - polyB.Body.Position;
        if (Vector2.Dot(toA, smallestAxis) < 0f)
        {
            smallestAxis = -smallestAxis;
        }

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = polyA,
            ColliderB = polyB,
            Normal = smallestAxis,
            Penetration = minOverlap,
            ContactPoint = (polyA.Body.Position + polyB.Body.Position) * 0.5f
        };
        return true;
    }

    /// <summary>
    /// AABB 矩形与 AABB 矩形碰撞检测（用于带物理重力的箱子与平台或箱子间的碰撞）。
    /// </summary>
    public static bool TestBoxVsBox(BoxCollider a, BoxCollider b, out CollisionManifold manifold)
    {
        manifold = default;
        Vector2 posA = a.Body.Position;
        Vector2 posB = b.Body.Position;
        Vector2 halfA = a.HalfSize;
        Vector2 halfB = b.HalfSize;

        float dx = posA.X - posB.X;
        float px = (halfA.X + halfB.X) - MathF.Abs(dx);
        if (px <= 0f) return false;

        float dy = posA.Y - posB.Y;
        float py = (halfA.Y + halfB.Y) - MathF.Abs(dy);
        if (py <= 0f) return false;

        Vector2 normal;
        float penetration;

        if (px < py)
        {
            normal = new Vector2(dx > 0f ? 1f : -1f, 0f);
            penetration = px;
        }
        else
        {
            normal = new Vector2(0f, dy > 0f ? 1f : -1f);
            penetration = py;
        }

        manifold = new CollisionManifold
        {
            HasCollision = true,
            ColliderA = a,
            ColliderB = b,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = posB + normal * (normal.X != 0 ? halfB.X : halfB.Y)
        };
        return true;
    }

    /// <summary>
    /// 射线与凸多边形相交检测。
    /// </summary>
    public static bool RayIntersectPolygon(Vector2 origin, Vector2 dir, PolygonCollider poly, out float distance, out Vector2 normal)
    {
        distance = -1f;
        normal = Vector2.Zero;

        var verts = poly.WorldVertices;
        int n = verts.Length;
        if (n < 3) return false;

        float tNear = float.MinValue;
        float tFar = float.MaxValue;
        Vector2 nearNormal = Vector2.Zero;

        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = verts[i];
            Vector2 p2 = verts[(i + 1) % n];
            Vector2 edge = p2 - p1;
            Vector2 edgeNorm = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));

            float denominator = Vector2.Dot(edgeNorm, dir);
            float numerator = Vector2.Dot(edgeNorm, p1 - origin);

            if (MathF.Abs(denominator) < 0.00001f)
            {
                // 平行射线：如果在外部，则无法相交
                if (numerator < 0f) return false;
            }
            else
            {
                float t = numerator / denominator;
                if (denominator < 0f)
                {
                    // 射入半平面
                    if (t > tNear)
                    {
                        tNear = t;
                        nearNormal = edgeNorm;
                    }
                }
                else
                {
                    // 射出半平面
                    if (t < tFar)
                    {
                        tFar = t;
                    }
                }

                if (tNear > tFar) return false;
            }
        }

        if (tFar < 0f) return false;

        distance = tNear > 0f ? tNear : tFar;
        normal = nearNormal;
        return true;
    }

    /// <summary>
    /// 射线与圆形相交检测。
    /// </summary>
    public static bool RayIntersectCircle(Vector2 origin, Vector2 dir, CircleCollider circle, out float distance, out Vector2 normal)
    {
        distance = -1f;
        normal = Vector2.Zero;

        Vector2 m = origin - circle.Body.Position;
        float radiusSq = circle.Radius * circle.Radius;

        // 如果起点在圆内部（如角色自身中心发射探测射线），忽略自身相交
        if (m.LengthSquared() <= radiusSq) return false;

        float b = Vector2.Dot(m, dir);
        if (b > 0f) return false; // 圆在射线相反方向

        float c = Vector2.Dot(m, m) - radiusSq;
        float discr = b * b - c;
        if (discr < 0f) return false;

        float t = -b - MathF.Sqrt(discr);
        if (t < 0f) return false;

        distance = t;
        Vector2 hitPoint = origin + dir * t;
        normal = MathHelper2D.SafeNormalize(hitPoint - circle.Body.Position);
        return true;
    }

    private static void ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = Vector2.Dot(vertices[i], axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    private static void ProjectCircle(Vector2 center, float radius, Vector2 axis, out float min, out float max)
    {
        float c = Vector2.Dot(center, axis);
        min = c - radius;
        max = c + radius;
    }

    private static void ProjectBoxFast(Vector2 center, Vector2 half, Vector2 axis, out float min, out float max)
    {
        float c = Vector2.Dot(center, axis);
        float r = half.X * MathF.Abs(axis.X) + half.Y * MathF.Abs(axis.Y);
        min = c - r;
        max = c + r;
    }
}
