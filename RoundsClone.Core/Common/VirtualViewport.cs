using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RoundsClone.Core.Common;

/// <summary>
/// 虚拟视口与分辨率适配管理器。
/// 将游戏固定逻辑分辨率 (1920x1080) 自适应投射到任意窗口或移动端屏幕上，保持等比黑边 (Letterbox) 并提供双向坐标映射。
/// </summary>
public class VirtualViewport
{
    /// <summary>
    /// 虚拟逻辑宽度。
    /// </summary>
    public const int VirtualWidth = 1920;

    /// <summary>
    /// 虚拟逻辑高度。
    /// </summary>
    public const int VirtualHeight = 1080;

    /// <summary>
    /// 当前是否运行在移动端（Android / iOS）小尺寸高分屏设备上。
    /// </summary>
    public static bool IsMobile => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    /// <summary>
    /// 当前屏幕视口矩形（居中渲染区域）。
    /// </summary>
    public Rectangle ViewportBounds { get; private set; }

    /// <summary>
    /// 从虚拟坐标空间映射到屏幕空间的缩放矩阵，用于 SpriteBatch.Begin。
    /// </summary>
    public Matrix TransformMatrix { get; private set; }

    /// <summary>
    /// 逆矩阵，用于将屏幕触控/鼠标像素坐标反推回虚拟世界坐标。
    /// </summary>
    public Matrix InverseMatrix { get; private set; }

    /// <summary>
    /// 当前全局缩放比例。
    /// </summary>
    public float Scale { get; private set; } = 1.0f;

    /// <summary>
    /// 根据物理窗口尺寸更新投影矩阵与有效视口。
    /// </summary>
    /// <param name="graphicsDevice">图形设备</param>
    public void Update(GraphicsDevice graphicsDevice)
    {
        int screenWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
        int screenHeight = graphicsDevice.PresentationParameters.BackBufferHeight;

        float scaleX = (float)screenWidth / VirtualWidth;
        float scaleY = (float)screenHeight / VirtualHeight;
        Scale = MathF.Min(scaleX, scaleY);

        int viewWidth = (int)MathF.Round(VirtualWidth * Scale);
        int viewHeight = (int)MathF.Round(VirtualHeight * Scale);
        int viewX = (screenWidth - viewWidth) / 2;
        int viewY = (screenHeight - viewHeight) / 2;

        ViewportBounds = new Rectangle(viewX, viewY, viewWidth, viewHeight);

        TransformMatrix = Matrix.CreateTranslation(-VirtualWidth * 0.5f, -VirtualHeight * 0.5f, 0f)
                          * Matrix.CreateScale(Scale, Scale, 1.0f)
                          * Matrix.CreateTranslation(viewX + viewWidth * 0.5f, viewY + viewHeight * 0.5f, 0f);

        InverseMatrix = Matrix.Invert(TransformMatrix);
    }

    /// <summary>
    /// 将屏幕像素坐标（如鼠标点击、手指触摸点）转换为逻辑虚拟世界的 2D 坐标。
    /// </summary>
    /// <param name="screenPos">物理屏幕像素坐标</param>
    /// <returns>1920x1080 范围内的虚拟世界坐标</returns>
    public Vector2 ScreenToVirtual(Vector2 screenPos)
    {
        return Vector2.Transform(screenPos, InverseMatrix);
    }

    /// <summary>
    /// 将虚拟世界坐标转换为屏幕像素坐标。
    /// </summary>
    /// <param name="virtualPos">虚拟世界坐标</param>
    /// <returns>物理屏幕像素坐标</returns>
    public Vector2 VirtualToScreen(Vector2 virtualPos)
    {
        return Vector2.Transform(virtualPos, TransformMatrix);
    }
}
