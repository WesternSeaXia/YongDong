using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Physics;

/// <summary>
/// 基于二维虎克定律与阻尼弹簧的角色物理形变模型 (Squash & Stretch)。
/// 在受击、着地冲击或发射后坐力时产生沿力轴向的体积压缩与垂直轴向的拉伸震荡，营造极具弹性质感的动画表现。
/// </summary>
public class SpringSquashStretch
{
    private float _axisCompression = 0f;       // 主变形轴压缩量
    private float _compressionVelocity = 0f;   // 变形速度
    private float _deformationAngle = 0f;      // 当前主变形轴旋转角
    private float _walkBounceOffset = 0f;      // 行走与呼吸附加的周期性果冻Q弹偏移

    /// <summary>
    /// 弹簧刚度系数 (适中的刚度使果冻形变周期清晰可见)。
    /// </summary>
    public float Stiffness { get; set; } = 185f;

    /// <summary>
    /// 阻尼吸收系数 (低阻尼让角色受激后产生 3~4 次明显的果冻回弹震荡)。
    /// </summary>
    public float Damping { get; set; } = 6.2f;

    /// <summary>
    /// 获取当前形变轴向与垂直轴向的缩放比 (X: 变形轴, Y: 垂直轴)。
    /// 依据体积守恒原则，沿轴向压缩时垂直轴向等量膨胀。
    /// </summary>
    public Vector2 CurrentScale
    {
        get
        {
            float totalComp = MathHelper.Clamp(_axisCompression + _walkBounceOffset, -0.62f, 0.62f);
            float axisScale = 1.0f - totalComp;
            float perpScale = 1.0f + totalComp * 0.92f;
            return new Vector2(axisScale, perpScale);
        }
    }

    /// <summary>
    /// 获取当前主变形轴在世界坐标系下的旋转弧度。
    /// </summary>
    public float DeformationAngle => _deformationAngle;

    /// <summary>
    /// 沿指定方向施加拉伸冲量（如跳跃腾空、蹬墙跳、高速冲刺）。
    /// 角色将沿运动方向显著伸长，并在垂直方向收窄，随后产生Q弹回振。
    /// </summary>
    /// <param name="direction">拉伸主方向矢量</param>
    /// <param name="intensity">拉伸强度 (0.0 到 0.6)</param>
    public void ApplyStretch(Vector2 direction, float intensity)
    {
        if (direction.LengthSquared() > 0.0001f)
        {
            _deformationAngle = MathF.Atan2(direction.Y, direction.X);
        }
        _axisCompression = MathHelper.Clamp(_axisCompression - intensity * 0.42f, -0.60f, 0.60f);
        _compressionVelocity -= intensity * 19f;
    }

    /// <summary>
    /// 沿指定方向施加压扁冲量（如着陆撞地、枪械后坐反冲、受击冲击）。
    /// 角色将沿撞击方向显著压扁，并在垂直方向膨胀，随后产生Q弹回振。
    /// </summary>
    /// <param name="direction">受力法线或后坐方向矢量</param>
    /// <param name="intensity">压扁强度 (0.0 到 0.6)</param>
    public void ApplySquash(Vector2 direction, float intensity)
    {
        if (direction.LengthSquared() > 0.0001f)
        {
            _deformationAngle = MathF.Atan2(direction.Y, direction.X);
        }
        _axisCompression = MathHelper.Clamp(_axisCompression + intensity * 0.42f, -0.60f, 0.60f);
        _compressionVelocity += intensity * 19f;
    }

    /// <summary>
    /// 设置行走步态或呼吸产生的连续果冻律动形变。
    /// </summary>
    /// <param name="verticalCompression">垂直方向的压缩/拉伸偏移量</param>
    public void SetContinuousWobble(float verticalCompression)
    {
        _walkBounceOffset = verticalCompression;
        // 当主弹簧近似静止时，将变形轴平滑对齐至垂直轴向以表现步行Q弹颠簸
        if (MathF.Abs(_axisCompression) < 0.08f && MathF.Abs(_compressionVelocity) < 1.5f)
        {
            _deformationAngle = MathHelper.PiOver2;
        }
    }

    /// <summary>
    /// 兼容旧接口：向弹簧施加压扁形变冲量。
    /// </summary>
    public void ApplyImpulse(Vector2 direction, float intensity)
    {
        ApplySquash(direction, intensity);
    }

    /// <summary>
    /// 帧更新阻尼弹簧运动方程。
    /// </summary>
    /// <param name="dt">时间步长 (秒)</param>
    public void Update(float dt)
    {
        // 虎克定律回弹: F = -k * x - c * v
        _axisCompression = MathHelper2D.SpringDamp(
            _axisCompression,
            0f,
            ref _compressionVelocity,
            Stiffness,
            Damping,
            dt
        );

        // 保护限制最大形变范围，避免极端数值扭曲
        _axisCompression = MathHelper.Clamp(_axisCompression, -0.62f, 0.62f);
    }
}
