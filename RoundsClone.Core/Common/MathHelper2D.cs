using System;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Common;

/// <summary>
/// 二维向量与数学计算辅助工具类，提供物理模拟、角度解算、阻尼插值等常用算法。
/// </summary>
public static class MathHelper2D
{
    /// <summary>
    /// 将角度转换为归一化方向向量。
    /// </summary>
    /// <param name="radians">弧度角</param>
    /// <returns>二维单位方向向量</returns>
    public static Vector2 FromAngle(float radians)
    {
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    /// <summary>
    /// 获取向量对应的弧度角 (-π 到 π)。
    /// </summary>
    /// <param name="vector">二维向量</param>
    /// <returns>弧度角</returns>
    public static float ToAngle(Vector2 vector)
    {
        return MathF.Atan2(vector.Y, vector.X);
    }

    /// <summary>
    /// 安全地归一化向量，避免因零长度产生除以零异常 (NaN)。
    /// </summary>
    /// <param name="vector">原始向量</param>
    /// <returns>归一化后的单位向量；若为零向量则返回 Vector2.Zero</returns>
    public static Vector2 SafeNormalize(Vector2 vector)
    {
        float length = vector.Length();
        return length > 0.00001f ? vector / length : Vector2.Zero;
    }

    /// <summary>
    /// 限制向量的最大长度。
    /// </summary>
    /// <param name="vector">输入向量</param>
    /// <param name="maxLength">最大允许模长</param>
    /// <returns>截断模长后的向量</returns>
    public static Vector2 ClampLength(Vector2 vector, float maxLength)
    {
        float lengthSq = vector.LengthSquared();
        if (lengthSq > maxLength * maxLength && lengthSq > 0.00001f)
        {
            return vector * (maxLength / MathF.Sqrt(lengthSq));
        }
        return vector;
    }

    /// <summary>
    /// 依据表面法线计算入射矢量的完美反射矢量。
    /// 公式: R = V - 2 * (V · N) * N
    /// </summary>
    /// <param name="velocity">入射速度矢量</param>
    /// <param name="normal">碰撞接触面单位法线</param>
    /// <returns>反射后的速度矢量</returns>
    public static Vector2 Reflect(Vector2 velocity, Vector2 normal)
    {
        float dot = Vector2.Dot(velocity, normal);
        return velocity - 2f * dot * normal;
    }

    /// <summary>
    /// 弹簧阻尼模型步进更新（常用于形变、摄像机平滑、摇晃回复）。
    /// </summary>
    /// <param name="current">当前值</param>
    /// <param name="target">目标平衡值</param>
    /// <param name="velocity">当前变化速度（将被更新）</param>
    /// <param name="stiffness">弹簧刚度系数 (k)</param>
    /// <param name="damping">阻尼系数 (c)</param>
    /// <param name="deltaTime">时间步长 (秒)</param>
    /// <returns>更新后的当前值</returns>
    public static float SpringDamp(float current, float target, ref float velocity, float stiffness, float damping, float deltaTime)
    {
        float displacement = current - target;
        float force = -stiffness * displacement - damping * velocity;
        velocity += force * deltaTime;
        return current + velocity * deltaTime;
    }

    /// <summary>
    /// 二维弹簧阻尼步进更新。
    /// </summary>
    public static Vector2 SpringDamp2D(Vector2 current, Vector2 target, ref Vector2 velocity, float stiffness, float damping, float deltaTime)
    {
        float vx = velocity.X;
        float vy = velocity.Y;
        float x = SpringDamp(current.X, target.X, ref vx, stiffness, damping, deltaTime);
        float y = SpringDamp(current.Y, target.Y, ref vy, stiffness, damping, deltaTime);
        velocity = new Vector2(vx, vy);
        return new Vector2(x, y);
    }
}
