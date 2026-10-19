using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.SubWeapons;

/// <summary>
/// 瞬态冲刺副武器实现。
/// 职责边界：沿输入方向瞬间施加强力爆发冲量，并在冲刺期间赋予角色完全无敌帧免疫任何子弹伤害（不反弹子弹）；
/// 视觉上提供多段平滑衰减的角色剪影残影与发光拖尾微粒。
/// </summary>
public class DashSubWeapon : ISubWeapon
{
    public string Id => "sub_dash";
    public string Name => "疾风冲刺";
    public string Description => "瞬间向指引/瞄准方向爆发冲刺 200px，期间完全免疫子弹伤害，附带连续残影。";
    public IconType Icon => IconType.Dash;

    public float Cooldown { get; set; } = 1.6f;
    public float CurrentCooldown { get; set; } = 0f;
    public bool IsReady => CurrentCooldown <= 0f;
    public float ActiveDuration => 0.15f;
    public bool IsActive => _activeTimer > 0f;

    /// <summary>
    /// 爆发瞬时速度 (像素/秒)。
    /// 默认对应 200px / 0.15s ≈ 1333.3px/s。
    /// </summary>
    public float DashSpeed { get; set; } = 200f / 0.15f;

    /// <summary>
    /// 冲刺目标位移距离 (默认 200 像素)。
    /// </summary>
    public float DashDistance
    {
        get => DashSpeed * ActiveDuration;
        set => DashSpeed = value / ActiveDuration;
    }

    private float _activeTimer = 0f;
    private Vector2 _dashDirection = Vector2.UnitX;
    private Actor? _currentUser;
    private float _ghostSampleTimer = 0f;

    private struct DashGhost
    {
        public Vector2 Position;
        public Vector2 Scale;
        public float Angle;
        public float Alpha;
        public Color Color;
    }

    private readonly List<DashGhost> _ghosts = new(8);

    public bool TryActivate(Actor user, Vector2 direction)
    {
        if (!IsReady) return false;

        _currentUser = user;
        CurrentCooldown = Cooldown;
        _activeTimer = ActiveDuration;
        _ghostSampleTimer = 0f;
        _ghosts.Clear();

        // 优先严格朝向鼠标所在的世界坐标方向冲刺
        if (direction.LengthSquared() > 0.001f)
        {
            _dashDirection = MathHelper2D.SafeNormalize(direction);
        }
        else if (user.Body.Velocity.LengthSquared() > 10f)
        {
            _dashDirection = MathHelper2D.SafeNormalize(user.Body.Velocity);
        }
        else
        {
            _dashDirection = Vector2.UnitX;
        }

        // 爆发恒定速度突破重力与摩擦束缚，确保精准穿行 200px
        user.Body.Velocity = _dashDirection * DashSpeed;
        user.Body.IsGrounded = false;
        user.IsInvulnerable = true;
        user.Squash.ApplyImpulse(-_dashDirection, 0.55f);

        // 立即采样首个残影
        SpawnGhost(user);

        return true;
    }

    public void Update(float dt)
    {
        if (CurrentCooldown > 0f) CurrentCooldown -= dt;

        // 更新并衰减残影队列
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var ghost = _ghosts[i];
            ghost.Alpha -= dt * 4.8f;
            if (ghost.Alpha <= 0f)
            {
                _ghosts.RemoveAt(i);
            }
            else
            {
                _ghosts[i] = ghost;
            }
        }

        if (_activeTimer > 0f)
        {
            _activeTimer -= dt;

            if (_currentUser != null)
            {
                // 冲刺期间锁定速度矢量，确保在 0.15s 内精准直贯 200px
                if (_activeTimer > 0f)
                {
                    _currentUser.Body.Velocity = _dashDirection * DashSpeed;
                }

                // 采样残影
                _ghostSampleTimer += dt;
                if (_ghostSampleTimer >= 0.025f)
                {
                    _ghostSampleTimer = 0f;
                    SpawnGhost(_currentUser);
                }

                if (_activeTimer <= 0f)
                {
                    _currentUser.IsInvulnerable = false;
                    _currentUser.Body.Velocity = _dashDirection * 260f; // 结束时保留适度余速惯性
                }
            }
        }
    }

    private void SpawnGhost(Actor user)
    {
        if (_ghosts.Count >= 7) _ghosts.RemoveAt(0);

        _ghosts.Add(new DashGhost
        {
            Position = user.Body.Position,
            Scale = user.Squash.CurrentScale,
            Angle = user.Squash.DeformationAngle,
            Alpha = 0.72f,
            Color = Color.Lerp(user.ThemeColor, ColorPalette.DashTrail, 0.45f)
        });
    }

    /// <summary>
    /// 渲染冲刺的多层角色轮廓残影与发光拖尾。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, Actor user)
    {
        float visualRadius = user.Radius * 0.78f;

        // 1. 绘制历史残影
        foreach (var ghost in _ghosts)
        {
            if (ghost.Alpha <= 0.02f) continue;

            Color ghostFill = ghost.Color * (ghost.Alpha * 0.40f);
            Color ghostRim = Color.White * (ghost.Alpha * 0.70f);

            batcher.DrawDeformedCircle(spriteBatch, ghost.Position, visualRadius, ghost.Scale, ghost.Angle, ghostFill);
            batcher.DrawDeformedCircleOutline(spriteBatch, ghost.Position, visualRadius, ghost.Scale, ghost.Angle, ghostRim, 2.0f);
        }

        // 2. 冲刺中绘制瞬态流光拖尾
        if (IsActive)
        {
            Vector2 p0 = user.Body.Position;
            Vector2 p1 = p0 - _dashDirection * (visualRadius * 1.6f);
            Vector2 p2 = p0 - _dashDirection * (visualRadius * 2.8f);

            batcher.DrawLine(spriteBatch, p0, p1, Color.White * 0.75f, 3.5f);
            batcher.DrawLine(spriteBatch, p1, p2, user.ThemeColor * 0.50f, 2.0f);
        }
    }
}
