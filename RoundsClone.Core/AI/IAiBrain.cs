using System.Collections.Generic;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.AI;

/// <summary>
/// AI 行为大脑决策接口。
/// 职责边界：定义 AI 智能体的决策输入与控制输出契约，解耦 AI 算法与具体的角色实体 (<see cref="Actor"/>)，支持插拔不同战术风格的 AI。
/// </summary>
public interface IAiBrain
{
    /// <summary>
    /// AI 风格标识名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 思考并生成当前帧的控制意图输入。
    /// </summary>
    /// <param name="self">AI 自身实体</param>
    /// <param name="target">对抗目标实体 (玩家)</param>
    /// <param name="projectiles">全场活跃子弹只读列表</param>
    /// <param name="world">物理碰撞世界</param>
    /// <param name="dt">帧时间步长 (秒)</param>
    /// <returns>计算出的统一控制操作意图</returns>
    ControlInputState Think(Actor self, Actor target, IReadOnlyList<Projectile> projectiles, CollisionWorld world, float dt);
}
