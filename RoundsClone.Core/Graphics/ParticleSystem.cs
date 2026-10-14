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
                    Vector2 tail = Position - dir * MathF.Min(size * 2.8f, velLen * 0.08f);
                    batcher.DrawLine(spriteBatch, tail, Position, color, Math.Max(1.5f, size * 0.4f));
                }
                else
                {
                    batcher.DrawFilledCircle(spriteBatch, Position, size * 0.4f, color);
                }
                break;
            case ParticleShape.Ring:
                batcher.DrawCircleOutline(spriteBatch, Position, size * 0.5f, color, Math.Max(2.0f, (1f - t) * 4.5f));
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
    private const int MaxParticles = 550;
    private readonly List<Particle> _particles = new(512);
    private readonly Random _rand = new();

    /// <summary>
    /// 更新所有存活粒子的物理动力学行为。
    /// </summary>
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
            _particles.RemoveAt(0);
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
            float speed = (float)(_rand.NextDouble() * 340 + 140);
            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

            AddParticle(new Particle
            {
                Position = position,
                Velocity = vel,
                Drag = 0.91f,
                Gravity = 450f,
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
    /// 发射子弹飞行过程中的柔和发光拖尾微粒。
    /// </summary>
    public void EmitProjectileTrail(Vector2 position, Vector2 velocity, Color color)
    {
        AddParticle(new Particle
        {
            Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 3f, (float)(_rand.NextDouble() - 0.5) * 3f),
            Velocity = -velocity * 0.08f + new Vector2((float)(_rand.NextDouble() - 0.5) * 20f, (float)(_rand.NextDouble() - 0.5) * 20f),
            Drag = 0.88f,
            Gravity = 0f,
            StartColor = Color.Lerp(color, Color.White, 0.4f) * 0.75f,
            EndColor = color * 0f,
            StartSize = 4.2f,
            EndSize = 0.5f,
            Lifetime = 0f,
            MaxLifetime = 0.16f,
            Shape = ParticleShape.Circle
        });
    }

    /// <summary>
    /// 起跳或着陆时喷发轻柔的烟尘气团粒子。
    /// </summary>
    public void EmitJumpDust(Vector2 position, Color color, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float vx = (float)(_rand.NextDouble() - 0.5) * 180f;
            float vy = -(float)(_rand.NextDouble() * 70 + 20);

            AddParticle(new Particle
            {
                Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 14f, 0f),
                Velocity = new Vector2(vx, vy),
                Drag = 0.88f,
                Gravity = -30f,
                StartColor = color * 0.65f,
                EndColor = Color.Transparent,
                StartSize = (float)(_rand.NextDouble() * 8 + 6),
                EndSize = (float)(_rand.NextDouble() * 15 + 10),
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.30 + 0.18),
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
    /// 触发高辨识度、多层冲击波与碎片闪光的剧烈爆炸粒子特效。
    /// </summary>
    public void EmitExplosion(Vector2 position, float radius, Color color, int shardCount = 36)
    {
        float effectRadius = MathF.Max(radius, 60f);

        // 1. 核心高光致盲白闪球
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White,
            EndColor = Color.Transparent,
            StartSize = effectRadius * 0.9f,
            EndSize = effectRadius * 1.6f,
            Lifetime = 0f,
            MaxLifetime = 0.14f,
            Shape = ParticleShape.Circle
        });

        // 2. 双重扩张冲击波环（内层白光，外层主题霓虹光）
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White * 0.95f,
            EndColor = color * 0f,
            StartSize = 12f,
            EndSize = effectRadius * 2.4f,
            Lifetime = 0f,
            MaxLifetime = 0.26f,
            Shape = ParticleShape.Ring
        });

        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = color * 0.9f,
            EndColor = ColorPalette.ExplosiveOrange * 0f,
            StartSize = 18f,
            EndSize = effectRadius * 2.8f,
            Lifetime = 0f,
            MaxLifetime = 0.35f,
            Shape = ParticleShape.Ring
        });

        // 3. 密集高速火花射线与燃烧碎屑
        for (int i = 0; i < shardCount; i++)
        {
            float angle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
            float speed = (float)(_rand.NextDouble() * (effectRadius * 6.5f) + 120f);
            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

            Color sparkColor = (i % 3 == 0) ? Color.White : (i % 2 == 0 ? color : ColorPalette.ExplosiveOrange);

            AddParticle(new Particle
            {
                Position = position,
                Velocity = vel,
                Drag = 0.89f,
                Gravity = 280f,
                StartColor = sparkColor,
                EndColor = Color.Transparent,
                StartSize = (float)(_rand.NextDouble() * 7 + 4),
                EndSize = 0.5f,
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.42 + 0.22),
                Shape = (i % 4 == 0) ? ParticleShape.Circle : (i % 3 == 0 ? ParticleShape.Square : ParticleShape.LineSpark)
            });
        }
    }

    /// <summary>
    /// 冲刺流光残影与尾焰粒子。
    /// </summary>
    public void EmitDashTrail(Vector2 position, Vector2 velocity, Color color)
    {
        AddParticle(new Particle
        {
            Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 8f, (float)(_rand.NextDouble() - 0.5) * 8f),
            Velocity = -velocity * 0.15f + new Vector2((float)(_rand.NextDouble() - 0.5) * 36f, (float)(_rand.NextDouble() - 0.5) * 36f),
            Drag = 0.84f,
            Gravity = 0f,
            StartColor = Color.Lerp(color, Color.White, 0.35f) * 0.85f,
            EndColor = color * 0f,
            StartSize = 16f,
            EndSize = 2f,
            Lifetime = 0f,
            MaxLifetime = 0.22f,
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
