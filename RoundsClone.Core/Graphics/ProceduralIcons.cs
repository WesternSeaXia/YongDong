using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 图标类型枚举。
/// </summary>
public enum IconType
{
    Shield,       // 护盾/格挡
    Dash,         // 闪电/冲刺
    Crosshair,    // 准星/精准
    Bullet,       // 子弹/弹药
    Bomb,         // 炸弹/爆炸
    Poison,       // 剧毒/骷髅
    Heart,        // 生命/恢复
    Star,         // 勋章/通关
    Bounce,       // 弹跳/反射
    Heavy,        // 重装/铁砧
    Frost,        // 冰冻/霜寒
    Leech         // 吸血/尖牙
}

/// <summary>
/// 纯程序化矢量图标生成与绘制器。无需任何外部图像切片，使用几何数学公式绘制清晰锐利的卡牌与 UI 图标标记。
/// </summary>
public static class ProceduralIcons
{
    /// <summary>
    /// 在指定中心位置与半径内绘制程序化图标。
    /// </summary>
    /// <param name="batcher">几何绘制器</param>
    /// <param name="spriteBatch">精灵批次</param>
    /// <param name="type">图标枚举类型</param>
    /// <param name="center">绘制中心点</param>
    /// <param name="size">图标外包围盒像素尺寸</param>
    /// <param name="color">图标前景色</param>
    public static void DrawIcon(ShapeBatcher batcher, SpriteBatch spriteBatch, IconType type, Vector2 center, float size, Color color)
    {
        float r = size * 0.5f;

        switch (type)
        {
            case IconType.Shield:
                // 绘制带弧度的盾牌轮廓与徽记
                Vector2 topL = center + new Vector2(-r * 0.7f, -r * 0.8f);
                Vector2 topR = center + new Vector2(r * 0.7f, -r * 0.8f);
                Vector2 midL = center + new Vector2(-r * 0.7f, 0f);
                Vector2 midR = center + new Vector2(r * 0.7f, 0f);
                Vector2 botTip = center + new Vector2(0f, r * 0.9f);

                batcher.DrawLine(spriteBatch, topL, topR, color, 3f);
                batcher.DrawLine(spriteBatch, topL, midL, color, 3f);
                batcher.DrawLine(spriteBatch, topR, midR, color, 3f);
                batcher.DrawLine(spriteBatch, midL, botTip, color, 3f);
                batcher.DrawLine(spriteBatch, midR, botTip, color, 3f);
                batcher.DrawFilledCircle(spriteBatch, center, r * 0.25f, color);
                break;

            case IconType.Dash:
                // 绘制三折折线闪电/疾风矢
                Vector2 p1 = center + new Vector2(r * 0.2f, -r * 0.9f);
                Vector2 p2 = center + new Vector2(-r * 0.6f, -r * 0.1f);
                Vector2 p3 = center + new Vector2(0f, -r * 0.1f);
                Vector2 p4 = center + new Vector2(-r * 0.2f, r * 0.9f);
                Vector2 p5 = center + new Vector2(r * 0.6f, 0.1f);
                Vector2 p6 = center + new Vector2(0f, 0.1f);

                batcher.DrawLine(spriteBatch, p1, p2, color, 3.5f);
                batcher.DrawLine(spriteBatch, p2, p3, color, 3.5f);
                batcher.DrawLine(spriteBatch, p3, p4, color, 3.5f);
                batcher.DrawLine(spriteBatch, p4, p5, color, 3.5f);
                batcher.DrawLine(spriteBatch, p5, p6, color, 3.5f);
                batcher.DrawLine(spriteBatch, p6, p1, color, 3.5f);
                break;

            case IconType.Crosshair:
                // 绘制外圈准星加四向十字瞄准线
                batcher.DrawCircleOutline(spriteBatch, center, r * 0.7f, color, 2.5f);
                batcher.DrawLine(spriteBatch, center + new Vector2(-r * 0.9f, 0f), center + new Vector2(-r * 0.3f, 0f), color, 2.5f);
                batcher.DrawLine(spriteBatch, center + new Vector2(r * 0.3f, 0f), center + new Vector2(r * 0.9f, 0f), color, 2.5f);
                batcher.DrawLine(spriteBatch, center + new Vector2(0f, -r * 0.9f), center + new Vector2(0f, -r * 0.3f), color, 2.5f);
                batcher.DrawLine(spriteBatch, center + new Vector2(0f, r * 0.3f), center + new Vector2(0f, r * 0.9f), color, 2.5f);
                batcher.DrawFilledCircle(spriteBatch, center, 2.5f, color);
                break;

            case IconType.Bullet:
                // 绘制子弹弹丸形状 (圆头矩形尾)
                Vector2 tip = center + new Vector2(0f, -r * 0.6f);
                batcher.DrawFilledCircle(spriteBatch, tip, r * 0.4f, color);
                batcher.DrawRectangle(spriteBatch, new Rectangle((int)(center.X - r * 0.4f), (int)tip.Y, (int)(r * 0.8f), (int)(r * 0.9f)), color);
                break;

            case IconType.Bomb:
                // 绘制球形炸弹与引信星火
                batcher.DrawFilledCircle(spriteBatch, center + new Vector2(0f, r * 0.15f), r * 0.65f, color);
                Vector2 fuseBase = center + new Vector2(0f, -r * 0.5f);
                Vector2 fuseSpark = center + new Vector2(r * 0.4f, -r * 0.85f);
                batcher.DrawLine(spriteBatch, fuseBase, fuseSpark, color, 2f);
                batcher.DrawFilledCircle(spriteBatch, fuseSpark, r * 0.2f, Color.Gold);
                break;

            case IconType.Poison:
                // 绘制毒液滴或毒气气泡环
                batcher.DrawFilledCircle(spriteBatch, center + new Vector2(0f, r * 0.2f), r * 0.5f, color);
                batcher.DrawFilledCircle(spriteBatch, center + new Vector2(-r * 0.35f, -r * 0.3f), r * 0.25f, color);
                batcher.DrawFilledCircle(spriteBatch, center + new Vector2(r * 0.35f, -r * 0.2f), r * 0.2f, color);
                break;

            case IconType.Heart:
                // 绘制爱心 (双圆加上三角尾)
                float hr = r * 0.35f;
                Vector2 leftC = center + new Vector2(-hr, -hr * 0.4f);
                Vector2 rightC = center + new Vector2(hr, -hr * 0.4f);
                batcher.DrawFilledCircle(spriteBatch, leftC, hr, color);
                batcher.DrawFilledCircle(spriteBatch, rightC, hr, color);
                Vector2 bottomV = center + new Vector2(0f, r * 0.8f);
                batcher.DrawLine(spriteBatch, leftC + new Vector2(-hr, 0), bottomV, color, 3f);
                batcher.DrawLine(spriteBatch, rightC + new Vector2(hr, 0), bottomV, color, 3f);
                batcher.DrawFilledCircle(spriteBatch, center + new Vector2(0f, 0f), hr * 0.8f, color);
                break;

            case IconType.Bounce:
                // 绘制折线反弹光路与反弹墙
                batcher.DrawLine(spriteBatch, center + new Vector2(-r * 0.8f, r * 0.7f), center + new Vector2(r * 0.8f, r * 0.7f), color, 4f);
                Vector2 inP = center + new Vector2(-r * 0.7f, -r * 0.7f);
                Vector2 bounceP = center + new Vector2(0f, r * 0.65f);
                Vector2 outP = center + new Vector2(r * 0.7f, -r * 0.7f);
                batcher.DrawLine(spriteBatch, inP, bounceP, color, 2.5f);
                batcher.DrawLine(spriteBatch, bounceP, outP, color, 2.5f);
                batcher.DrawFilledCircle(spriteBatch, outP, 4f, color);
                break;

            case IconType.Heavy:
                // 绘制坚固厚实铁砧外形
                batcher.DrawRectangle(spriteBatch, new Rectangle((int)(center.X - r * 0.8f), (int)(center.Y - r * 0.4f), (int)(r * 1.6f), (int)(r * 0.4f)), color);
                batcher.DrawRectangle(spriteBatch, new Rectangle((int)(center.X - r * 0.5f), (int)center.Y, (int)(r * 1.0f), (int)(r * 0.6f)), color);
                break;

            case IconType.Frost:
                // 六角雪花交叉晶体
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * MathHelper.Pi / 3f;
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r * 0.75f;
                    batcher.DrawLine(spriteBatch, center - dir, center + dir, color, 2.5f);
                }
                break;

            case IconType.Star:
            default:
                // 五角星光标
                batcher.DrawFilledCircle(spriteBatch, center, r * 0.4f, color);
                batcher.DrawCircleOutline(spriteBatch, center, r * 0.8f, color, 2f);
                break;
        }
    }
}
