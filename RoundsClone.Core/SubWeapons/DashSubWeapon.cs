using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.SubWeapons;

/// <summary>
/// 瞬态冲刺副武器实现。
/// 向输入矢量施加瞬间极大冲量，并提供短暂无敌帧与水滴拉伸形变，用于机动突围或躲避致命弹幕。
/// </summary>
public class DashSubWeapon : ISubWeapon
{
    public string Id => "sub_dash";
    public string Name => "疾风冲刺";
    public string Description => "沿当前运动或瞄准方向瞬间爆发冲量，期间获得无敌帧。";
    public IconType Icon => IconType.Dash;

    public float Cooldown { get; set; } = 1.6f;
    public float CurrentCooldown { get; set; } = 0f;
    public bool IsReady => CurrentCooldown <= 0f;
    public float ActiveDuration => 0.20f;
    public bool IsActive => _activeTimer > 0f;

    /// <summary>
    /// 爆发瞬时速度 (像素/秒)。
    /// </summary>
    public float DashSpeed { get; set; } = 1450f;

    private float _activeTimer = 0f;
    private Vector2 _dashDirection = Vector2.UnitX;
    private Actor? _currentUser;

    public bool TryActivate(Actor user, Vector2 direction)
    {
        if (!IsReady) return false;

        _currentUser = user;
        CurrentCooldown = Cooldown;
        _activeTimer = ActiveDuration;

        // 优先使用速度移动方向；若处于静止则使用瞄准方向
        Vector2 moveIntent = user.Body.Velocity;
        if (moveIntent.LengthSquared() > 100f)
        {
            _dashDirection = MathHelper2D.SafeNormalize(moveIntent);
        }
        else if (direction.LengthSquared() > 0.01f)
        {
            _dashDirection = MathHelper2D.SafeNormalize(direction);
        }
        else
        {
            _dashDirection = Vector2.UnitX;
        }

        // 赋予爆发速度冲量
        user.Body.Velocity = _dashDirection * DashSpeed;
        user.IsInvulnerable = true;

        // 沿冲刺方向施加强烈的拉长变形
        user.Squash.ApplyImpulse(-_dashDirection, 0.55f);

        return true;
    }

    public void Update(float dt)
    {
        if (CurrentCooldown > 0f) CurrentCooldown -= dt;

        if (_activeTimer > 0f)
        {
            _activeTimer -= dt;
            if (_activeTimer <= 0f && _currentUser != null)
            {
                _currentUser.IsInvulnerable = false;
            }
        }
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, Actor user)
    {
        if (!IsActive) return;

        // 绘制冲刺流光残影拖尾
        Vector2 pos = user.Body.Position;
        float progress = _activeTimer / ActiveDuration;
        Vector2 backPos1 = pos - _dashDirection * 24f;
        Vector2 backPos2 = pos - _dashDirection * 48f;

        batcher.DrawFilledCircle(spriteBatch, backPos1, user.Radius * 0.75f, ColorPalette.DashTrail * (progress * 0.5f));
        batcher.DrawFilledCircle(spriteBatch, backPos2, user.Radius * 0.50f, ColorPalette.DashTrail * (progress * 0.25f));
    }
}
