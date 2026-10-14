using System;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 创伤衰减屏幕震屏系统 (Trauma-based Screen Shake)。
/// 职责边界：管理战局打击与爆炸产生的适度屏幕抖动，经过非线性平方阻尼衰减，避免过度摇晃导致眩晕。
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
            // 创伤适度加速衰减，快速恢复平稳
            _trauma = Math.Max(0f, _trauma - dt * 2.2f);

            // 非线性平方响应：低创伤几乎无感，高爆破沉稳有力
            float shake = _trauma * _trauma;
            float maxOffset = 13f; // 由原本 28f 大幅降低，消除剧烈眩晕感
            float maxAngle = 0.015f; // 由 0.045f 降低，保持视野水平稳定

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
