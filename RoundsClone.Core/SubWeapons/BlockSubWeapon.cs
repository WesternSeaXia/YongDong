using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.SubWeapons;

/// <summary>
/// 格挡护盾副武器实现。
/// 在角色周围展开高能偏转力场，能完美反弹迎面而来的敌方子弹并赋予加速增幅，同时向外产生径向击退脉冲。
/// </summary>
public class BlockSubWeapon : ISubWeapon
{
    public string Id => "sub_block";
    public string Name => "力场格挡";
    public string Description => "展开瞬态护盾，反弹一切子弹并施加强劲击退脉冲。";
    public IconType Icon => IconType.Shield;

    public float Cooldown { get; set; } = 2.4f;
    public float CurrentCooldown { get; set; } = 0f;
    public bool IsReady => CurrentCooldown <= 0f;
    public float ActiveDuration => 0.35f;
    public bool IsActive => _activeTimer > 0f;

    /// <summary>
    /// 力场有效保护半径。
    /// </summary>
    public float ShieldRadius => 72f;

    private float _activeTimer = 0f;
    private float _pulseAnimation = 0f;

    public bool TryActivate(Actor user, Vector2 direction)
    {
        if (!IsReady) return false;

        CurrentCooldown = Cooldown;
        _activeTimer = ActiveDuration;
        _pulseAnimation = 0f;

        // 给予角色轻微受力稳定
        user.Body.Velocity *= 0.5f;

        return true;
    }

    public void Update(float dt)
    {
        if (CurrentCooldown > 0f) CurrentCooldown -= dt;
        if (_activeTimer > 0f)
        {
            _activeTimer -= dt;
            _pulseAnimation += dt * 4f;
        }
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, Actor user)
    {
        if (!IsActive) return;

        Vector2 center = user.Body.Position;
        float progress = 1f - (_activeTimer / ActiveDuration);
        float currentRadius = ShieldRadius * (0.85f + progress * 0.25f);
        float alpha = MathF.Sin(progress * MathHelper.Pi);

        // 绘制内外双层霓虹能量力场环
        Color shieldColor = ColorPalette.BlockShield * alpha;
        batcher.DrawCircleOutline(spriteBatch, center, currentRadius, shieldColor, 4f);
        batcher.DrawCircleOutline(spriteBatch, center, currentRadius * 0.85f, Color.White * (alpha * 0.6f), 2f);

        // 绘制正六边形防御符文
        int segments = 6;
        float step = MathHelper.TwoPi / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step + _pulseAnimation;
            float a2 = (i + 1) * step + _pulseAnimation;
            Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (currentRadius * 0.95f);
            Vector2 p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * (currentRadius * 0.95f);
            batcher.DrawLine(spriteBatch, p1, p2, shieldColor * 0.8f, 2f);
        }
    }
}
