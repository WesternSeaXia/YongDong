using System;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 创伤衰减屏幕震屏系统 (Trauma-based Screen Shake)。
/// 物理学非线性平方响应模型，呈现强烈的爆破与受击打击感。
/// </summary>
public class ScreenShake
{
    private float _trauma = 0f;
    private readonly Random _random = new();

    /// <summary>
    /// 当前震屏产生的 X 轴虚拟偏移量。
    /// </summary>
    public float OffsetX { get; private set; }

    /// <summary>
    /// 当前震屏产生的 Y 轴虚拟偏移量。
    /// </summary>
    public float OffsetY { get; private set; }

    /// <summary>
    /// 当前震屏产生的轻微旋转角 (弧度)。
    /// </summary>
    public float Angle { get; private set; }

    /// <summary>
    /// 增加震屏创伤能量。
    /// </summary>
    /// <param name="amount">累积值 (0.0 到 1.0)</param>
    public void AddTrauma(float amount)
    {
        _trauma = Math.Clamp(_trauma + amount, 0f, 1.0f);
    }

    /// <summary>
    /// 帧更新，衰减创伤能量并计算当前抖动向量。
    /// </summary>
    /// <param name="dt">时间步长 (秒)</param>
    public void Update(float dt)
    {
        if (_trauma > 0f)
        {
            // 创伤线性衰减
            _trauma = Math.Max(0f, _trauma - dt * 1.5f);

            // 非线性平方响应：低震动平缓，高创伤剧烈
            float shake = _trauma * _trauma;
            float maxOffset = 28f;
            float maxAngle = 0.045f;

            OffsetX = (float)(_random.NextDouble() * 2.0 - 1.0) * maxOffset * shake;
            OffsetY = (float)(_random.NextDouble() * 2.0 - 1.0) * maxOffset * shake;
            Angle = (float)(_random.NextDouble() * 2.0 - 1.0) * maxAngle * shake;
        }
        else
        {
            OffsetX = 0f;
            OffsetY = 0f;
            Angle = 0f;
        }
    }

    /// <summary>
    /// 获取当前震屏位移向量。
    /// </summary>
    public Vector2 GetOffset() => new(OffsetX, OffsetY);
}
