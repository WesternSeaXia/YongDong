using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 毒云弥漫实体 (Toxic Cloud)。
/// 职责边界：子弹命中留下的范围滞留毒雾，为踏入其中的敌方单位持续施加减速与毒素腐蚀伤害。
/// </summary>
public class ToxicCloud
{
    public Vector2 Position { get; }
    public float Radius { get; set; } = 75f;
    public float Duration { get; set; } = 3.5f;
    public float Timer { get; private set; } = 0f;
    public Actor Owner { get; }
    public bool IsExpired => Timer >= Duration;

    private float _particleTimer = 0f;
    private float _damageTickTimer = 0f;

    public ToxicCloud(Vector2 position, Actor owner, float duration = 3.5f, float radius = 75f)
    {
        Position = position;
        Owner = owner;
        Duration = duration;
        Radius = radius;
    }

    public void Update(float dt, List<Actor> actors, ParticleSystem particles, ScreenShake shake)
    {
        if (IsExpired) return;

        Timer += dt;
        _particleTimer += dt;
        if (_particleTimer >= 0.09f)
        {
            _particleTimer = 0f;
            Vector2 offset = new(
                (float)(Random.Shared.NextDouble() - 0.5) * Radius * 1.6f,
                (float)(Random.Shared.NextDouble() - 0.5) * Radius * 1.6f
            );
            particles.EmitHitSparks(Position + offset, -Vector2.UnitY, ColorPalette.PoisonGreen, 2);
        }

        _damageTickTimer += dt;
        bool shouldDealDamage = false;
        if (_damageTickTimer >= 0.25f)
        {
            _damageTickTimer = 0f;
            shouldDealDamage = true;
        }

        float radiusSq = Radius * Radius;
        foreach (var actor in actors)
        {
            if (actor == Owner || actor.IsDead || actor.IsInvulnerable) continue;

            float distSq = Vector2.DistanceSquared(Position, actor.Body.Position);
            if (distSq <= radiusSq)
            {
                // 持续减速 35%
                actor.Body.Velocity *= MathF.Max(0.65f, 1f - 0.35f * dt * 4f);

                // 每 0.25 秒跳一次腐蚀伤害
                if (shouldDealDamage)
                {
                    actor.TakeDamage(3.5f, Owner, Vector2.Zero, particles, shake);
                }
            }
        }
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsExpired) return;

        float alpha = MathHelper.Clamp(1.0f - (Timer / Duration), 0f, 1f) * 0.30f;
        Color cloudColor = ColorPalette.PoisonGreen * alpha;

        // 柔和半透明双层毒雾环
        batcher.DrawFilledCircle(spriteBatch, Position, Radius, cloudColor);
        batcher.DrawCircleOutline(spriteBatch, Position, Radius, ColorPalette.PoisonGreen * (alpha * 1.5f), 2.0f);
    }
}
