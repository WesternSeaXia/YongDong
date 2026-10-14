using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.SubWeapons;

/// <summary>
/// 反弹偏转副武器实现。
/// 职责边界：激活时角色周身展开透明球形力场罩，一道斜向斜切光源沿着球体表面扫过一条极具质感的球形阴影分界线与金属宝剑般璀璨的白光反光流光，
/// 并在命中中心瞬间爆发出元气骑士/像素宝剑标志性的高光十字星闪烁，期间将来袭子弹沿其来向完美弹反。
/// </summary>
public class BlockSubWeapon : ISubWeapon
{
    public string Id => "sub_block";
    public string Name => "力场弹反";
    public string Description => "展开瞬态球形白光偏转力场，将子弹沿其来向极速弹反。";
    public IconType Icon => IconType.Shield;

    public float Cooldown { get; set; } = 2.2f;
    public float CurrentCooldown { get; set; } = 0f;
    public bool IsReady => CurrentCooldown <= 0f;
    public float ActiveDuration => 0.28f;
    public bool IsActive => _activeTimer > 0f;

    /// <summary>
    /// 有效偏转判定半径。
    /// </summary>
    public float ShieldRadius => 58f;

    private float _activeTimer = 0f;

    public bool TryActivate(Actor user, Vector2 direction)
    {
        if (!IsReady) return false;

        CurrentCooldown = Cooldown;
        _activeTimer = ActiveDuration;

        // 给予角色受力轻微稳定
        user.Body.Velocity *= 0.6f;

        return true;
    }

    public void Update(float dt)
    {
        if (CurrentCooldown > 0f) CurrentCooldown -= dt;
        if (_activeTimer > 0f) _activeTimer -= dt;
    }

    /// <summary>
    /// 渲染斜向球形阴影分界线流光与金属宝剑十字星高光闪烁动画。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, Actor user)
    {
        if (!IsActive) return;

        Vector2 center = user.Body.Position;
        float progress = 1f - (_activeTimer / ActiveDuration); // 0.0 ~ 1.0
        float alpha = MathF.Sin(progress * MathHelper.Pi);

        float sphereRadius = user.Radius + 9f;

        // 1. 角色周身的半透明暗色球形气罩底色与细致球形外边缘环
        batcher.DrawFilledCircle(spriteBatch, center, sphereRadius, new Color(14, 18, 28) * (alpha * 0.24f));
        batcher.DrawCircleOutline(spriteBatch, center, sphereRadius, Color.White * (alpha * 0.40f), 1.8f);

        // 2. 斜向 3D 球体截面阴影分界线（以 -35° 斜角扫过球体表面）
        const float tiltAngle = -0.61f; // -35度斜向切割
        Vector2 sweepDir = new(MathF.Cos(tiltAngle), MathF.Sin(tiltAngle));
        Vector2 perpDir = new(-sweepDir.Y, sweepDir.X);

        // 截面沿倾斜轴向从 -1.15R 扫过至 +1.15R
        float planeDist = MathHelper.Lerp(-1.15f, 1.15f, progress) * sphereRadius;

        if (MathF.Abs(planeDist) < sphereRadius)
        {
            // 3D 球体平面切圆半径
            float sliceRadius = MathF.Sqrt(sphereRadius * sphereRadius - planeDist * planeDist);
            Vector2 sliceCenter = center + sweepDir * planeDist;

            // 投影为椭圆弧：主轴沿 perpDir，副轴沿 sweepDir
            const float viewTilt = 0.72f; // 观察视角倾角因子
            const int arcSegments = 24;
            Vector2 prevPoint = Vector2.Zero;
            bool hasPrev = false;

            for (int i = 0; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments; // 0.0 ~ 1.0
                float phi = (t - 0.5f) * MathHelper.Pi * 0.95f; // -π/2 到 π/2

                Vector2 offset = perpDir * (MathF.Sin(phi) * sliceRadius)
                               + sweepDir * (MathF.Cos(phi) * sliceRadius * viewTilt);
                Vector2 pt = sliceCenter + offset;

                // 约束在球体内
                if (Vector2.Distance(pt, center) <= sphereRadius + 1f)
                {
                    if (hasPrev)
                    {
                        // 阴影分界线后侧暗色微边
                        Vector2 shadowOffset = sweepDir * (-1.5f);
                        batcher.DrawLine(spriteBatch, prevPoint + shadowOffset, pt + shadowOffset, Color.Black * (alpha * 0.35f), 2.2f);

                        // 柔和微光辉光线
                        batcher.DrawLine(spriteBatch, prevPoint, pt, new Color(210, 245, 255) * (alpha * 0.55f), 3.8f);

                        // 核心高光纯白金属反光线（金属宝剑闪光边缘）
                        batcher.DrawLine(spriteBatch, prevPoint, pt, Color.White * (alpha * 0.95f), 2.0f);
                    }
                    prevPoint = pt;
                    hasPrev = true;
                }
                else
                {
                    hasPrev = false;
                }
            }
        }

        // 3. 元气骑士 / 像素风金属流光“PING! ✨”十字星高光闪烁（在流光掠过球体中心阶段爆发）
        if (progress >= 0.35f && progress <= 0.65f)
        {
            float starPhase = (progress - 0.35f) / 0.30f; // 0.0 ~ 1.0
            float starAlpha = MathF.Sin(starPhase * MathHelper.Pi);
            float starSize = (11f + starAlpha * 6f);

            Vector2 starCenter = center + sweepDir * (planeDist * 0.65f);

            // 十字星横竖尖芒
            batcher.DrawLine(spriteBatch, starCenter - Vector2.UnitX * starSize, starCenter + Vector2.UnitX * starSize, Color.White * starAlpha, 1.8f);
            batcher.DrawLine(spriteBatch, starCenter - Vector2.UnitY * starSize, starCenter + Vector2.UnitY * starSize, Color.White * starAlpha, 1.8f);

            // 45° 辅助小芒
            float subSize = starSize * 0.55f;
            Vector2 diag1 = new Vector2(1f, 1f) * (subSize * 0.707f);
            Vector2 diag2 = new Vector2(-1f, 1f) * (subSize * 0.707f);
            batcher.DrawLine(spriteBatch, starCenter - diag1, starCenter + diag1, Color.White * (starAlpha * 0.75f), 1.2f);
            batcher.DrawLine(spriteBatch, starCenter - diag2, starCenter + diag2, Color.White * (starAlpha * 0.75f), 1.2f);

            // 中心纯白闪烁核
            batcher.DrawFilledCircle(spriteBatch, starCenter, 3.2f, Color.White * starAlpha);
        }
    }
}
