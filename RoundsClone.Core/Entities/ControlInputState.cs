using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色统一输入控制意图状态。
/// 解耦顶层输入源（Windows 键鼠、Android 虚拟摇杆触控、AI 战术决策大脑）与底层角色物理执行器。
/// </summary>
public struct ControlInputState
{
    /// <summary>
    /// 水平移动力矢量意图（X 分量范围 -1.0 到 1.0）。
    /// </summary>
    public Vector2 MoveVector;

    /// <summary>
    /// 归一化的瞄准方向单位矢量。
    /// </summary>
    public Vector2 AimVector;

    /// <summary>
    /// 主武器开火扳机是否处于按下状态。
    /// </summary>
    public bool IsFireDown;

    /// <summary>
    /// 跳跃指令是否处于按下状态。
    /// </summary>
    public bool IsJumpDown;

    /// <summary>
    /// 副武器（格挡/冲刺）指令是否处于按下状态。
    /// </summary>
    public bool IsSecondaryDown;
}
