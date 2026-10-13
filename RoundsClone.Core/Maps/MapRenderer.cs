using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地图与关卡缩略图渲染器。
/// 职责边界：专门负责将 <see cref="MapData"/> 的几何结构绘制为战斗背景网格、墙体、高弹反弹柱以及选关界面的等比缩略图。
/// </summary>
public static class MapRenderer
{
    /// <summary>
    /// 渲染战斗场景中的完整地图背景网格、实体墙与高弹性反弹柱。
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

        // 2. 渲染实体墙壁
        foreach (var wall in map.SolidWalls)
        {
            batcher.DrawRoundedRect(spriteBatch, wall, 4f, ColorPalette.WallSolid);
            batcher.DrawRectangleOutline(spriteBatch, wall, ColorPalette.WallOutline, 2.5f);
        }

        // 3. 渲染高弹性粉红反弹柱
        foreach (var bounce in map.BouncyPillars)
        {
            batcher.DrawRoundedRect(spriteBatch, bounce, 6f, ColorPalette.BouncySurface);
            batcher.DrawRectangleOutline(spriteBatch, bounce, Color.White, 3f);
        }
    }

    /// <summary>
    /// 在指定目标矩形区域内绘制地图的等比微缩预览图（用于关卡三选一界面）。
    /// </summary>
    public static void DrawThumbnail(MapData map, ShapeBatcher batcher, SpriteBatch spriteBatch, Rectangle destRect)
    {
        float scaleX = (float)destRect.Width / VirtualViewport.VirtualWidth;
        float scaleY = (float)destRect.Height / VirtualViewport.VirtualHeight;

        batcher.DrawRectangle(spriteBatch, destRect, ColorPalette.BackgroundDark);
        batcher.DrawRectangleOutline(spriteBatch, destRect, ColorPalette.WallOutline, 2f);

        foreach (var wall in map.SolidWalls)
        {
            Rectangle thumbWall = new(
                destRect.X + (int)(wall.X * scaleX),
                destRect.Y + (int)(wall.Y * scaleY),
                Math.Max(2, (int)(wall.Width * scaleX)),
                Math.Max(2, (int)(wall.Height * scaleY))
            );
            batcher.DrawRectangle(spriteBatch, thumbWall, ColorPalette.WallSolid);
        }

        foreach (var bounce in map.BouncyPillars)
        {
            Rectangle thumbBounce = new(
                destRect.X + (int)(bounce.X * scaleX),
                destRect.Y + (int)(bounce.Y * scaleY),
                Math.Max(2, (int)(bounce.Width * scaleX)),
                Math.Max(2, (int)(bounce.Height * scaleY))
            );
            batcher.DrawRectangle(spriteBatch, thumbBounce, ColorPalette.BouncySurface);
        }
    }
}
