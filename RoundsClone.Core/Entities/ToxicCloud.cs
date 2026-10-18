using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 毒云弥漫实体 (Toxic Cloud)。
/// 职责边界：子弹命中留下的范围滞留毒雾（经典卡通积云形态），呈现平底隆顶、多峰并起的饱满蓬松云朵外观；
/// 具备浓厚实体感（低透明度），拥有顶光亮面、浓郁云身与背阴暗腹三层体积深度；
/// 渲染处于背景层，绝不遮蔽前景玩家与敌方角色；为踏入其中的单位施加减速与毒素腐蚀伤害。
/// </summary>
public class ToxicCloud
{
    public const float DefaultRadius = 50f;

    public Vector2 Position { get; }
    public float Radius { get; set; } = DefaultRadius;
    public float Duration { get; set; } = 3.5f;
    public float Timer { get; private set; } = 0f;
    public Actor Owner { get; }
    public bool IsExpired => Timer >= Duration;

    private float _particleTimer = 0f;
    private float _damageTickTimer = 0f;

    /// <summary>
    /// 蓬松积云气团叶片数据（构成积云特有的平底隆顶、多峰并起的经典云朵形态）。
    /// </summary>
    private readonly struct CloudLobe
    {
        public readonly Vector2 BaseOffset;     // 相对云中心的相对偏移
        public readonly float RelativeRadius;   // 叶片相对基准半径的尺寸比例
        public readonly float Phase;            // 呼吸起伏初始相位
        public readonly float WobbleSpeed;      // 呼吸扰动频率
        public readonly float WobbleAmp;        // 缓动位移幅度
        public readonly float RadiusAmp;        // 尺寸微胀缩幅度
        public readonly bool HasTopHighlight;   // 是否绘制顶部白绿高光蓬松峰

        public CloudLobe(Vector2 baseOffset, float relativeRadius, float phase, float wobbleSpeed, float wobbleAmp, float radiusAmp, bool hasTopHighlight = false)
        {
            BaseOffset = baseOffset;
            RelativeRadius = relativeRadius;
            Phase = phase;
            WobbleSpeed = wobbleSpeed;
            WobbleAmp = wobbleAmp;
            RadiusAmp = radiusAmp;
            HasTopHighlight = hasTopHighlight;
        }
    }

    private readonly CloudLobe[] _lobes;

