using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.SubWeapons;

/// <summary>
/// 副武器/战术技能插件接口。
/// 替代传统单一格挡键，支持可插拔、可强化、可定制的副武器槽位体系。
/// </summary>
public interface ISubWeapon
{
    /// <summary>
    /// 副武器唯一标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 显示名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 技能特性简短描述。
    /// </summary>
    string Description { get; }

    /// <summary>
    /// UI 矢量图标类型。
    /// </summary>
    IconType Icon { get; }

    /// <summary>
    /// 基础总冷却时长 (秒)。
    /// </summary>
    float Cooldown { get; set; }

    /// <summary>
    /// 剩余冷却时间 (秒)。小于等于 0 时代表技能就绪。
    /// </summary>
    float CurrentCooldown { get; set; }

    /// <summary>
    /// 技能是否已冷却完毕。
    /// </summary>
    bool IsReady => CurrentCooldown <= 0f;

    /// <summary>
    /// 技能释放后的持续生效时长 (秒)。
    /// </summary>
    float ActiveDuration { get; }

    /// <summary>
    /// 技能当前是否处于激活维持状态。
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 尝试激发副武器能力。
    /// </summary>
    /// <param name="user">释放者实体</param>
    /// <param name="direction">当前瞄准或移动输入方向</param>
    /// <returns>若成功激发返回 true，冷却中则返回 false</returns>
    bool TryActivate(Actor user, Vector2 direction);

    /// <summary>
    /// 帧更新冷却与生效维持逻辑。
    /// </summary>
    /// <param name="dt">时间步长</param>
    void Update(float dt);

    /// <summary>
    /// 程序化渲染副武器的附加视觉效果 (如格挡护盾圈、充能指示器等)。
    /// </summary>
    void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, Actor user);
}
