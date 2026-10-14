using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 绳索/吊链物理约束。
/// 职责边界：连接刚体与世界固定挂点（或两个刚体），约束最大拉伸距离，提供逼真的钟摆摆动与吊装物理动力学。
/// </summary>
public class RopeConstraint
{
    /// <summary>
    /// 世界固定挂点坐标。
    /// </summary>
    public Vector2 WorldAnchor { get; set; }

    /// <summary>
    /// 连接的动态物理刚体。
    /// </summary>
    public RigidBody2D Body { get; }

    /// <summary>
    /// 绳子在刚体上的局部系挂锚点（默认为刚体顶部中心）。
    /// </summary>
    public Vector2 LocalAnchor { get; set; }

    /// <summary>
    /// 绳索最大伸长长度（像素）。
    /// </summary>
    public float Length { get; set; }

    /// <summary>
    /// 刚度阻尼系数 (0.0 到 1.0)。
    /// </summary>
    public float Stiffness { get; set; } = 1.0f;

    /// <summary>
    /// 构造单端挂靠在世界固定点的绳索约束。
    /// </summary>
    public RopeConstraint(Vector2 worldAnchor, RigidBody2D body, Vector2 localAnchor, float length)
    {
        WorldAnchor = worldAnchor;
        Body = body;
        LocalAnchor = localAnchor;
        Length = MathF.Max(length, 10f);
    }

    /// <summary>
    /// 获取当前刚体端挂点的世界坐标。
    /// </summary>
    public Vector2 GetBodyAnchorWorldPos() => Body.Position + LocalAnchor;

    /// <summary>
    /// 求解绳索距离约束并修正刚体位置与速度。
    /// </summary>
    public void Solve(float dt)
    {
        if (Body.IsStatic) return;

        Vector2 bodyAnchor = GetBodyAnchorWorldPos();
        Vector2 delta = bodyAnchor - WorldAnchor;
        float currentDist = delta.Length();

        if (currentDist > Length && currentDist > 0.001f)
        {
            Vector2 ropeDir = delta / currentDist;
            float excess = currentDist - Length;

            // 1. 位置穿透强制校正 (将刚体沿绳子拉回球面)
            Body.Position -= ropeDir * (excess * Stiffness);

            // 2. 速度法向投影消解 (消除沿拉伸方向的速度分量，保留切向角动量摆动)
            float velAlongRope = Vector2.Dot(Body.Velocity, ropeDir);
            if (velAlongRope > 0f)
            {
                Body.Velocity -= ropeDir * velAlongRope;
            }
        }
    }
}
