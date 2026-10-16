using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 2D 凸多边形物理碰撞体（支持三角形斜坡、梯形高台、菱形折射块及多边形浮空岛）。
/// 职责边界：存储多边形局部顶点拓扑，根据绑定的刚体实时计算世界顶点与包围盒，并提供法线集合用于分离轴定理 (SAT) 窄相碰撞解算。
/// </summary>
public class PolygonCollider : Collider
{
    /// <inheritdoc />
    public override ColliderType Type => ColliderType.Polygon;

    /// <summary>
    /// 相对于刚体中心的局部凸多边形顶点列表（逆时针或顺时针连续闭合）。
    /// </summary>
    public Vector2[] LocalVertices { get; }

    private readonly Vector2[] _worldVertices;
    private readonly Vector2[] _edgeNormals;
    private Vector2 _cachedBodyPos = new(float.MinValue, float.MinValue);
    private Rectangle _cachedBounds;

    /// <summary>
    /// 获取当前帧在世界空间中的多边形顶点只读数组。
    /// </summary>
    public ReadOnlySpan<Vector2> WorldVertices
    {
        get
        {
            UpdateWorldGeometry();
            return _worldVertices;
        }
    }

    /// <summary>
    /// 获取当前多边形各边的归一化法线数组（用于分离轴测试）。
    /// </summary>
    public ReadOnlySpan<Vector2> EdgeNormals
    {
        get
        {
            UpdateWorldGeometry();
            return _edgeNormals;
        }
    }

    /// <summary>
    /// 当前多边形在世界空间中的轴对齐包围盒 (AABB)。
    /// </summary>
    public override Rectangle Bounds
    {
        get
        {
            UpdateWorldGeometry();
            return _cachedBounds;
        }
    }

    /// <summary>
    /// 构造凸多边形碰撞体。
    /// </summary>
    /// <param name="body">绑定的物理刚体</param>
    /// <param name="localVertices">相对于刚体中心的多边形顶点集合（至少 3 个点）</param>
    public PolygonCollider(RigidBody2D body, Vector2[] localVertices) : base(body)
    {
        if (localVertices == null || localVertices.Length < 3)
        {
            throw new ArgumentException("多边形碰撞体必须包含至少 3 个顶点。", nameof(localVertices));
        }

        LocalVertices = localVertices;
        _worldVertices = new Vector2[localVertices.Length];
        _edgeNormals = new Vector2[localVertices.Length];

        UpdateWorldGeometry(force: true);
    }

    /// <summary>
    /// 从世界顶点快速构建多边形碰撞体（自动计算几何中心作为刚体坐标）。
    /// </summary>
    public static PolygonCollider CreateFromWorldVertices(Vector2[] worldPoints, bool isStatic = true, float mass = 0f)
    {
        // 计算几何中心质心
        Vector2 center = Vector2.Zero;
        for (int i = 0; i < worldPoints.Length; i++)
        {
            center += worldPoints[i];
        }
        center /= worldPoints.Length;

        var body = new RigidBody2D(center, isStatic ? 0f : (mass > 0f ? mass : 10f));
        Vector2[] localPoints = new Vector2[worldPoints.Length];
        for (int i = 0; i < worldPoints.Length; i++)
        {
            localPoints[i] = worldPoints[i] - center;
        }

        return new PolygonCollider(body, localPoints);
    }

    private void UpdateWorldGeometry(bool force = false)
    {
        if (!force && _cachedBodyPos == Body.Position)
        {
            return;
        }

        _cachedBodyPos = Body.Position;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        int n = LocalVertices.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 wv = Body.Position + LocalVertices[i];
            _worldVertices[i] = wv;

            if (wv.X < minX) minX = wv.X;
            if (wv.Y < minY) minY = wv.Y;
            if (wv.X > maxX) maxX = wv.X;
            if (wv.Y > maxY) maxY = wv.Y;
        }

        _cachedBounds = new Rectangle(
            (int)MathF.Floor(minX),
            (int)MathF.Floor(minY),
            (int)MathF.Ceiling(maxX - minX),
            (int)MathF.Ceiling(maxY - minY)
        );

        // 计算世界边的垂直外向法线
        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = _worldVertices[i];
            Vector2 p2 = _worldVertices[(i + 1) % n];
            Vector2 edge = p2 - p1;
            Vector2 normal = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));
            _edgeNormals[i] = normal;
        }
    }
}
