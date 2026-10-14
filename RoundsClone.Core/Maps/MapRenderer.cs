using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图与关卡缩略图高级渲染器。
/// 职责边界：负责背景网格、同材质不可动地形的边界自动无缝融合外轮廓消隐算法、动态受重力箱体/绳链、运动导轨及选关微缩预览。
/// </summary>
public static class MapRenderer
{
    private static bool _contourDirty = true;
    private static readonly List<(Vector2 Start, Vector2 End, Color Color)> _cachedContours = new();
    private static string _cachedMapId = string.Empty;

    /// <summary>
    /// 标记边界融合轮廓缓存失效（当切换地图或可破坏掩体损毁时调用）。
    /// </summary>
    public static void InvalidateContourCache()
    {
        _contourDirty = true;
    }

    /// <summary>
    /// 渲染战斗场景中的完整地图（科技网格、同样式无缝合并静态地形、动态悬挂箱、运动平台）。
    /// </summary>
    public static void Draw(MapData map, ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        // 1. 渲染背景科技网格
        for (int x = 0; x < VirtualViewport.VirtualWidth; x += 120)
        {
            batcher.DrawLine(spriteBatch, new Vector2(x, 0), new Vector2(x, VirtualViewport.VirtualHeight), ColorPalette.GridLine, 1f);
        }
        for (int y = 0; y < VirtualViewport.VirtualHeight; y += 120)
        {
            batcher.DrawLine(spriteBatch, new Vector2(0, y), new Vector2(VirtualViewport.VirtualWidth, y), ColorPalette.GridLine, 1f);
        }

        // 2. 检查并重建同材质静态地形的无缝合并外轮廓
        if (_contourDirty || _cachedMapId != map.Id)
        {
            RebuildMergedContours(map);
            _contourDirty = false;
            _cachedMapId = map.Id;
        }

        // 3. 渲染所有不可动非破坏静态地形的实心填充（同材质邻接块天然无缝交融）
        foreach (var terrain in map.TerrainObjects)
        {
            if (terrain.IsStatic && !terrain.IsDestructible && !terrain.IsDestroyed && terrain is not MovingTerrain)
            {
                Color fill = TerrainStyleHelper.GetFillColor(terrain.Style);
                if (terrain is BoxTerrain box)
                {
                    batcher.DrawRectangle(spriteBatch, box.Bounds, fill);
                }
                else if (terrain is PolygonTerrain poly)
                {
                    batcher.DrawFilledConvexPolygon(spriteBatch, poly.PolyCol.WorldVertices, fill);
                }
                else if (terrain is CircleTerrain circle)
                {
                    batcher.DrawFilledCircle(spriteBatch, circle.Body.Position, circle.Radius, fill);
                }
            }
        }

        // 4. 渲染同材质合并后的纯外轮廓线（消除所有内部相连的分隔线，呈现浑然一体的外边界）
        for (int i = 0; i < _cachedContours.Count; i++)
        {
            var line = _cachedContours[i];
            batcher.DrawLine(spriteBatch, line.Start, line.End, line.Color, 2.5f);
        }

        // 5. 渲染具有特异性状态的地形（受重力动态箱、绳索吊链、可破坏砖块、运动平台）
        foreach (var terrain in map.TerrainObjects)
        {
            if (terrain.IsDestroyed) continue;

            if (!terrain.IsStatic || terrain.IsDestructible || terrain is MovingTerrain)
            {
                terrain.Draw(batcher, spriteBatch);
            }
        }
    }

