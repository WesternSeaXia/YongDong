using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 粒子几何外形形态。
/// </summary>
public enum ParticleShape
{
    Circle,
    LineSpark,
    Ring,
    Square
}

/// <summary>
/// 单个物理力驱动粒子数据实体。
/// </summary>
public class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Drag = 0.96f;
    public float Gravity = 400f;
    public float Rotation;
    public float AngularVelocity;

    public Color StartColor;
    public Color EndColor;
    public float StartSize;
    public float EndSize;

    public float Lifetime;
    public float MaxLifetime;
    public ParticleShape Shape;

    public bool IsAlive => Lifetime < MaxLifetime;

    public void Update(float dt)
    {
        Lifetime += dt;
        Velocity.Y += Gravity * dt;
        Velocity *= MathF.Pow(Drag, dt * 60f);
        Position += Velocity * dt;
        Rotation += AngularVelocity * dt;
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        float t = MathHelper.Clamp(Lifetime / MaxLifetime, 0f, 1f);
        // 平滑二次方渐隐，避免生硬截断
        float alpha = (1f - t) * (1f - t);
        Color color = Color.Lerp(StartColor, EndColor, t) * alpha;
        float size = MathHelper.Lerp(StartSize, EndSize, t);
        if (size <= 0.1f) return;

        switch (Shape)
        {
            case ParticleShape.Circle:
                batcher.DrawFilledCircle(spriteBatch, Position, size * 0.5f, color);
                break;
            case ParticleShape.LineSpark:
                float velLen = Velocity.Length();
                if (velLen > 10f)
                {
                    Vector2 dir = Velocity / velLen;
                    Vector2 tail = Position - dir * MathF.Min(size * 2.6f, velLen * 0.06f);
                    batcher.DrawLine(spriteBatch, tail, Position, color, Math.Max(1.5f, size * 0.35f));
                }
                else
                {
                    batcher.DrawFilledCircle(spriteBatch, Position, size * 0.4f, color);
                }
                break;
            case ParticleShape.Ring:
                batcher.DrawCircleOutline(spriteBatch, Position, size * 0.5f, color, Math.Max(1.5f, (1f - t) * 3.5f));
                break;
            case ParticleShape.Square:
                Vector2 half = new(size * 0.5f);
                batcher.DrawRectangle(spriteBatch, new Rectangle((int)(Position.X - half.X), (int)(Position.Y - half.Y), Math.Max(2, (int)size), Math.Max(2, (int)size)), color);
                break;
        }
    }
}

/// <summary>
/// 纯矢量物理粒子系统管理器，提供受力计算、生命周期池化与多样化程序特效发生器。
/// </summary>
public class ParticleSystem
{
    private const int MaxParticles = 450;
    private readonly List<Particle> _particles = new(512);
    private readonly Random _rand = new();

