using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 毒云弥漫实体 (Toxic Cloud)。
/// 100% 还原《ROUNDS》原版实机风格与动态流体循环动画：
/// 1. 中心毒气不断喷涌：核心恒定气团镇守中核，外围交错气团从中心以正弦曲线迅速膨胀涌出；
/// 2. 圈边毒气动态消散：气团在推进至约 50px 半径外边缘时按二次方曲线收缩湮灭，形成周而复始的内涌外散；
/// 3. ROUNDS 经典纯净双色色阶（无黑色描边杂质）：下层深橄榄绿阴影 + 上层芥末荧光绿主体。
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

    private float _damageTickTimer = 0f;
    private float _sporeTimer = 0f;

    // 核心气团：镇守云核，确保中心始终厚实致密、永不镂空
    private readonly struct CorePuff
    {
        public readonly Vector2 Offset;
        public readonly float RelativeRadius;
        public readonly float PulseSpeed;
        public readonly float Phase;

        public CorePuff(Vector2 offset, float relativeRadius, float pulseSpeed, float phase)
        {
            Offset = offset;
            RelativeRadius = relativeRadius;
            PulseSpeed = pulseSpeed;
            Phase = phase;
        }
    }

    // 喷涌翻滚气团：从中心涌现膨胀，推进至边缘收缩消散
    private readonly struct BillowPuff
    {
        public readonly float Angle;          // 喷涌主方向（弧度）
        public readonly float MaxDist;        // 推进至边缘的最大距离
        public readonly float BaseRadius;     // 最大膨胀半径
        public readonly float CycleSpeed;     // 喷涌循环频率（次/秒）
        public readonly float Phase;          // 相位偏移，确保各方向交错连续喷涌
        public readonly float SwirlAmount;    // 翻滚微旋幅度

        public BillowPuff(float angle, float maxDist, float baseRadius, float cycleSpeed, float phase, float swirlAmount)
        {
            Angle = angle;
            MaxDist = maxDist;
            BaseRadius = baseRadius;
            CycleSpeed = cycleSpeed;
            Phase = phase;
            SwirlAmount = swirlAmount;
        }
    }

    private readonly CorePuff[] _corePuffs;
    private readonly BillowPuff[] _billowPuffs;

    public ToxicCloud(Vector2 position, Actor owner, float duration = 3.5f, float radius = DefaultRadius)
    {
        Position = position;
        Owner = owner;
        Duration = duration;
        Radius = radius;

        var rand = Random.Shared;

        // 1. 3 朵致密核心气团，呈品字形分布在中心，轻微呼吸搏动
        _corePuffs = new CorePuff[]
        {
            new CorePuff(new Vector2(0f, -Radius * 0.04f), Radius * 0.44f, 2.2f, 0f),
            new CorePuff(new Vector2(-Radius * 0.12f, Radius * 0.08f), Radius * 0.38f, 2.6f, 1.8f),
            new CorePuff(new Vector2(Radius * 0.12f, Radius * 0.07f), Radius * 0.39f, 2.4f, 3.4f)
        };

        // 2. 10 朵交错相位向外喷涌气团，全方向覆盖
        const int billowCount = 10;
        _billowPuffs = new BillowPuff[billowCount];

        for (int i = 0; i < billowCount; i++)
        {
            // 角度均匀分布并附带少量有机抖动
            float baseAngle = (i / (float)billowCount) * MathHelper.TwoPi;
            float angleJitter = ((float)rand.NextDouble() - 0.5f) * 0.25f;
            float angle = baseAngle + angleJitter;

            // 推进最大距离：0.60R ~ 0.74R（气泡外沿在展开后正好抵达约 50px 边缘）
            float maxDist = Radius * (0.60f + (float)rand.NextDouble() * 0.14f);

            // 最大饱满半径：0.34R ~ 0.44R (约 17px ~ 22px)
            float baseRadius = Radius * (0.34f + (float)rand.NextDouble() * 0.10f);

            // 循环频率：0.75Hz ~ 0.95Hz（约 1.05s ~ 1.33s 完成一次从中心涌出到边缘消散的完整循环）
            float cycleSpeed = 0.75f + (float)rand.NextDouble() * 0.20f;

            // 相位使用黄金分割率打散，确保不同方向的气泡不会同时膨胀或同时消散，呈现持续不断的涌动感
            float phase = (i * 0.6180339887f + (float)rand.NextDouble() * 0.08f) % 1.0f;

            // 左右微旋翻滚
            float swirlAmount = ((float)rand.NextDouble() - 0.5f) * 0.26f;

            _billowPuffs[i] = new BillowPuff(angle, maxDist, baseRadius, cycleSpeed, phase, swirlAmount);
        }
    }

    public void Update(float dt, List<Actor> actors, ParticleSystem particles, ScreenShake shake)
    {
        if (IsExpired) return;

        Timer += dt;

        // 周期性跳伤害与减速判定
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
                // 持续减速 35% 并施加通用减速状态（非冰冻）
                actor.ApplySlow(0.35f, 0.65f, isFrost: false);
                actor.Body.Velocity *= MathF.Max(0.65f, 1f - 0.35f * dt * 4f);

                // 每 0.25 秒跳一次腐蚀伤害（10 点/秒）
                if (shouldDealDamage)
                {
                    actor.TakeDamage(2.5f, Owner, Vector2.Zero, particles, shake);
                }
            }
        }

        // 偶发向圈边释放微量毒素轻烟，辅佐圈边消散视觉感
        _sporeTimer += dt;
        if (_sporeTimer >= 0.14f)
        {
            _sporeTimer = 0f;
            particles.EmitToxicCloudSpore(Position, Radius);
        }
    }

    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsExpired) return;

        // 1. ROUNDS 经典果冻弹力展开（前 0.16s 迅速弹起弹出）与收缩消散（最后 0.30s 平滑收缩）
        float overallScale;
        const float spawnTime = 0.16f;
        const float despawnTime = 0.30f;

        if (Timer < spawnTime)
        {
            float t = Timer / spawnTime;
            overallScale = MathF.Sin(t * MathHelper.PiOver2 * 1.08f);
        }
        else if (Duration - Timer < despawnTime)
        {
            float t = MathHelper.Clamp((Duration - Timer) / despawnTime, 0f, 1f);
            overallScale = t * t;
        }
        else
        {
            overallScale = 1.0f;
        }

        if (overallScale <= 0.001f) return;

        // 整体微幅有机上下浮动
        float floatY = MathF.Sin(Timer * 2.2f) * 1.5f * overallScale;
        Vector2 cloudCenter = Position + new Vector2(0f, floatY);

        // 2. 颜色方案：100% 还原 ROUNDS 原版纯净双色色阶（无黑色描边）
        Color shadowColor = new Color(90, 158, 26);  // 下层浓郁深橄榄绿阴影
        Color bodyColor = new Color(158, 225, 42);   // 上层明亮鲜艳芥末荧光绿

        int totalCircles = _corePuffs.Length + _billowPuffs.Length;
        Span<Vector2> circlePositions = stackalloc Vector2[totalCircles];
        Span<float> circleRadii = stackalloc float[totalCircles];
        int validCount = 0;

        // 3. 计算核心气团（呼吸脉动）
        for (int i = 0; i < _corePuffs.Length; i++)
        {
            ref readonly var core = ref _corePuffs[i];
            float pulse = 1.0f + MathF.Sin(Timer * core.PulseSpeed + core.Phase) * 0.07f;
            circlePositions[validCount] = cloudCenter + core.Offset * overallScale;
            circleRadii[validCount] = core.RelativeRadius * pulse * overallScale;
            validCount++;
        }

        // 4. 计算喷涌翻滚气团（从中心喷涌膨胀 -> 推进至边缘二次方收缩消散）
        for (int i = 0; i < _billowPuffs.Length; i++)
        {
            ref readonly var puff = ref _billowPuffs[i];
            float p = (Timer * puff.CycleSpeed + puff.Phase) % 1.0f;

            // 曲线 A：位移向外推进（初期微加速喷涌）
            float distT = MathF.Pow(p, 0.75f);
            float currentDist = MathHelper.Lerp(4f, puff.MaxDist, distT) * overallScale;

            // 微弱偏转产生流体翻滚感
            float currentAngle = puff.Angle + MathF.Sin(p * MathHelper.TwoPi) * puff.SwirlAmount;
            Vector2 puffPos = cloudCenter + new Vector2(MathF.Cos(currentAngle), MathF.Sin(currentAngle)) * currentDist;

            // 曲线 B：尺寸变化
            // p < 0.28: 从中心迅速喷涌破壳膨胀（0 -> 1.0）
            // 0.28 <= p <= 0.68: 保持饱满云团厚度向前推进
            // p > 0.68: 到达约 50px 边缘时按二次方平滑收缩消解为 0（边缘消散感）
            float sizeFactor;
            if (p < 0.28f)
            {
                sizeFactor = MathF.Sin((p / 0.28f) * MathHelper.PiOver2);
            }
            else if (p > 0.68f)
            {
                float fade = (1.0f - p) / 0.32f;
                sizeFactor = fade * fade;
            }
            else
            {
                sizeFactor = 1.0f;
            }

            float puffRadius = puff.BaseRadius * sizeFactor * overallScale;
            if (puffRadius > 0.5f)
            {
                circlePositions[validCount] = puffPos;
                circleRadii[validCount] = puffRadius;
                validCount++;
            }
        }

        // ==========================================
        // 层级 1：下层深绿阴影层（向下偏移，融合成整块有机底部阴影）
        // ==========================================
        Vector2 shadowShift = new(0f, 4.5f * overallScale);
        for (int i = 0; i < validCount; i++)
        {
            batcher.DrawFilledCircle(spriteBatch, circlePositions[i] + shadowShift, circleRadii[i] * 1.02f, shadowColor);
        }

        // ==========================================
        // 层级 2：上层明亮芥末黄绿主体（多圆自然融合为连续流体毒云）
        // ==========================================
        for (int i = 0; i < validCount; i++)
        {
            batcher.DrawFilledCircle(spriteBatch, circlePositions[i], circleRadii[i], bodyColor);
        }
    }
}
