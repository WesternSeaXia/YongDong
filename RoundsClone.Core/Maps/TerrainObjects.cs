using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 地形组件通用基类，封装生命值、破损破裂逻辑、受击碎屑与通用物理属性。
/// </summary>
public abstract class BaseTerrainObject : ITerrainObject
{
    public abstract Collider Collider { get; }
    public RigidBody2D Body => Collider.Body;

    public TerrainStyle Style { get; set; } = TerrainStyle.SolidMetal;
    public bool IsStatic => Body.IsStatic;

    public bool IsDestructible { get; set; } = false;
    public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth { get; set; } = 100f;
    public bool IsDestroyed { get; protected set; } = false;

    public RopeConstraint? AttachedRope { get; set; }

    // 预计算的破坏裂纹段，生命值降低时逐步显现
    protected readonly List<(Vector2 Start, Vector2 End)> CrackLines = new();

    public virtual void TakeDamage(float damage, ParticleSystem particles, ScreenShake shake)
    {
        if (!IsDestructible || IsDestroyed) return;

        CurrentHealth = MathF.Max(0f, CurrentHealth - damage);

        // 喷射材质碎屑
        Color debrisColor = TerrainStyleHelper.GetOutlineColor(Style);
        particles.EmitHitSparks(Body.Position, -Vector2.UnitY, debrisColor, 8);

        if (CurrentHealth <= 0f)
        {
            IsDestroyed = true;
            Collider.IsEnabled = false;

            // 碎石碎屑脱落与轻微崩塌散落（无刺眼爆炸闪光与膨胀光圈）
            particles.EmitHitSparks(Body.Position, -Vector2.UnitY, debrisColor, 16);
            particles.EmitHitSparks(Body.Position, Vector2.UnitX, debrisColor, 8);
            particles.EmitHitSparks(Body.Position, -Vector2.UnitX, debrisColor, 8);
            shake.AddTrauma(0.08f);
        }
    }

    public virtual void Update(float dt)
    {
        // 基础类供子类重写物理驱动逻辑
    }

    public abstract void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch);

    /// <summary>
    /// 绘制通用受损裂纹与简易耐久度微指示条。
    /// </summary>
    protected void DrawDamageIndicators(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 center, float width)
    {
        if (!IsDestructible || IsDestroyed) return;

        float hpRatio = MathHelper.Clamp(CurrentHealth / MaxHealth, 0f, 1f);

        // 绘制破裂暗纹
        if (hpRatio < 0.99f && CrackLines.Count > 0)
        {
            int showCount = (int)MathF.Ceiling((1f - hpRatio) * CrackLines.Count);
            Color crackCol = Color.Black * 0.75f;
            for (int i = 0; i < showCount && i < CrackLines.Count; i++)
            {
                batcher.DrawLine(spriteBatch, Body.Position + CrackLines[i].Start, Body.Position + CrackLines[i].End, crackCol, 2f);
            }
        }

        // 生命值不满时显示迷你战术血条
        if (hpRatio < 0.98f)
        {
            float barW = MathF.Min(width * 0.75f, 60f);
            float barH = 5f;
            Vector2 barPos = new(center.X - barW * 0.5f, center.Y - 26f);

            batcher.DrawRectangle(spriteBatch, new Rectangle((int)barPos.X, (int)barPos.Y, (int)barW, (int)barH), Color.Black * 0.6f);
            batcher.DrawRectangle(spriteBatch, new Rectangle((int)barPos.X, (int)barPos.Y, (int)(barW * hpRatio), (int)barH), Color.Lerp(Color.Red, Color.LimeGreen, hpRatio));
        }
    }
}

/// <summary>
/// 矩形地形（支持静态墙体/平台、或带重力与质量的动态箱子、可挂接绳索吊链、可配置生命值）。
/// </summary>
public class BoxTerrain : BaseTerrainObject
{
    public BoxCollider BoxCol { get; }
    public override Collider Collider => BoxCol;

    public Vector2 Size => BoxCol.Size;
    public Rectangle Bounds => BoxCol.Bounds;

