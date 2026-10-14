using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 战斗地图实例数据与物理碰撞绑定模型。
/// 职责边界：统一管理地图中所有的矩形、圆形、多边形、动态物理与运动平台组件；
/// 负责向物理世界 (<see cref="CollisionWorld"/>) 注入与卸载碰撞体及绳索约束，视觉绘制委托给 <see cref="MapRenderer"/>。
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
    /// 地图中所有的丰富地形组件（包含多边形、圆形、动力学箱、运动平台与可破坏物）。
    /// </summary>
    public List<ITerrainObject> TerrainObjects { get; } = new();

    /// <summary>
    /// 兼容旧版矩形实体墙（自动转换为 BoxTerrain）。
    /// </summary>
    public List<Rectangle> SolidWalls { get; } = new();

    /// <summary>
    /// 兼容旧版高弹性反弹柱（自动转换为 BouncyRubber BoxTerrain）。
    /// </summary>
    public List<Rectangle> BouncyPillars { get; } = new();

    private readonly List<Collider> _registeredColliders = new();
    private readonly List<RopeConstraint> _registeredRopes = new();

    /// <summary>
    /// 添加一个地形组件。
    /// </summary>
    public T AddTerrain<T>(T terrain) where T : ITerrainObject
    {
        TerrainObjects.Add(terrain);
        return terrain;
    }

    /// <summary>
    /// 快捷添加静态矩形墙体/平台。
    /// </summary>
    public BoxTerrain AddBox(Rectangle rect, TerrainStyle style = TerrainStyle.SolidMetal, bool destructible = false, float hp = 120f)
    {
        var box = new BoxTerrain(rect, style, destructible, hp);
        TerrainObjects.Add(box);
        return box;
    }

    /// <summary>
    /// 快捷添加动态受重力影响的物理箱子（可被推动、反弹、受子弹冲击波冲击）。
    /// </summary>
    public BoxTerrain AddDynamicBox(Vector2 center, Vector2 size, float mass = 4f, TerrainStyle style = TerrainStyle.SolidMetal, float restitution = 0.25f, float drag = 0.02f)
    {
        var box = new BoxTerrain(center, size, mass, style, restitution, drag);
        TerrainObjects.Add(box);
        return box;
    }

    /// <summary>
    /// 快捷添加悬挂在天花板或横梁上的物理摆动箱（绳索/吊链约束）。
    /// </summary>
    public BoxTerrain AddHangingBox(Vector2 worldAnchor, Vector2 boxCenter, Vector2 boxSize, float mass = 3.5f, float? ropeLength = null, TerrainStyle style = TerrainStyle.SolidMetal)
    {
        var box = new BoxTerrain(boxCenter, boxSize, mass, style, 0.25f, 0.02f);
        float len = ropeLength ?? Vector2.Distance(worldAnchor, boxCenter - new Vector2(0f, boxSize.Y * 0.5f));
        box.AttachRope(worldAnchor, len);
        TerrainObjects.Add(box);
        return box;
    }

    /// <summary>
    /// 快捷添加圆形地形（如高弹反弹球或圆形掩体）。
    /// </summary>
    public CircleTerrain AddCircle(Vector2 center, float radius, TerrainStyle style = TerrainStyle.BouncyRubber, bool isStatic = true, float mass = 0f, bool destructible = false, float hp = 90f)
    {
        var circle = new CircleTerrain(center, radius, style, isStatic, mass, destructible, hp);
        TerrainObjects.Add(circle);
        return circle;
    }

    /// <summary>
    /// 快捷添加凸多边形地形（如三角形斜坡、梯形高台、异形浮岛）。
    /// </summary>
    public PolygonTerrain AddPolygon(Vector2[] worldVertices, TerrainStyle style = TerrainStyle.SolidMetal, bool isStatic = true, float mass = 0f, bool destructible = false, float hp = 160f)
    {
        var poly = new PolygonTerrain(worldVertices, style, isStatic, mass, destructible, hp);
        TerrainObjects.Add(poly);
        return poly;
    }

    /// <summary>
    /// 快捷添加匀速/正弦缓动来回巡回运动平台。
    /// </summary>
    public MovingTerrain AddMovingPlatform(Vector2 startPos, Vector2 endPos, Vector2 size, float duration = 3.5f, MotionCurve curve = MotionCurve.SmoothSine, TerrainStyle style = TerrainStyle.SolidMetal)
    {
        var moving = new MovingTerrain(startPos, endPos, size, duration, curve, style);
        TerrainObjects.Add(moving);
        return moving;
    }

    /// <summary>
    /// 将本张地图的所有组件构建为碰撞体与绳索并注入物理世界。
    /// </summary>
    public void BuildPhysicsWorld(CollisionWorld world)
    {
        CleanPhysicsWorld(world);

        // 兼容旧版列表转换
        foreach (var rect in SolidWalls)
        {
            if (!TerrainObjects.Exists(t => t is BoxTerrain b && b.Bounds == rect && b.Style == TerrainStyle.SolidMetal))
            {
                TerrainObjects.Add(new BoxTerrain(rect, TerrainStyle.SolidMetal));
            }
        }
        foreach (var rect in BouncyPillars)
        {
            if (!TerrainObjects.Exists(t => t is BoxTerrain b && b.Bounds == rect && b.Style == TerrainStyle.BouncyRubber))
            {
                TerrainObjects.Add(new BoxTerrain(rect, TerrainStyle.BouncyRubber));
            }
        }

        foreach (var terrain in TerrainObjects)
        {
            if (!terrain.IsDestroyed)
            {
                world.AddCollider(terrain.Collider);
                _registeredColliders.Add(terrain.Collider);

                if (terrain.AttachedRope != null)
                {
                    world.AddRope(terrain.AttachedRope);
                    _registeredRopes.Add(terrain.AttachedRope);
                }
            }
        }

        // 重新标记地图渲染轮廓缓存需要更新
        MapRenderer.InvalidateContourCache();
    }

    /// <summary>
    /// 从物理世界中注销本张地图的所有碰撞体与绳索。
    /// </summary>
    public void CleanPhysicsWorld(CollisionWorld world)
    {
        foreach (var col in _registeredColliders)
        {
            world.RemoveCollider(col);
        }
        _registeredColliders.Clear();

        foreach (var rope in _registeredRopes)
        {
            world.RemoveRope(rope);
        }
        _registeredRopes.Clear();
    }

    /// <summary>
    /// 每帧更新地形组件（运动平台轨迹、可破坏状态检测等）。
    /// </summary>
    public void Update(float dt, CollisionWorld world)
    {
        for (int i = TerrainObjects.Count - 1; i >= 0; i--)
        {
            var t = TerrainObjects[i];
            t.Update(dt);

            if (t.IsDestroyed && _registeredColliders.Contains(t.Collider))
            {
                world.RemoveCollider(t.Collider);
                _registeredColliders.Remove(t.Collider);

                if (t.AttachedRope != null)
                {
                    world.RemoveRope(t.AttachedRope);
                    _registeredRopes.Remove(t.AttachedRope);
                }

                MapRenderer.InvalidateContourCache();
            }
        }
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
