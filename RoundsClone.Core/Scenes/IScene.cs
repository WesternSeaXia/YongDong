using Microsoft.Xna.Framework.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 游戏场景生命周期接口。
/// 职责边界：规范所有游戏场景（主菜单、设置、选角色、选地图、战斗、选卡、结算）的进入、帧更新、绘制与退出生命周期方法。
/// </summary>
public interface IScene
{
    /// <summary>
    /// 进入场景时调用，执行场景资源或状态初始化。
    /// </summary>
    void Enter();

    /// <summary>
    /// 帧更新场景逻辑。
    /// </summary>
    /// <param name="dt">帧时间步长 (秒)</param>
    void Update(float dt);

    /// <summary>
    /// 渲染场景画面。
    /// </summary>
    /// <param name="spriteBatch">MonoGame 精灵批处理器</param>
    void Draw(SpriteBatch spriteBatch);

    /// <summary>
    /// 离开场景时调用，执行场景状态清理。
    /// </summary>
    void Exit();
}
