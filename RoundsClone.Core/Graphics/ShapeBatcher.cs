using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 纯程序化 2D 几何形状绘制器。
/// 基于动态程序生成的单像素纹理与抗锯齿圆环纹理，实现无素材依赖的高质量线段、实心圆、线框圆、胶囊体、多边形以及辉光特效绘制。
/// </summary>
public class ShapeBatcher : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Texture2D _pixelTexture;
    private readonly Texture2D _circleTexture;
    private const int CircleResolution = 128;

    /// <summary>
    /// 初始化几何绘制器并生成基础程序化像素与高精圆形纹理。
    /// </summary>
    /// <param name="graphicsDevice">图形设备上下文</param>
    public ShapeBatcher(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        // 1. 生成 1x1 纯白单像素纹理，用于绘制所有矩形、线段与几何基元
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // 2. 程序化生成高质量抗锯齿圆形纹理
        _circleTexture = new Texture2D(graphicsDevice, CircleResolution, CircleResolution);
        Color[] circleColors = new Color[CircleResolution * CircleResolution];
        float radius = CircleResolution * 0.5f;
        Vector2 center = new(radius, radius);

        for (int y = 0; y < CircleResolution; y++)
        {
            for (int x = 0; x < CircleResolution; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x + 0.5f, y + 0.5f));
                // 边缘 1 像素平滑插值实现反走样
                float alpha = MathHelper.Clamp(radius - dist, 0f, 1f);
                circleColors[y * CircleResolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        _circleTexture.SetData(circleColors);
    }

    /// <summary>
    /// 绘制单像素实心矩形。
    /// </summary>
    public void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        spriteBatch.Draw(_pixelTexture, rect, color);
    }

    /// <summary>
    /// 绘制带旋转角度的实心矩形。
    /// </summary>
    public void DrawRectangle(SpriteBatch spriteBatch, Vector2 position, Vector2 size, float rotation, Vector2 originRatio, Color color)
    {
        Vector2 origin = new(size.X * originRatio.X, size.Y * originRatio.Y);
        Rectangle destRect = new((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        spriteBatch.Draw(_pixelTexture, destRect, null, color, rotation, origin, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 绘制矩形线框。
    /// </summary>
    public void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, float thickness = 2f)
    {
        // 上
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, (int)thickness), color);
        // 下
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - (int)thickness, rect.Width, (int)thickness), color);
        // 左
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, (int)thickness, rect.Height), color);
        // 右
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - (int)thickness, rect.Y, (int)thickness, rect.Height), color);
    }

    /// <summary>
    /// 绘制圆角矩形。
    /// </summary>
    public void DrawRoundedRect(SpriteBatch spriteBatch, Rectangle rect, float cornerRadius, Color color)
    {
        int cr = (int)cornerRadius;
        // 中间与上下主体
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + cr, rect.Y, rect.Width - cr * 2, rect.Height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + cr, cr, rect.Height - cr * 2), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - cr, rect.Y + cr, cr, rect.Height - cr * 2), color);

        // 四角填充
        DrawFilledCircle(spriteBatch, new Vector2(rect.X + cr, rect.Y + cr), cr, color);
        DrawFilledCircle(spriteBatch, new Vector2(rect.Right - cr, rect.Y + cr), cr, color);
        DrawFilledCircle(spriteBatch, new Vector2(rect.X + cr, rect.Bottom - cr), cr, color);
        DrawFilledCircle(spriteBatch, new Vector2(rect.Right - cr, rect.Bottom - cr), cr, color);
    }

    /// <summary>
    /// 绘制线段。
    /// </summary>
    public void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness = 2f)
    {
        Vector2 delta = end - start;
        float angle = MathF.Atan2(delta.Y, delta.X);
        float length = delta.Length();
        spriteBatch.Draw(_pixelTexture, start, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 绘制实心平滑圆。
    /// </summary>
    /// <param name="spriteBatch">精灵批处理器</param>
    /// <param name="center">圆心坐标</param>
    /// <param name="radius">半径</param>
    /// <param name="color">填充色彩</param>
    public void DrawFilledCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        float scale = (radius * 2f) / CircleResolution;
        Vector2 origin = new(CircleResolution * 0.5f, CircleResolution * 0.5f);
        spriteBatch.Draw(_circleTexture, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 绘制具有独立 X/Y 轴形变的椭圆 (用于受击与后坐力 Squash & Stretch 物理表现)。
    /// </summary>
    public void DrawDeformedCircle(SpriteBatch spriteBatch, Vector2 center, float baseRadius, Vector2 scaleAxis, float rotation, Color color)
    {
        float scale = (baseRadius * 2f) / CircleResolution;
        Vector2 finalScale = new(scale * scaleAxis.X, scale * scaleAxis.Y);
        Vector2 origin = new(CircleResolution * 0.5f, CircleResolution * 0.5f);
        spriteBatch.Draw(_circleTexture, center, null, color, rotation, origin, finalScale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 绘制具有独立 X/Y 轴形变与旋转的椭圆轮廓线框，使描边轮廓与内部形变完全贴合一体。
    /// </summary>
    public void DrawDeformedCircleOutline(SpriteBatch spriteBatch, Vector2 center, float baseRadius, Vector2 scaleAxis, float rotation, Color color, float thickness = 2.5f)
    {
        int segments = Math.Max(20, (int)(baseRadius * 0.9f));
        float step = MathHelper.TwoPi / segments;
        float cosR = MathF.Cos(rotation);
        float sinR = MathF.Sin(rotation);

        Vector2 prevPoint = Vector2.Zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * step;
            float localX = MathF.Cos(angle) * baseRadius * scaleAxis.X;
            float localY = MathF.Sin(angle) * baseRadius * scaleAxis.Y;
            float worldX = center.X + (localX * cosR - localY * sinR);
            float worldY = center.Y + (localX * sinR + localY * cosR);
            Vector2 currPoint = new(worldX, worldY);

            if (i > 0)
            {
                DrawLine(spriteBatch, prevPoint, currPoint, color, thickness);
            }
            prevPoint = currPoint;
        }
    }

    /// <summary>
    /// 绘制平滑圆环线框。
    /// </summary>
    public void DrawCircleOutline(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness = 2f)
    {
        // 采用多边形分段线逼近绘制高品质圆环
        int segments = Math.Max(16, (int)(radius * 0.8f));
        float step = MathHelper.TwoPi / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step;
            float a2 = (i + 1) * step;
            Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            Vector2 p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            DrawLine(spriteBatch, p1, p2, color, thickness);
        }
    }

    /// <summary>
    /// 绘制平滑圆弧。
    /// </summary>
    public void DrawArc(SpriteBatch spriteBatch, Vector2 center, float radius, float startAngle, float sweepAngle, Color color, float thickness = 2f)
    {
        int segments = Math.Max(8, (int)(MathF.Abs(sweepAngle) * radius * 0.2f));
        float step = sweepAngle / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = startAngle + i * step;
            float a2 = startAngle + (i + 1) * step;
            Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            Vector2 p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            DrawLine(spriteBatch, p1, p2, color, thickness);
        }
    }

    /// <summary>
    /// 绘制发光线段（多层透明衰减实现辉光）。
    /// </summary>
    public void DrawGlowLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color coreColor, Color glowColor, float coreThickness = 3f, float glowWidth = 12f)
    {
        // 外层柔和辉光
        DrawLine(spriteBatch, start, end, glowColor * 0.25f, glowWidth);
        DrawLine(spriteBatch, start, end, glowColor * 0.5f, glowWidth * 0.5f);
        // 核心高亮线
        DrawLine(spriteBatch, start, end, coreColor, coreThickness);
    }

    /// <summary>
    /// 绘制具有发光环的多层能量球 (用于副武器护盾、子弹汇聚等)。
    /// </summary>
    public void DrawGlowCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color coreColor, Color glowColor)
    {
        DrawFilledCircle(spriteBatch, center, radius * 1.35f, glowColor * 0.2f);
        DrawFilledCircle(spriteBatch, center, radius * 1.15f, glowColor * 0.45f);
        DrawFilledCircle(spriteBatch, center, radius, coreColor);
    }

    /// <summary>
    /// 绘制丝滑连续的二阶贝塞尔弹性曲线肢体（模仿 Rounds 中可随外力弯曲的细长竹节/软管手脚）。
    /// 通过高频分段采样与抗锯齿圆帽无缝拼接，消除任何折角感。
    /// </summary>
    /// <param name="spriteBatch">精灵批处理器</param>
    /// <param name="start">肢体根部坐标 (肩部/髋部)</param>
    /// <param name="control">受力弯曲控制点坐标</param>
    /// <param name="end">肢体末端坐标 (手部/脚尖)</param>
    /// <param name="startThickness">根部核心线宽</param>
    /// <param name="endThickness">末端核心线宽</param>
    /// <param name="coreColor">肢体核心颜色</param>
    /// <param name="outlineColor">外轮廓描边颜色</param>
    /// <param name="outlineMargin">单侧描边额外宽度</param>
    public void DrawBezierLimb(
        SpriteBatch spriteBatch,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float startThickness,
        float endThickness,
        Color coreColor,
        Color outlineColor,
        float outlineMargin = 1.8f)
    {
        const int segments = 16;
        Span<Vector2> pts = stackalloc Vector2[segments + 1];
        Span<float> coreWidths = stackalloc float[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float u = 1f - t;
            pts[i] = (u * u) * start + (2f * u * t) * control + (t * t) * end;
            coreWidths[i] = MathHelper.Lerp(startThickness, endThickness, t);
        }

        // 1. 底层连续抗锯齿描边
        if (outlineMargin > 0.01f)
        {
            for (int i = 0; i < segments; i++)
            {
                float outW = (coreWidths[i] + coreWidths[i + 1]) * 0.5f + outlineMargin * 2f;
                DrawLine(spriteBatch, pts[i], pts[i + 1], outlineColor, outW);
                DrawFilledCircle(spriteBatch, pts[i], (coreWidths[i] * 0.5f) + outlineMargin, outlineColor);
            }
            DrawFilledCircle(spriteBatch, pts[segments], (coreWidths[segments] * 0.5f) + outlineMargin, outlineColor);
        }

        // 2. 顶层丝滑弹性竹节核心曲线
        for (int i = 0; i < segments; i++)
        {
            float coreW = (coreWidths[i] + coreWidths[i + 1]) * 0.5f;
            DrawLine(spriteBatch, pts[i], pts[i + 1], coreColor, coreW);
            DrawFilledCircle(spriteBatch, pts[i], coreWidths[i] * 0.5f, coreColor);
        }
        DrawFilledCircle(spriteBatch, pts[segments], coreWidths[segments] * 0.5f, coreColor);
    }

    /// <summary>
    /// 绘制严格恒定弧长的弹性竹节肢体（无论角色高速跳跃或移动，肢体总长度绝对不变，仅随力产生弯曲弧度）。
    /// </summary>
    /// <param name="spriteBatch">精灵批处理器</param>
    /// <param name="rootPos">身体边缘根部锚点</param>
    /// <param name="fixedLength">严格恒定的肢体总弧长</param>
    /// <param name="rootAngle">根部初始朝向弧度</param>
    /// <param name="bendAngle">受力产生的整体弯曲偏转角 (弧度)</param>
    /// <param name="startThickness">根部粗细</param>
    /// <param name="endThickness">末端粗细</param>
    /// <param name="coreColor">肢体黑色核心颜色</param>
    /// <param name="outlineColor">边缘微描边颜色</param>
    /// <param name="outlineMargin">描边宽度</param>
    /// <returns>肢体末端的精确世界坐标</returns>
    public Vector2 DrawFixedLengthBambooLimb(
        SpriteBatch spriteBatch,
        Vector2 rootPos,
        float fixedLength,
        float rootAngle,
        float bendAngle,
        float startThickness,
        float endThickness,
        Color coreColor,
        Color outlineColor,
        float outlineMargin = 1.0f)
    {
        const int segments = 12;
        Span<Vector2> pts = stackalloc Vector2[segments + 1];
        Span<float> coreWidths = stackalloc float[segments + 1];

        pts[0] = rootPos;
        coreWidths[0] = startThickness;
        float stepLen = fixedLength / segments;

        for (int i = 0; i < segments; i++)
        {
            float t = (i + 0.5f) / segments;
            // 沿竹节从根部到梢部平滑累加坡度弯曲角，保证每一小段长度恒为 stepLen，总弧长恒为 fixedLength
            float localAngle = rootAngle + bendAngle * (t * 1.15f);
            Vector2 dir = new(MathF.Cos(localAngle), MathF.Sin(localAngle));
            pts[i + 1] = pts[i] + dir * stepLen;
            coreWidths[i + 1] = MathHelper.Lerp(startThickness, endThickness, (i + 1) / (float)segments);
        }

        if (outlineMargin > 0.01f)
        {
            for (int i = 0; i < segments; i++)
            {
                float outW = (coreWidths[i] + coreWidths[i + 1]) * 0.5f + outlineMargin * 2f;
                DrawLine(spriteBatch, pts[i], pts[i + 1], outlineColor, outW);
                DrawFilledCircle(spriteBatch, pts[i], (coreWidths[i] * 0.5f) + outlineMargin, outlineColor);
            }
            DrawFilledCircle(spriteBatch, pts[segments], (coreWidths[segments] * 0.5f) + outlineMargin, outlineColor);
        }

        for (int i = 0; i < segments; i++)
        {
            float coreW = (coreWidths[i] + coreWidths[i + 1]) * 0.5f;
            DrawLine(spriteBatch, pts[i], pts[i + 1], coreColor, coreW);
            DrawFilledCircle(spriteBatch, pts[i], coreWidths[i] * 0.5f, coreColor);
        }
        DrawFilledCircle(spriteBatch, pts[segments], coreWidths[segments] * 0.5f, coreColor);

        return pts[segments];
    }

    /// <summary>
    /// 释放非托管纹理资源。
    /// </summary>
    public void Dispose()
    {
        _pixelTexture.Dispose();
        _circleTexture.Dispose();
    }
}
