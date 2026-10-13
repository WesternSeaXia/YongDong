using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 战斗地图实例数据与物理碰撞绑定模型。
/// 职责边界：存储单张地图的几何墙体、反弹柱及出生点数据，并负责向物理世界 (<see cref="CollisionWorld"/>) 注册或卸载静态碰撞体；
/// 视觉绘制委托给 <see cref="MapRenderer"/>。
/// </summary>
public class MapData
{
    public string Id { get; set; } = "map_default";
    public string Name { get; set; } = "经典竞技场";
    public string Description { get; set; } = "平衡对称的对抗地图，拥有中央浮岛与侧翼掩体。";
    public string EnvironmentalModifier { get; set; } = "标准物理环境";

    public Vector2 PlayerSpawn { get; set; } = new(350f, 750f);
    public Vector2 AiSpawn { get; set; } = new(1570f, 750f);

    /// <summary>
    /// 普通刚性实体墙矩形集合。
    /// </summary>
    public List<Rectangle> SolidWalls { get; } = new();

    /// <summary>
    /// 高弹性反弹柱/蹦床矩形集合。
    /// </summary>
    public List<Rectangle> BouncyPillars { get; } = new();

    private readonly List<BoxCollider> _registeredColliders = new();

    /// <summary>
    /// 将本张地图的静态墙体与高弹柱构建为碰撞体并注入物理世界。
    /// </summary>
    public void BuildPhysicsWorld(CollisionWorld world)
    {
        _registeredColliders.Clear();

        foreach (var rect in SolidWalls)
        {
            var body = new RigidBody2D(new Vector2(rect.Center.X, rect.Center.Y), 0f)
            {
                Restitution = 0.2f
            };
            var collider = new BoxCollider(body, new Vector2(rect.Width, rect.Height))
            {
                BouncinessOverride = 0.2f
            };
            world.AddCollider(collider);
            _registeredColliders.Add(collider);
        }

        foreach (var rect in BouncyPillars)
        {
            var body = new RigidBody2D(new Vector2(rect.Center.X, rect.Center.Y), 0f)
            {
                Restitution = 1.0f
            };
            var collider = new BoxCollider(body, new Vector2(rect.Width, rect.Height))
            {
                BouncinessOverride = 1.0f
            };
            world.AddCollider(collider);
            _registeredColliders.Add(collider);
        }
    }

    /// <summary>
    /// 从物理世界中清理并卸载本张地图注册的所有静态碰撞体。
    /// </summary>
    public void CleanPhysicsWorld(CollisionWorld world)
    {
        foreach (var col in _registeredColliders)
        {
            world.RemoveCollider(col);
        }
        _registeredColliders.Clear();
    }

    /// <summary>
    /// 渲染地图战场（委托给 <see cref="MapRenderer"/>）。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        MapRenderer.Draw(this, batcher, spriteBatch);
    }

    /// <summary>
    /// 渲染地图缩略图（委托给 <see cref="MapRenderer"/>）。
    /// </summary>
    public void DrawThumbnail(ShapeBatcher batcher, SpriteBatch spriteBatch, Rectangle destRect)
    {
        MapRenderer.DrawThumbnail(this, batcher, spriteBatch, destRect);
    }
}