    /// <summary>
    /// 创建绝对静态矩形墙体/平台。
    /// </summary>
    public BoxTerrain(Rectangle rect, TerrainStyle style = TerrainStyle.SolidMetal, bool destructible = false, float hp = 120f)
    {
        Style = style;
        IsDestructible = destructible;
        MaxHealth = hp;
        CurrentHealth = hp;

        var body = new RigidBody2D(new Vector2(rect.Center.X, rect.Center.Y), 0f)
        {
            Restitution = TerrainStyleHelper.GetBounciness(style)
        };
        BoxCol = new BoxCollider(body, new Vector2(rect.Width, rect.Height))
        {
            BouncinessOverride = TerrainStyleHelper.GetBounciness(style)
        };

        GenerateCracks(rect.Width, rect.Height);
    }

    /// <summary>
    /// 创建具有重力和真实物理特性的动态物理箱子（如挂在天花板上的吊装集装箱、可被子弹击退晃动的木箱）。
    /// </summary>
    public BoxTerrain(Vector2 center, Vector2 size, float mass, TerrainStyle style = TerrainStyle.SolidMetal, float restitution = 0.25f, float drag = 0.02f)
    {
        Style = style;

        var body = new RigidBody2D(center, mass)
        {
            Restitution = restitution,
            Drag = drag
        };
        BoxCol = new BoxCollider(body, size)
        {
            BouncinessOverride = restitution
        };

        GenerateCracks((int)size.X, (int)size.Y);
    }

    /// <summary>
    /// 挂接到世界固定吊点（构建钟摆或吊装物理箱）。
    /// </summary>
    public RopeConstraint AttachRope(Vector2 worldAnchor, float ropeLength, Vector2? localAnchor = null)
    {
        Vector2 anchor = localAnchor ?? new Vector2(0f, -Size.Y * 0.5f);
        AttachedRope = new RopeConstraint(worldAnchor, Body, anchor, ropeLength);
        return AttachedRope;
    }

    private void GenerateCracks(int w, int h)
    {
        var rand = new Random(w * 37 + h * 91);
        float hw = w * 0.4f;
        float hh = h * 0.4f;
        for (int i = 0; i < 5; i++)
        {
            Vector2 s = new((float)(rand.NextDouble() - 0.5) * hw, (float)(rand.NextDouble() - 0.5) * hh);
            Vector2 e = s + new Vector2((float)(rand.NextDouble() - 0.5) * hw, (float)(rand.NextDouble() - 0.5) * hh);
            CrackLines.Add((s, e));
        }
    }

    public override void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        // 若挂载了吊链，先绘制钢链
        if (AttachedRope != null)
        {
            batcher.DrawChain(spriteBatch, AttachedRope.WorldAnchor, AttachedRope.GetBodyAnchorWorldPos(), Color.SlateGray, 11f);
        }

        Rectangle rect = BoxCol.Bounds;
        Color fill = TerrainStyleHelper.GetFillColor(Style);
        Color outline = TerrainStyleHelper.GetOutlineColor(Style);

        // 主体填充与外边框
        batcher.DrawRoundedRect(spriteBatch, rect, 4f, fill);
        batcher.DrawRectangleOutline(spriteBatch, rect, outline, 2.5f);

        // 动态物理箱内部加固斜角 X 纹理（突出工业机械吊装感）
        if (!IsStatic)
        {
            Vector2 tl = new(rect.Left + 5, rect.Top + 5);
            Vector2 br = new(rect.Right - 5, rect.Bottom - 5);
            Vector2 tr = new(rect.Right - 5, rect.Top + 5);
            Vector2 bl = new(rect.Left + 5, rect.Bottom - 5);
            batcher.DrawLine(spriteBatch, tl, br, outline * 0.35f, 2f);
            batcher.DrawLine(spriteBatch, tr, bl, outline * 0.35f, 2f);
        }

        DrawDamageIndicators(batcher, spriteBatch, Body.Position, rect.Width);
    }
}

/// <summary>
/// 圆形地形（如弹球碰撞球、圆形掩体、摇摆重锤）。
/// </summary>
public class CircleTerrain : BaseTerrainObject
{
    public CircleCollider CircleCol { get; }
    public override Collider Collider => CircleCol;
    public float Radius => CircleCol.Radius;