    /// <summary>
    /// 更新所有存活粒子的物理动力学行为。
    /// </summary>
    /// <param name="dt">时间步长</param>
    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Update(dt);
            if (!p.IsAlive)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 渲染所有粒子。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        foreach (var p in _particles)
        {
            p.Draw(batcher, spriteBatch);
        }
    }

    private void AddParticle(Particle p)
    {
        if (_particles.Count >= MaxParticles)
        {
            _particles.RemoveAt(0); // 移除最早粒子，保持恒定 60FPS
        }
        _particles.Add(p);
    }

    /// <summary>
    /// 在指定点触发放射状碰撞火花/击中溅射。
    /// </summary>
    public void EmitHitSparks(Vector2 position, Vector2 normal, Color color, int count = 10)
    {
        float baseAngle = MathF.Atan2(normal.Y, normal.X);
        for (int i = 0; i < count; i++)
        {
            float spread = (float)(_rand.NextDouble() - 0.5) * 1.6f;
            float angle = baseAngle + spread;
            float speed = (float)(_rand.NextDouble() * 320 + 120);
            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

            AddParticle(new Particle
            {
                Position = position,
                Velocity = vel,
                Drag = 0.91f,
                Gravity = 500f,
                StartColor = color,
                EndColor = color * 0.2f,
                StartSize = (float)(_rand.NextDouble() * 4 + 3),
                EndSize = 0f,
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.30 + 0.12),
                Shape = ParticleShape.LineSpark
            });
        }
    }

    /// <summary>
    /// 起跳或着陆时喷发轻柔的烟尘气团粒子。
    /// </summary>
    public void EmitJumpDust(Vector2 position, Color color, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float vx = (float)(_rand.NextDouble() - 0.5) * 180f;
            float vy = -(float)(_rand.NextDouble() * 60 + 20);

            AddParticle(new Particle
            {
                Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 12f, 0f),
                Velocity = new Vector2(vx, vy),
                Drag = 0.88f,
                Gravity = -40f, // 烟尘轻微向上飘散
                StartColor = color * 0.65f,
                EndColor = Color.Transparent,
                StartSize = (float)(_rand.NextDouble() * 8 + 6),
                EndSize = (float)(_rand.NextDouble() * 14 + 10),
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.28 + 0.18),
                Shape = ParticleShape.Circle
            });
        }
    }

    /// <summary>
    /// 贴墙缓缓下滑时的摩擦火星微粒。
    /// </summary>
    public void EmitWallSlideFriction(Vector2 position, Vector2 wallNormal, Color color)
    {
        float vx = wallNormal.X * (float)(_rand.NextDouble() * 60 + 20);
        float vy = -(float)(_rand.NextDouble() * 80 + 30);

        AddParticle(new Particle
        {
            Position = position,
            Velocity = new Vector2(vx, vy),
            Drag = 0.90f,
            Gravity = 300f,
            StartColor = Color.Lerp(color, Color.White, 0.5f),
            EndColor = Color.Transparent,
            StartSize = 3.5f,
            EndSize = 0f,
            Lifetime = 0f,
            MaxLifetime = (float)(_rand.NextDouble() * 0.22 + 0.10),
            Shape = ParticleShape.LineSpark
        });
    }

    /// <summary>
    /// 发生范围爆炸粒子特效与扩张冲击波环。
    /// </summary>
    public void EmitExplosion(Vector2 position, float radius, Color color, int shardCount = 22)
    {
        // 冲击波扩散环
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White,
            EndColor = color * 0f,
            StartSize = 10f,
            EndSize = radius * 2.2f,
            Lifetime = 0f,
            MaxLifetime = 0.28f,
            Shape = ParticleShape.Ring
        });

        // 放射状火球碎屑
        for (int i = 0; i < shardCount; i++)
        {
            float angle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
            float speed = (float)(_rand.NextDouble() * (radius * 4.5f) + 80f);
            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

            AddParticle(new Particle
            {
                Position = position,
                Velocity = vel,
                Drag = 0.90f,
                Gravity = 280f,
                StartColor = (i % 2 == 0) ? Color.White : color,
                EndColor = ColorPalette.ExplosiveOrange * 0f,
                StartSize = (float)(_rand.NextDouble() * 8 + 5),
                EndSize = 1f,
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.45 + 0.20),
                Shape = (i % 3 == 0) ? ParticleShape.Circle : ParticleShape.Square
            });
        }
    }

    /// <summary>
    /// 副武器格挡力场偏转能量环。
    /// </summary>
    public void EmitShieldDeflectWave(Vector2 position, float radius, Color color)
    {
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = color,
            EndColor = color * 0f,
            StartSize = radius * 0.5f,
            EndSize = radius * 2.5f,
            Lifetime = 0f,
            MaxLifetime = 0.25f,
            Shape = ParticleShape.Ring
        });
    }

    /// <summary>
    /// 冲刺残影与尾焰粒子。
    /// </summary>
    public void EmitDashTrail(Vector2 position, Vector2 velocity, Color color)
    {
        AddParticle(new Particle
        {
            Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 6f, (float)(_rand.NextDouble() - 0.5) * 6f),
            Velocity = -velocity * 0.12f + new Vector2((float)(_rand.NextDouble() - 0.5) * 30f, (float)(_rand.NextDouble() - 0.5) * 30f),
            Drag = 0.86f,
            Gravity = 0f,
            StartColor = color * 0.8f,
            EndColor = color * 0f,
            StartSize = 14f,
            EndSize = 2f,
            Lifetime = 0f,
            MaxLifetime = 0.18f,
            Shape = ParticleShape.Circle
        });
    }

    /// <summary>
    /// 清除所有当前粒子。
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
    }
}
