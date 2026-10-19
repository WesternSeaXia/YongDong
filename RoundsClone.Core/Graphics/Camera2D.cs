using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 2D 自动观察与丝滑缓动摄像机。
/// 职责边界：在双人战斗中自动计算双方空间中点，以低频温和的指数缓动跟随战局中心并进行微幅视野缩放；
/// 允许摄像机自然超出地图边缘，避免在边缘产生生硬的边界卡顿。
/// </summary>
public class Camera2D
{
    /// <summary>
    /// 摄像机当前焦点的世界坐标（默认位于竞技场中心 960, 540）。
    /// </summary>
    public Vector2 Position { get; set; } = new(960f, 540f);

    /// <summary>
    /// 摄像机当前视口缩放比例（默认 1.0f）。
    /// </summary>
    public float Zoom { get; set; } = 1.0f;

    /// <summary>
    /// 是否开启自动动态跟踪。若关闭则平滑归位至默认全景视角。
    /// </summary>
    public bool IsEnabled => GameSettings.Current.DynamicCameraEnabled;

    private Vector2? _focusTargetPos = null;
    private float _focusTargetZoom = 1.35f;

    /// <summary>
    /// 触发终局特写运镜：镜头平滑拉近并聚焦于终结击杀目标位置。
    /// </summary>
    public void FocusOnTarget(Vector2 worldPos, float targetZoom = 1.35f)
    {
        _focusTargetPos = worldPos;
        _focusTargetZoom = targetZoom;
    }

    /// <summary>
    /// 解除单点特写聚焦，恢复双人常规战斗运镜。
    /// </summary>
    public void ClearFocus()
    {
        _focusTargetPos = null;
    }

    /// <summary>
    /// 重置摄像机到默认全景位置与缩放。
    /// </summary>
    public void Reset()
    {
        _focusTargetPos = null;
        Position = new Vector2(960f, 540f);
        Zoom = 1.0f;
    }

    /// <summary>
    /// 帧更新双人动态运镜焦点与平滑缓动（允许摄像机平滑超出边缘，避免边缘顿挫）。
    /// </summary>
    public void Update(Actor? player, Actor? ai, float dt)
    {
        Vector2 defaultCenter = new(960f, 540f);
        const float defaultZoom = 1.0f;

        if (_focusTargetPos.HasValue)
        {
            const float focusMargin = 260f;
            float fx = MathHelper.Clamp(_focusTargetPos.Value.X, -focusMargin, VirtualViewport.VirtualWidth + focusMargin);
            float fy = MathHelper.Clamp(_focusTargetPos.Value.Y, -focusMargin, VirtualViewport.VirtualHeight + focusMargin);
            Vector2 focusTarget = new(fx, fy);

            Position = Vector2.Lerp(Position, focusTarget, MathHelper.Clamp(dt * 3.8f, 0f, 1f));
            Zoom = MathHelper.Lerp(Zoom, _focusTargetZoom, MathHelper.Clamp(dt * 3.2f, 0f, 1f));
            return;
        }

        if (!IsEnabled || player == null || ai == null)
        {
            Position = Vector2.Lerp(Position, defaultCenter, MathHelper.Clamp(dt * 3.2f, 0f, 1f));
            Zoom = MathHelper.Lerp(Zoom, defaultZoom, MathHelper.Clamp(dt * 2.8f, 0f, 1f));
            return;
        }

        Vector2 p1 = player.Body.Position;
        Vector2 p2 = ai.Body.Position;
        Vector2 midPoint = (p1 + p2) * 0.5f;
        float distance = Vector2.Distance(p1, p2);

        // 微幅、克制的动态视口缩放 (1.00x ~ 1.14x，消除大起大落的眩晕感)
        float targetZoom = MathHelper.Clamp(1.14f - (distance / 2000f) * 0.16f, 1.00f, 1.14f);

        // 允许摄像机跟随人物自然超出竞技场边缘（仅施加极为宽泛的软缓冲，绝不在墙边生硬卡死）
        float softMargin = 260f;
        float targetX = MathHelper.Clamp(midPoint.X, -softMargin, VirtualViewport.VirtualWidth + softMargin);
        float targetY = MathHelper.Clamp(midPoint.Y, -softMargin, VirtualViewport.VirtualHeight + softMargin);
        Vector2 targetPos = new(targetX, targetY);

        // 丝滑温和的阻尼缓动（降低跟随速度，呈现沉稳大气的摄影机质感）
        Position = Vector2.Lerp(Position, targetPos, MathHelper.Clamp(dt * 2.2f, 0f, 1f));
        Zoom = MathHelper.Lerp(Zoom, targetZoom, MathHelper.Clamp(dt * 1.8f, 0f, 1f));
    }

    /// <summary>
    /// 获取当前摄像机的 2D 变换矩阵。
    /// </summary>
    public Matrix GetMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
               * Matrix.CreateScale(Zoom, Zoom, 1f)
               * Matrix.CreateTranslation(VirtualViewport.VirtualWidth * 0.5f, VirtualViewport.VirtualHeight * 0.5f, 0f);
    }

    /// <summary>
    /// 将 1920x1080 虚拟屏幕坐标反向转换为当前摄像机视角下的战场世界坐标。
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 virtualScreenPos)
    {
        Vector2 screenCenter = new(VirtualViewport.VirtualWidth * 0.5f, VirtualViewport.VirtualHeight * 0.5f);
        return Position + (virtualScreenPos - screenCenter) / Zoom;
    }

    /// <summary>
    /// 将战场世界坐标转换为 1920x1080 虚拟屏幕坐标。
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        Vector2 screenCenter = new(VirtualViewport.VirtualWidth * 0.5f, VirtualViewport.VirtualHeight * 0.5f);
        return (worldPos - Position) * Zoom + screenCenter;
    }
}