    public CircleTerrain(Vector2 center, float radius, TerrainStyle style = TerrainStyle.BouncyRubber, bool isStatic = true, float mass = 0f, bool destructible = false, float hp = 80f)
    {
        Style = style;
        IsDestructible = destructible;
        MaxHealth = hp;
        CurrentHealth = hp;

        var body = new RigidBody2D(center, isStatic ? 0f : (mass > 0f ? mass : 4f))
        {
            Restitution = TerrainStyleHelper.GetBounciness(style),
            Drag = isStatic ? 0.08f : 0.02f
        };
        CircleCol = new CircleCollider(body, radius)
        {
            BouncinessOverride = TerrainStyleHelper.GetBounciness(style)
        };

        var rand = new Random((int)radius * 43);
        for (int i = 0; i < 4; i++)
        {
            float a1 = (float)(rand.NextDouble() * MathHelper.TwoPi);
            float a2 = a1 + (float)(rand.NextDouble() * 1.5 - 0.75);
            CrackLines.Add((
                new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (radius * 0.3f),
                new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * (radius * 0.85f)
            ));
        }
    }

    public override void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        if (AttachedRope != null)
        {
            batcher.DrawChain(spriteBatch, AttachedRope.WorldAnchor, AttachedRope.GetBodyAnchorWorldPos(), Color.SlateGray, 11f);
        }

        Vector2 pos = Body.Position;
        Color fill = TerrainStyleHelper.GetFillColor(Style);
        Color outline = TerrainStyleHelper.GetOutlineColor(Style);

        // 实心圆与高抗锯齿描边
        batcher.DrawFilledCircle(spriteBatch, pos, Radius, fill);
        batcher.DrawCircleOutline(spriteBatch, pos, Radius, outline, 2.5f);

        // 如果是橡胶高弹球，绘制内置同心光环
        if (Style == TerrainStyle.BouncyRubber)
        {
            batcher.DrawCircleOutline(spriteBatch, pos, Radius * 0.65f, Color.White * 0.6f, 2f);
            batcher.DrawFilledCircle(spriteBatch, pos, Radius * 0.25f, Color.White * 0.9f);
        }

        DrawDamageIndicators(batcher, spriteBatch, pos, Radius * 2f);
    }
}

/// <summary>
/// 凸多边形地形（支持三角形斜坡、梯形高台、斜切掩体及异形浮空岛）。
/// </summary>
public class PolygonTerrain : BaseTerrainObject
{
    public PolygonCollider PolyCol { get; }
    public override Collider Collider => PolyCol;

    public PolygonTerrain(Vector2[] worldVertices, TerrainStyle style = TerrainStyle.SolidMetal, bool isStatic = true, float mass = 0f, bool destructible = false, float hp = 150f)
    {
        Style = style;
        IsDestructible = destructible;
        MaxHealth = hp;
        CurrentHealth = hp;

        PolyCol = PolygonCollider.CreateFromWorldVertices(worldVertices, isStatic, mass);
        PolyCol.BouncinessOverride = TerrainStyleHelper.GetBounciness(style);
        PolyCol.Body.Restitution = TerrainStyleHelper.GetBounciness(style);

        var rand = new Random(worldVertices.Length * 59);
        for (int i = 0; i < 4; i++)
        {
            CrackLines.Add((
                new Vector2((float)(rand.NextDouble() - 0.5) * 30f, (float)(rand.NextDouble() - 0.5) * 30f),
                new Vector2((float)(rand.NextDouble() - 0.5) * 60f, (float)(rand.NextDouble() - 0.5) * 60f)
            ));
        }
    }

    public override void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        Color fill = TerrainStyleHelper.GetFillColor(Style);
        Color outline = TerrainStyleHelper.GetOutlineColor(Style);

        var verts = PolyCol.WorldVertices;
        batcher.DrawFilledConvexPolygon(spriteBatch, verts, fill);
        batcher.DrawPolygonOutline(spriteBatch, verts, outline, 2.5f);

        DrawDamageIndicators(batcher, spriteBatch, Body.Position, PolyCol.Bounds.Width);
    }
}

/// <summary>
/// 来回巡回运动平台（可配置端点坐标、运动周期、停留时间与缓动曲线，并在接触时准确传导动量载客）。
/// </summary>
public class MovingTerrain : BaseTerrainObject
{
    public BoxCollider BoxCol { get; }
    public override Collider Collider => BoxCol;

