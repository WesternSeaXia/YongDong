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
    public float PeakSize = 0f; // 若 > 0，则先在前 22% 时间迅速膨胀至 PeakSize，之后平滑收敛消散至 EndSize

    public float Lifetime;
    public float MaxLifetime;
    public ParticleShape Shape;

    public float AlphaPower = 2f;
    public Vector2? AttractorPosition;
    public float AttractorStrength;

    public bool IsAlive => Lifetime < MaxLifetime;

    public void Update(float dt)
    {
        if (Lifetime < 0f)
        {
            Lifetime += dt;
            if (Lifetime < 0f) return;
            dt = Lifetime;
        }
        else
        {
            Lifetime += dt;
        }

        if (AttractorPosition.HasValue)
        {
            Vector2 toCenter = AttractorPosition.Value - Position;
            float dist = toCenter.Length();
            if (dist < 12f)
            {
                // 到达引力奇点，直接被黑洞视界吞噬湮灭
                Lifetime = MaxLifetime;
                return;
            }
            Vector2 dir = toCenter / dist;
            float pullAccel = AttractorStrength / MathF.Max(dist, 18f);
            Velocity += dir * (pullAccel * dt);
        }
        else
        {
            Velocity.Y += Gravity * dt;
        }

        Velocity *= MathF.Pow(Drag, dt * 60f);
        Position += Velocity * dt;
        Rotation += AngularVelocity * dt;
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (Lifetime < 0f) return;

        float t = MathHelper.Clamp(Lifetime / MaxLifetime, 0f, 1f);
        float alpha = AlphaPower switch
        {
            1f => 1f - t,
            0.5f => MathF.Sqrt(MathF.Max(0f, 1f - t)),
            _ => (1f - t) * (1f - t)
        };
        Color color = Color.Lerp(StartColor, EndColor, t) * alpha;

        float size;
        if (PeakSize > 0f)
        {
            // ROUNDS 经典卡通圆形展开与收缩消散曲线：前 22% 时间以正弦曲线爆发膨胀至峰值，后 78% 平滑快速收敛消解
            const float growRatio = 0.22f;
            if (t < growRatio)
            {
                size = MathHelper.Lerp(StartSize, PeakSize, MathF.Sin((t / growRatio) * MathHelper.PiOver2));
            }
            else
            {
                float shrinkT = (t - growRatio) / (1f - growRatio);
                size = MathHelper.Lerp(PeakSize, EndSize, shrinkT * shrinkT);
            }
        }
        else
        {
            size = MathHelper.Lerp(StartSize, EndSize, t);
        }

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
    private const int MaxParticles = 800;
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
    /// 触发空中二段跳时的空气冲击波环与脚底气浪爆发特效。
    /// </summary>
    public void EmitAirJump(Vector2 position, Color color)
    {
        // 1. 空气爆散冲击环（白色至透明，向下缓移）
        AddParticle(new Particle
        {
            Position = position,
            Velocity = new Vector2(0f, 35f),
            Drag = 0.92f,
            Gravity = 0f,
            StartColor = Color.White * 0.95f,
            EndColor = color * 0f,
            StartSize = 10f,
            EndSize = 48f,
            Lifetime = 0f,
            MaxLifetime = 0.22f,
            Shape = ParticleShape.Ring
        });

        // 2. 向下放射状喷射的气浪微粒
        for (int i = 0; i < 9; i++)
        {
            float vx = (float)(_rand.NextDouble() - 0.5) * 180f;
            float vy = (float)(_rand.NextDouble() * 110 + 40);

            AddParticle(new Particle
            {
                Position = position + new Vector2((float)(_rand.NextDouble() - 0.5) * 16f, 0f),
                Velocity = new Vector2(vx, vy),
                Drag = 0.88f,
                Gravity = 120f,
                StartColor = Color.Lerp(color, Color.White, 0.45f) * 0.85f,
                EndColor = Color.Transparent,
                StartSize = (float)(_rand.NextDouble() * 7 + 4),
                EndSize = 0.5f,
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.24 + 0.14),
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
    /// 触发《ROUNDS》风格矢量爆炸特效：
    /// 1. 效果范围与杀伤作用范围（radius）严格一致：由多朵类似毒云气团的小圆圈紧凑交叠，外沿刚好完整覆盖杀伤半径；
    /// 2. 单个圆圈小巧精致适中，瞬时爆开并在 0.18 秒内迅速收缩湮灭，不持续停留；
    /// 3. 火光消散后留存数朵小巧黑烟圈向上微浮消解；已完全去除外围流光。
    /// </summary>
    public void EmitExplosion(Vector2 position, float radius, Color color, int shardCount = 8)
    {
        float effectRadius = MathF.Max(radius, 40f);

        // 1. 爆心小巧瞬时高光白闪（0.06 秒）
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White * 0.95f,
            EndColor = Color.Transparent,
            StartSize = effectRadius * 0.25f,
            EndSize = effectRadius * 0.65f,
            Lifetime = 0f,
            MaxLifetime = 0.06f,
            Shape = ParticleShape.Circle
        });

        // 2. 效果范围内：由多朵类似毒云的紧凑小气团交叠覆盖杀伤范围
        // 单个圈小巧（半径约 0.32R~0.38R），外沿(分布距 0.65R + 半径 0.35R)刚好达到 1.0R，与伤害判定范围完全一致！
        // 2.1 爆心小巧主核气团
        AddParticle(new Particle
        {
            Position = position,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = new Color(255, 235, 60),
            EndColor = new Color(255, 120, 20, 0),
            StartSize = 8f,
            PeakSize = effectRadius * 0.75f, // 直径 0.75R，半径仅 0.375R
            EndSize = 0f,
            Lifetime = 0f,
            MaxLifetime = 0.18f,
            AlphaPower = 1.4f,
            Shape = ParticleShape.Circle
        });

        // 2.2 围绕爆心分布的 6~8 朵小巧橙黄气团组合（类似毒云花瓣，交叠出 Q 弹爆炸质感并铺满杀伤范围）
        int puffCount = _rand.Next(6, 9);
        for (int i = 0; i < puffCount; i++)
        {
            float puffAngle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
            // 距离：0.35R ~ 0.65R，搭配 0.35R 的半径，整体外沿恰好触及 1.0R
            float puffDist = effectRadius * (0.35f + (float)_rand.NextDouble() * 0.30f);
            Vector2 puffPos = position + new Vector2(MathF.Cos(puffAngle), MathF.Sin(puffAngle)) * puffDist;

            float puffDiameter = effectRadius * (0.62f + (float)_rand.NextDouble() * 0.16f); // 直径约 0.7R (半径 0.35R)

            double roll = _rand.NextDouble();
            Color puffColor = (roll < 0.40) ? new Color(255, 230, 50)   // 亮金黄
                            : (roll < 0.75) ? new Color(255, 125, 20)   // 鲜艳爆炸橙
                                            : new Color(255, 75, 12);   // 深焰红橙

            AddParticle(new Particle
            {
                Position = puffPos,
                Velocity = Vector2.Zero,
                Drag = 1f,
                Gravity = 0f,
                StartColor = puffColor,
                EndColor = puffColor * 0f,
                StartSize = 6f,
                PeakSize = puffDiameter,
                EndSize = 0f,
                Lifetime = -(float)(_rand.NextDouble() * 0.02), // 细微错位膨胀
                MaxLifetime = (float)(_rand.NextDouble() * 0.04 + 0.16), // 0.16s ~ 0.20s 快速收缩消解，不持续留存
                AlphaPower = 1.4f,
                Shape = ParticleShape.Circle
            });
        }

        // 3. 炸完之后的精巧黑烟雾（2~3 朵小巧黑圈，向上微浮并缩小消散）
        int smokeCount = _rand.Next(2, 4);
        for (int i = 0; i < smokeCount; i++)
        {
            float smokeAngle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
            float smokeDist = effectRadius * (float)(_rand.NextDouble() * 0.35);
            Vector2 smokePos = position + new Vector2(MathF.Cos(smokeAngle), MathF.Sin(smokeAngle)) * smokeDist;

            float vx = (float)(_rand.NextDouble() - 0.5) * 16f;
            float vy = -(float)(_rand.NextDouble() * 22f + 10f);
            Vector2 smokeVel = new Vector2(vx, vy);

            float smokeDiameter = effectRadius * (0.45f + (float)_rand.NextDouble() * 0.20f);

            AddParticle(new Particle
            {
                Position = smokePos,
                Velocity = smokeVel,
                Drag = 0.94f,
                Gravity = -16f,
                StartColor = new Color(20, 20, 24, 230), // 深炭黑
                EndColor = new Color(15, 15, 18, 0),
                StartSize = 8f,
                PeakSize = smokeDiameter,
                EndSize = 2f,
                Lifetime = -(float)(_rand.NextDouble() * 0.04 + 0.06), // 延时在火光收缩时浮现
                MaxLifetime = (float)(_rand.NextDouble() * 0.18 + 0.42),
                AlphaPower = 1.2f,
                Shape = ParticleShape.Circle
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
    /// 在毒云弥漫范围内随机位置生成向上缓漂且随生命周期逐渐缩小的毒素气泡、微环与腐蚀孢子微粒。
    /// </summary>
    public void EmitToxicCloudSpore(Vector2 center, float radius)
    {
        float angle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
        float dist = radius * MathF.Sqrt((float)_rand.NextDouble()) * 0.94f;
        Vector2 spawnPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

        double roll = _rand.NextDouble();
        if (roll < 0.35)
        {
            // 1. 毒素气泡/孢子：在毒云范围内随机浮现，尺寸随时间逐渐减小至 0
            float startSize = (float)(_rand.NextDouble() * 9 + 8); // 8px ~ 17px
            AddParticle(new Particle
            {
                Position = spawnPos,
                Velocity = new Vector2((float)(_rand.NextDouble() - 0.5) * 20f, -(float)(_rand.NextDouble() * 32f + 10f)),
                Drag = 0.94f,
                Gravity = -15f,
                StartColor = Color.Lerp(ColorPalette.PoisonGreen, new Color(140, 255, 60), (float)_rand.NextDouble()) * 0.85f,
                EndColor = ColorPalette.PoisonGreen * 0f,
                StartSize = startSize,
                EndSize = 0f, // 慢慢变小直至消失
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.38 + 0.32),
                AlphaPower = 1f,
                Shape = ParticleShape.Circle
            });
        }
        else if (roll < 0.65)
        {
            // 2. 烟雾弹翻滚烟团微粒：轻柔半透明的大号游离烟团，低不透明度，绝不遮蔽前景
            float startSize = (float)(_rand.NextDouble() * 12 + 16); // 16px ~ 28px
            AddParticle(new Particle
            {
                Position = spawnPos,
                Velocity = new Vector2((float)(_rand.NextDouble() - 0.5) * 24f, -(float)(_rand.NextDouble() * 18f + 6f)),
                Drag = 0.95f,
                Gravity = -8f,
                StartColor = Color.Lerp(ColorPalette.PoisonGreen, new Color(50, 180, 80), 0.35f) * 0.28f,
                EndColor = ColorPalette.PoisonGreen * 0f,
                StartSize = startSize,
                EndSize = 4f, // 翻滚漂移中慢慢收缩消散
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.40 + 0.45),
                AlphaPower = 1f,
                Shape = ParticleShape.Circle
            });
        }
        else if (roll < 0.85)
        {
            // 3. 毒素轻泡微环：向内收缩微环
            float startSize = (float)(_rand.NextDouble() * 10 + 10); // 10px ~ 20px
            AddParticle(new Particle
            {
                Position = spawnPos,
                Velocity = new Vector2((float)(_rand.NextDouble() - 0.5) * 14f, -(float)(_rand.NextDouble() * 22f + 6f)),
                Drag = 0.93f,
                Gravity = -10f,
                StartColor = Color.Lerp(ColorPalette.PoisonGreen, Color.White, 0.25f) * 0.70f,
                EndColor = ColorPalette.PoisonGreen * 0f,
                StartSize = startSize,
                EndSize = 1.0f, // 慢慢变小
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.32 + 0.28),
                AlphaPower = 1f,
                Shape = ParticleShape.Ring
            });
        }
        else
        {
            // 4. 腐蚀微闪微粒：细小火星快速收缩
            AddParticle(new Particle
            {
                Position = spawnPos,
                Velocity = new Vector2((float)(_rand.NextDouble() - 0.5) * 45f, (float)(_rand.NextDouble() - 0.5) * 45f - 12f),
                Drag = 0.90f,
                Gravity = 30f,
                StartColor = new Color(170, 255, 90),
                EndColor = ColorPalette.PoisonGreen * 0f,
                StartSize = (float)(_rand.NextDouble() * 4 + 3),
                EndSize = 0f, // 慢慢变小
                Lifetime = 0f,
                MaxLifetime = (float)(_rand.NextDouble() * 0.24 + 0.16),
                AlphaPower = 1f,
                Shape = ParticleShape.LineSpark
            });
        }
    }

    /// <summary>
    /// 触发黑洞引力坍缩专属特效：自外围向中心剧烈收缩的多重坍缩环、向引力奇点加速吸入的螺旋光线与暗影核心。
    /// 视觉参数设定为 75px (直径 150px)。
    /// </summary>
    public void EmitBlackHoleImplosion(Vector2 center, float radius = 75f, Color? customColor = null)
    {
        Color themeColor = customColor ?? ColorPalette.ElectricPurple;
        Color coreColor = Color.Lerp(themeColor, Color.White, 0.45f);
        Color darkSingularity = new(18, 6, 32);

        // 0. 引力生效边界标记光环（精准标记 radius = 75px 物理有效范围，直径 = radius * 2.0 = 150px）
        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.Lerp(themeColor, Color.White, 0.35f) * 0.90f,
            EndColor = themeColor * 0f,
            StartSize = radius * 2.0f, // 精确对应 radius = 75px (150px 直径)
            EndSize = radius * 2.0f,
            Lifetime = 0f,
            MaxLifetime = 0.25f,
            AlphaPower = 1f,
            Shape = ParticleShape.Ring
        });

        // 1. 三重向心收缩视界环（从 75px 边界以极高向心速度聚缩至中心奇点）
        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = themeColor * 0.95f,
            EndColor = Color.White,
            StartSize = radius * 2.0f, // 从 75px 边界开始收缩
            EndSize = 2f,              // 坍缩至奇点
            Lifetime = 0f,
            MaxLifetime = 0.25f,
            AlphaPower = 0.5f,
            Shape = ParticleShape.Ring
        });

        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = coreColor * 0.90f,
            EndColor = themeColor * 0.2f,
            StartSize = radius * 1.45f, // 145px 相对半径中间波
            EndSize = 2f,
            Lifetime = -0.06f,
            MaxLifetime = 0.22f,
            AlphaPower = 0.5f,
            Shape = ParticleShape.Ring
        });

        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White * 0.95f,
            EndColor = themeColor * 0f,
            StartSize = radius * 0.95f, // 95px 核心收束波
            EndSize = 1f,
            Lifetime = -0.12f,
            MaxLifetime = 0.20f,
            AlphaPower = 0.5f,
            Shape = ParticleShape.Ring
        });

        // 2. 奇点暗核与光子层（黑洞中心吞噬光线的核心视界）
        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = darkSingularity * 0.98f,
            EndColor = Color.Transparent,
            StartSize = 16f,
            EndSize = 2f,
            Lifetime = 0f,
            MaxLifetime = 0.32f,
            Shape = ParticleShape.Circle
        });

        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = coreColor * 0.85f,
            EndColor = Color.Transparent,
            StartSize = 22f,
            EndSize = 4f,
            Lifetime = -0.04f,
            MaxLifetime = 0.28f,
            Shape = ParticleShape.Circle
        });

        // 终结微光闪烁（奇点彻底坍缩消散）
        AddParticle(new Particle
        {
            Position = center,
            Velocity = Vector2.Zero,
            Drag = 1f,
            Gravity = 0f,
            StartColor = Color.White * 0.95f,
            EndColor = Color.Transparent,
            StartSize = 7f,
            EndSize = 0.5f,
            Lifetime = -0.22f,
            MaxLifetime = 0.12f,
            Shape = ParticleShape.Circle
        });

        // 3. 向心吸附螺旋光线与星屑（42 条光斑/光线受向心引力场拉扯高速奔向奇点）
        // 粒子的生成范围精确限制在引力场内部 (0.35R ~ 1.0R，即 26px ~ 75px)
        const int streamCount = 42;
        for (int i = 0; i < streamCount; i++)
        {
            int wave = i % 3;
            float delay = wave * 0.055f;
            float baseAngle = (i / (float)streamCount) * MathHelper.TwoPi + (float)(_rand.NextDouble() - 0.5) * 0.35f;
            float spawnDist = radius * (float)(_rand.NextDouble() * 0.65 + 0.35); // 0.35R ~ 1.0R (严格位于 75px 边界内)

            Vector2 spawnPos = center + new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle)) * spawnDist;
            Vector2 toCenter = center - spawnPos;
            float toCenterDist = toCenter.Length();
            Vector2 inwardDir = toCenterDist > 0.001f ? toCenter / toCenterDist : -Vector2.UnitY;
            Vector2 tangentDir = new Vector2(-inwardDir.Y, inwardDir.X); // 吸积盘自转方向

            float inwardSpeed = (float)(_rand.NextDouble() * 110 + 70);
            float swirlSpeed = (float)(_rand.NextDouble() * 150 + 90);
            Vector2 vel = inwardDir * inwardSpeed + tangentDir * swirlSpeed;

            Color pColor = (i % 3 == 0) ? Color.White : (i % 2 == 0 ? themeColor : coreColor);

            AddParticle(new Particle
            {
                Position = spawnPos,
                Velocity = vel,
                Drag = 0.94f,
                Gravity = 0f,
                StartColor = pColor,
                EndColor = themeColor * 0.15f,
                StartSize = (float)(_rand.NextDouble() * 4.5 + 3.5),
                EndSize = 0.5f,
                Lifetime = -delay,
                MaxLifetime = (float)(_rand.NextDouble() * 0.18 + 0.32),
                AlphaPower = 1f,
                Shape = (i % 5 == 0) ? ParticleShape.Circle : ParticleShape.LineSpark,
                AttractorPosition = center,
                AttractorStrength = 240000f
            });
        }
    }

    /// <summary>
    /// 清除所有当前粒子。
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
    }
}