    public ToxicCloud(Vector2 position, Actor owner, float duration = 3.5f, float radius = DefaultRadius)
    {
        Position = position;
        Owner = owner;
        Duration = duration;
        Radius = radius;

        // 构造全向饱满的 11 簇蓬松积云形态（360度精准覆盖 50px 半径减速伤害区，带有顶光高光与底腹阴影）
        var rand = Random.Shared;
        _lobes = new CloudLobe[]
        {
            // 1. 中央致密核心（填充中心，确保中间没有空洞）
            new CloudLobe(Vector2.Zero, 0.62f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.2f, 2.5f, 0.04f, false),
            new CloudLobe(new Vector2(-Radius * 0.16f, -Radius * 0.10f), 0.52f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.3f, 3.0f, 0.05f, false),
            new CloudLobe(new Vector2(Radius * 0.16f, Radius * 0.10f), 0.52f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.1f, 3.0f, 0.05f, false),

            // 2. 8 朵圆周环绕云瓣（分布在各个方向，确保 360 度各个角度都精准填满 50px 范围）
            // 顶部隆起云峰（上方向，带有高光顶面）
            new CloudLobe(new Vector2(0f, -Radius * 0.54f), 0.50f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.4f, 3.5f, 0.05f, true),
            new CloudLobe(new Vector2(Radius * 0.38f, -Radius * 0.38f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.3f, 3.5f, 0.05f, true),
            new CloudLobe(new Vector2(-Radius * 0.38f, -Radius * 0.38f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.5f, 3.5f, 0.05f, true),

            // 左右舒展云翼
            new CloudLobe(new Vector2(Radius * 0.54f, 0f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.2f, 3.5f, 0.05f, true),
            new CloudLobe(new Vector2(-Radius * 0.54f, 0f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.6f, 3.5f, 0.05f, true),

            // 底部云腹基座（下方向，背光暗腹）
            new CloudLobe(new Vector2(0f, Radius * 0.54f), 0.50f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.3f, 3.0f, 0.05f, false),
            new CloudLobe(new Vector2(Radius * 0.38f, Radius * 0.38f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.4f, 3.0f, 0.05f, false),
            new CloudLobe(new Vector2(-Radius * 0.38f, Radius * 0.38f), 0.48f, (float)(rand.NextDouble() * MathHelper.TwoPi), 1.1f, 3.0f, 0.05f, false),
        };
    }

    public void Update(float dt, List<Actor> actors, ParticleSystem particles, ScreenShake shake)
    {
        if (IsExpired) return;

        // 初始生成时的轻盈毒雾爆发
        if (Timer == 0f)
        {
            for (int i = 0; i < 14; i++)
            {
                particles.EmitToxicCloudSpore(Position, Radius);
            }
        }

        Timer += dt;
        _particleTimer += dt;
        while (_particleTimer >= 0.045f)
        {
            _particleTimer -= 0.045f;
            particles.EmitToxicCloudSpore(Position, Radius);
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

                // 每 0.25 秒跳一次腐蚀伤害（10 点/秒）
                if (shouldDealDamage)
                {
                    actor.TakeDamage(2.5f, Owner, Vector2.Zero, particles, shake);
                }
            }
        }
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsExpired) return;

        // 展开与消散时间曲线（初期 0.35s 快速蓬松绽开，尾期 0.8s 平滑消散）
        float deployProgress = MathHelper.Clamp(Timer / 0.35f, 0f, 1f);
        float deployScale = 1f - MathF.Pow(1f - deployProgress, 3f);
        float fadeProgress = MathHelper.Clamp((Duration - Timer) / 0.80f, 0f, 1f);

        // 稍微透明一点点：基准透明度调整为 0.56（从原先 0.82 降至 0.56，更加通透轻盈，同时保持云朵形态）
        float baseAlpha = deployProgress * fadeProgress * 0.56f;
        if (baseAlpha <= 0.001f || deployScale <= 0.01f) return;

        // 慢速呼吸起伏悬浮（模拟云团悬浮时的上下微漂动）
        float floatY = MathF.Sin(Timer * 1.8f) * 3.5f * deployScale;
        Vector2 cloudCenter = Position + new Vector2(0f, floatY);

        // 预先计算各叶片当前帧的动态中心与半径
        Span<Vector2> lobePositions = stackalloc Vector2[_lobes.Length];
        Span<float> lobeRadii = stackalloc float[_lobes.Length];

        for (int i = 0; i < _lobes.Length; i++)
        {
            ref readonly var lobe = ref _lobes[i];
            float wobbleSin = MathF.Sin(Timer * lobe.WobbleSpeed + lobe.Phase);
            float wobbleCos = MathF.Cos(Timer * lobe.WobbleSpeed * 0.85f + lobe.Phase);
            Vector2 currentOffset = (lobe.BaseOffset + new Vector2(wobbleCos, wobbleSin) * lobe.WobbleAmp) * deployScale;
            lobePositions[i] = cloudCenter + currentOffset;
            lobeRadii[i] = Radius * lobe.RelativeRadius * deployScale * (1f + lobe.RadiusAmp * wobbleSin);
        }

        // ==========================================
        // 层级 1：云底深绿阴影层（营造经典卡通积云的背光腹部体积感）
        // ==========================================
        Color shadowColor = new Color(20, 68, 32) * (baseAlpha * 0.80f);
        for (int i = 0; i < _lobes.Length; i++)
        {
            Vector2 shadowPos = lobePositions[i] + new Vector2(0f, lobeRadii[i] * 0.16f);
            batcher.DrawFilledCircle(spriteBatch, shadowPos, lobeRadii[i] * 1.02f, shadowColor);
        }

        // ==========================================
        // 层级 2：云朵经典卡通轮廓描边（深毒绿外框，赋予云朵明确清晰的剪影边界）
        // ==========================================
        Color outlineColor = new Color(15, 52, 24) * (baseAlpha * 0.85f);
        for (int i = 0; i < _lobes.Length; i++)
        {
            batcher.DrawCircleOutline(spriteBatch, lobePositions[i], lobeRadii[i], outlineColor, 2.5f);
        }

        // ==========================================
        // 层级 3：云朵主体实体填充（浓郁饱满的毒素绿色，适度透视）
        // ==========================================
        Color bodyColor = Color.Lerp(ColorPalette.PoisonGreen, new Color(50, 195, 80), 0.30f) * (baseAlpha * 0.95f);
        for (int i = 0; i < _lobes.Length; i++)
        {
            batcher.DrawFilledCircle(spriteBatch, lobePositions[i], lobeRadii[i], bodyColor);
        }

        // ==========================================
        // 层级 4：云朵顶部蓬松亮面高光（向阳/向上翻卷的鹅黄柠檬绿亮顶）
        // ==========================================
        Color highlightColor = Color.Lerp(ColorPalette.PoisonGreen, new Color(195, 255, 140), 0.60f) * (baseAlpha * 0.82f);
        for (int i = 0; i < _lobes.Length; i++)
        {
            if (_lobes[i].HasTopHighlight)
            {
                Vector2 hlPos = lobePositions[i] + new Vector2(0f, -lobeRadii[i] * 0.28f);
                batcher.DrawFilledCircle(spriteBatch, hlPos, lobeRadii[i] * 0.65f, highlightColor);
            }
        }
    }
}