    /// <summary>
    /// 重构同材质不可动地形的外轮廓集合，自动抹除内部相切与重叠的连接边界线。
    /// </summary>
    private static void RebuildMergedContours(MapData map)
    {
        _cachedContours.Clear();

        // 将所有静态不可破坏地形按材质样式分组
        Dictionary<TerrainStyle, List<ITerrainObject>> groups = new();
        foreach (var t in map.TerrainObjects)
        {
            if (t.IsStatic && !t.IsDestructible && !t.IsDestroyed && t is not MovingTerrain)
            {
                if (!groups.TryGetValue(t.Style, out var list))
                {
                    list = new List<ITerrainObject>();
                    groups[t.Style] = list;
                }
                list.Add(t);
            }
        }

        // 针对每个材质组，提取所有外边框并剔除内部共享边
        foreach (var (style, list) in groups)
        {
            Color outlineCol = TerrainStyleHelper.GetOutlineColor(style);

            foreach (var t in list)
            {
                if (t is BoxTerrain box)
                {
                    Rectangle r = box.Bounds;
                    // 4 条边界：上、下、左、右
                    ClipAndAddSegment(new Vector2(r.Left, r.Top), new Vector2(r.Right, r.Top), -Vector2.UnitY, list, t, outlineCol);
                    ClipAndAddSegment(new Vector2(r.Left, r.Bottom), new Vector2(r.Right, r.Bottom), Vector2.UnitY, list, t, outlineCol);
                    ClipAndAddSegment(new Vector2(r.Left, r.Top), new Vector2(r.Left, r.Bottom), -Vector2.UnitX, list, t, outlineCol);
                    ClipAndAddSegment(new Vector2(r.Right, r.Top), new Vector2(r.Right, r.Bottom), Vector2.UnitX, list, t, outlineCol);
                }
                else if (t is PolygonTerrain poly)
                {
                    var verts = poly.PolyCol.WorldVertices;
                    int n = verts.Length;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 p1 = verts[i];
                        Vector2 p2 = verts[(i + 1) % n];
                        Vector2 edge = p2 - p1;
                        Vector2 normal = MathHelper2D.SafeNormalize(new Vector2(-edge.Y, edge.X));
                        ClipAndAddSegment(p1, p2, normal, list, t, outlineCol);
                    }
                }
                else if (t is CircleTerrain circle)
                {
                    // 圆形独立绘制高光边缘
                    _cachedContours.Add((circle.Body.Position, circle.Body.Position, outlineCol));
                }
            }
        }
    }

    /// <summary>
    /// 将一条线段按步长采样测试法向外侧点，仅将面向空气的外暴露边缘保留并加入轮廓缓存。
    /// </summary>
    private static void ClipAndAddSegment(Vector2 p1, Vector2 p2, Vector2 outNormal, List<ITerrainObject> group, ITerrainObject self, Color color)
    {
        Vector2 delta = p2 - p1;
        float len = delta.Length();
        if (len < 2f) return;

        Vector2 dir = delta / len;
        float stepSize = 4f;
        int stepCount = (int)MathF.Ceiling(len / stepSize);

        Vector2? runStart = null;
        Vector2 currentEnd = p1;

        for (int i = 0; i <= stepCount; i++)
        {
            float dist = MathF.Min(i * stepSize, len);
            Vector2 pt = p1 + dir * dist;

            // 沿外向法线微探 1.5 像素，若该测试点落在同组其他地形内部，说明此段为内部连通缝隙
            Vector2 probe = pt + outNormal * 1.5f;
            bool isInterior = false;

            for (int g = 0; g < group.Count; g++)
            {
                var other = group[g];
                if (other == self) continue;

                if (IsPointInsideTerrain(probe, other))
                {
                    isInterior = true;
                    break;
                }
            }

            if (!isInterior)
            {
                if (runStart == null)
                {
                    runStart = pt;
                }
                currentEnd = pt;
            }
            else
            {
                if (runStart != null)
                {
                    if (Vector2.Distance(runStart.Value, currentEnd) >= 1.5f)
                    {
                        _cachedContours.Add((runStart.Value, currentEnd, color));
                    }
                    runStart = null;
                }
            }
        }

        if (runStart != null && Vector2.Distance(runStart.Value, currentEnd) >= 1.5f)
        {
            _cachedContours.Add((runStart.Value, currentEnd, color));
        }
    }

    private static bool IsPointInsideTerrain(Vector2 pt, ITerrainObject terrain)
    {
        if (terrain is BoxTerrain box)
        {
            Rectangle b = box.Bounds;
            return pt.X >= b.Left - 0.5f && pt.X <= b.Right + 0.5f &&
                   pt.Y >= b.Top - 0.5f && pt.Y <= b.Bottom + 0.5f;
        }
        if (terrain is CircleTerrain circle)
        {
            return Vector2.DistanceSquared(pt, circle.Body.Position) <= circle.Radius * circle.Radius;
        }
        if (terrain is PolygonTerrain poly)
        {
            var verts = poly.PolyCol.WorldVertices;
            int n = verts.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p1 = verts[i];
                Vector2 p2 = verts[(i + 1) % n];
                Vector2 edge = p2 - p1;
                Vector2 norm = new(-edge.Y, edge.X);
                if (Vector2.Dot(norm, pt - p1) > 0.5f)
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 在指定目标矩形区域内绘制地图的等比微缩预览图（用于选关界面）。
    /// </summary>
    public static void DrawThumbnail(MapData map, ShapeBatcher batcher, SpriteBatch spriteBatch, Rectangle destRect)
    {
        float scaleX = (float)destRect.Width / VirtualViewport.VirtualWidth;
        float scaleY = (float)destRect.Height / VirtualViewport.VirtualHeight;

        batcher.DrawRectangle(spriteBatch, destRect, ColorPalette.BackgroundDark);
        batcher.DrawRectangleOutline(spriteBatch, destRect, ColorPalette.WallOutline, 2f);

        foreach (var terrain in map.TerrainObjects)
        {
            Color fill = TerrainStyleHelper.GetFillColor(terrain.Style);
            if (terrain is BoxTerrain box)
            {
                Rectangle thumbWall = new(
                    destRect.X + (int)(box.Bounds.X * scaleX),
                    destRect.Y + (int)(box.Bounds.Y * scaleY),
                    Math.Max(2, (int)(box.Bounds.Width * scaleX)),
                    Math.Max(2, (int)(box.Bounds.Height * scaleY))
                );
                batcher.DrawRectangle(spriteBatch, thumbWall, fill);
            }
            else if (terrain is CircleTerrain circle)
            {
                Vector2 thumbCenter = new(
                    destRect.X + circle.Body.Position.X * scaleX,
                    destRect.Y + circle.Body.Position.Y * scaleY
                );
                batcher.DrawFilledCircle(spriteBatch, thumbCenter, MathF.Max(2f, circle.Radius * scaleX), fill);
            }
            else if (terrain is PolygonTerrain poly)
            {
                var verts = poly.PolyCol.WorldVertices;
                Vector2[] thumbVerts = new Vector2[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    thumbVerts[i] = new Vector2(
                        destRect.X + verts[i].X * scaleX,
                        destRect.Y + verts[i].Y * scaleY
                    );
                }
                batcher.DrawFilledConvexPolygon(spriteBatch, thumbVerts, fill);
            }
        }
    }
}
