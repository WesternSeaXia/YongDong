using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地形组件通用抽象接口。
/// 职责边界：统一抽象矩形、圆形、多边形及可动地形，提供碰撞体暴露、生命值管理、破损视觉呈现与更新绘制约定。
/// </summary>
public interface ITerrainObject
{
    /// <summary>
    /// 地形对应的物理碰撞体。
    /// </summary>
    Collider Collider { get; }

    /// <summary>
    /// 物理刚体（支持静态锚定或受重力影响的动态刚体）。
    /// </summary>
    RigidBody2D Body { get; }

    /// <summary>
    /// 地形物理材质与视觉样式。
    /// </summary>
    TerrainStyle Style { get; set; }

    /// <summary>
    /// 是否为绝对静态地形（不可移动、不被重力驱动）。
    /// </summary>
    bool IsStatic { get; }

    /// <summary>
    /// 是否为可被破坏地形（承受子弹攻击将扣除生命值并破损）。
    /// </summary>
    bool IsDestructible { get; set; }

    /// <summary>
    /// 最大生命值。
    /// </summary>
    float MaxHealth { get; set; }

    /// <summary>
    /// 当前剩余生命值。
    /// </summary>
    float CurrentHealth { get; set; }

    /// <summary>
    /// 是否已彻底被损毁（损毁后自动从物理世界注销并不再参与碰撞与绘制）。
    /// </summary>
    bool IsDestroyed { get; }

    /// <summary>
    /// 绑定的绳索/吊链约束（若有）。
    /// </summary>
    RopeConstraint? AttachedRope { get; set; }

    /// <summary>
    /// 受到攻击时承受伤害，产生碎屑并检测是否爆裂摧毁。
    /// </summary>
    void TakeDamage(float damage, ParticleSystem particles, ScreenShake shake);

    /// <summary>
    /// 物理帧逻辑更新（处理运动平台轨迹、受损裂纹插值等）。
    /// </summary>
    void Update(float dt);

    /// <summary>
    /// 绘制地形实体（独立绘制单体，或在合批轮廓模式下绘制特异视觉）。
    /// </summary>
    void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch);
}