    public Vector2 StartPos { get; set; }
    public Vector2 EndPos { get; set; }
    public float Duration { get; set; } = 3.5f;
    public float PauseDuration { get; set; } = 0.5f;
    public MotionCurve Curve { get; set; } = MotionCurve.SmoothSine;

    private float _timer = 0f;

    public MovingTerrain(Vector2 startPos, Vector2 endPos, Vector2 size, float duration = 3.5f, MotionCurve curve = MotionCurve.SmoothSine, TerrainStyle style = TerrainStyle.SolidMetal)
    {
        StartPos = startPos;
        EndPos = endPos;
        Duration = MathF.Max(0.5f, duration);
        Curve = curve;
        Style = style;

        var body = new RigidBody2D(startPos, 0f)
        {
            Restitution = TerrainStyleHelper.GetBounciness(style)
        };
        BoxCol = new BoxCollider(body, size)
        {
            BouncinessOverride = TerrainStyleHelper.GetBounciness(style)
        };
    }

    public override void Update(float dt)
    {
        if (IsDestroyed) return;

        Vector2 oldPos = Body.Position;
        _timer += dt;

        Vector2 targetPos;
        switch (Curve)
        {
            case MotionCurve.SmoothSine:
            {
                // 正弦缓动，周期为 Duration * 2
                float phase = (_timer / Duration) * MathHelper.Pi;
                float t = (1f - MathF.Cos(phase)) * 0.5f;
                targetPos = Vector2.Lerp(StartPos, EndPos, t);
                break;
            }
            case MotionCurve.Linear:
            {
                float totalCycle = Duration * 2f;
                float cycleTime = _timer % totalCycle;
                float t = cycleTime < Duration ? (cycleTime / Duration) : (1f - (cycleTime - Duration) / Duration);
                targetPos = Vector2.Lerp(StartPos, EndPos, t);
                break;
            }
            case MotionCurve.PingPongWait:
            default:
            {
                float leg = Duration + PauseDuration;
                float cycle = _timer % (leg * 2f);
                if (cycle < Duration)
                {
                    float t = cycle / Duration;
                    float smoothT = MathHelper.SmoothStep(0f, 1f, t);
                    targetPos = Vector2.Lerp(StartPos, EndPos, smoothT);
                }
                else if (cycle < leg)
                {
                    targetPos = EndPos;
                }
                else if (cycle < leg + Duration)
                {
                    float t = (cycle - leg) / Duration;
                    float smoothT = MathHelper.SmoothStep(0f, 1f, t);
                    targetPos = Vector2.Lerp(EndPos, StartPos, smoothT);
                }
                else
                {
                    targetPos = StartPos;
                }
                break;
            }
        }

        // 计算运动学速度，驱动站立在其上方的角色一同平滑位移
        if (dt > 0.0001f)
        {
            Body.Velocity = (targetPos - oldPos) / dt;
        }
        Body.Position = targetPos;
    }

    public override void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDestroyed) return;

        // 1. 绘制运动导轨虚线/轨道指示线
        Color railColor = Color.DarkSlateGray * 0.55f;
        batcher.DrawLine(spriteBatch, StartPos, EndPos, railColor, 2f);
        batcher.DrawFilledCircle(spriteBatch, StartPos, 4f, railColor);
        batcher.DrawFilledCircle(spriteBatch, EndPos, 4f, railColor);

        // 2. 绘制移动平台主体
        Rectangle rect = BoxCol.Bounds;
        Color fill = TerrainStyleHelper.GetFillColor(Style);
        Color outline = TerrainStyleHelper.GetOutlineColor(Style);

        batcher.DrawRoundedRect(spriteBatch, rect, 4f, fill);
        batcher.DrawRectangleOutline(spriteBatch, rect, outline, 2.5f);

        // 3. 动力学机械齿轮指示标记
        batcher.DrawFilledCircle(spriteBatch, new Vector2(rect.Left + 10, rect.Center.Y), 4f, Color.Gold * 0.8f);
        batcher.DrawFilledCircle(spriteBatch, new Vector2(rect.Right - 10, rect.Center.Y), 4f, Color.Gold * 0.8f);
    }
}
