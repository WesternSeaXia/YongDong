using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色定长竹节手脚与步态弹簧动画控制器。
/// 职责边界：专门管理角色的步伐相位、呼吸律动、手部持枪后坐力弹簧，
/// 以及双腿与双手的胡克定律角位移/角速度物理模拟（确保四肢弧长严格恒定，仅通过弯曲弧度表现受力与惯性）。
/// </summary>
public class ActorLimbAnimator
{
    /// <summary>
    /// 行走周期相位（驱动双腿交替迈步与副手摆臂）。
    /// </summary>
    public float WalkCyclePhase { get; private set; } = 0f;

    /// <summary>
    /// 待机呼吸周期相位（驱动静止时的微幅律动与眼神动态）。
    /// </summary>
    public float IdlePhase { get; private set; } = 0f;

    /// <summary>
    /// 平滑插值后的视觉移动速率因子 (0.0 到 1.0)。
    /// </summary>
    public float VisualSpeedFactor { get; private set; } = 0f;

    /// <summary>
    /// 视觉水平移动朝向符号 (1f 或 -1f)。
    /// </summary>
    public float VisualMoveDir { get; private set; } = 1f;

    /// <summary>
    /// 主手射击后坐力回缩弹簧位移。
    /// </summary>
    public float HandRecoilSpring => _handRecoilSpring;
    private float _handRecoilSpring = 0f;
    private float _handRecoilVelocity = 0f;

    // 上一帧线速度与瞄准角（用于求解躯干加速度与甩枪角速度，作为竹节手脚的惯性外力激励）
    private Vector2 _prevVelocity = Vector2.Zero;
    private float _prevAimAngle = 0f;

    // 双腿与双手的受力弯曲角位移与角速度（仅改变弯曲角，绝不拉伸长度）
    private readonly float[] _legBendAngle = new float[2];
    private readonly float[] _legBendAngularVel = new float[2];
    private float _mainArmBendAngle = 0f;
    private float _mainArmBendAngularVel = 0f;
    private float _offArmBendAngle = 0f;
    private float _offArmBendAngularVel = 0f;

    /// <summary>
    /// 获取指定索引腿部（0: 左腿, 1: 右腿）的当前竹节弯曲角（弧度）。
    /// </summary>
    public float GetLegBendAngle(int index) => _legBendAngle[index];

    /// <summary>
    /// 主手（持枪手）当前竹节弯曲角（弧度）。
    /// </summary>
    public float MainArmBendAngle => _mainArmBendAngle;

    /// <summary>
    /// 副手当前竹节弯曲角（弧度）。
    /// </summary>
    public float OffArmBendAngle => _offArmBendAngle;

    /// <summary>
    /// 重置所有步态相位与手脚角弹簧状态（用于新回合开始）。
    /// </summary>
    public void Reset()
    {
        WalkCyclePhase = 0f;
        IdlePhase = 0f;
        VisualSpeedFactor = 0f;
        _handRecoilSpring = 0f;
        _handRecoilVelocity = 0f;
        _prevVelocity = Vector2.Zero;
        _legBendAngle[0] = _legBendAngle[1] = 0f;
        _legBendAngularVel[0] = _legBendAngularVel[1] = 0f;
        _mainArmBendAngle = _mainArmBendAngularVel = 0f;
        _offArmBendAngle = _offArmBendAngularVel = 0f;
    }

    /// <summary>
    /// 帧更新步态相位、躯干持续微幅形变以及四肢竹节弯曲角弹簧。
    /// </summary>
    public void Update(Actor actor, ControlInputState input, CollisionWorld? world, float dt)
    {
        var body = actor.Body;

        // 1. 累积行走步态相位与移动朝向
        bool isMovingHorizontally = MathF.Abs(input.MoveVector.X) > 0.05f;
        if (isMovingHorizontally)
        {
            float moveSpeed = world != null ? MathF.Abs(body.Velocity.X) : 240f * MathF.Abs(input.MoveVector.X);
            WalkCyclePhase += dt * MathF.Max(moveSpeed, 150f) * 0.052f;
            VisualMoveDir = MathF.Sign(input.MoveVector.X);
        }
        else if (world != null && MathF.Abs(body.Velocity.X) > 20f)
        {
            WalkCyclePhase += dt * MathF.Abs(body.Velocity.X) * 0.045f;
            VisualMoveDir = MathF.Sign(body.Velocity.X);
        }

        IdlePhase += dt * 3.8f;

        // 2. 平滑插值视觉速度因子
        float targetSpeedFactor = world != null
            ? MathHelper.Clamp(MathF.Abs(body.Velocity.X) / 340f, 0f, 1f)
            : MathHelper.Clamp(MathF.Abs(input.MoveVector.X) * 1.4f, 0f, 0.85f);
        VisualSpeedFactor = MathHelper.Lerp(VisualSpeedFactor, targetSpeedFactor, MathHelper.Clamp(dt * 14f, 0f, 1f));

        // 3. 更新躯干行走/呼吸/空中连续弹性形变
        if (body.IsGrounded || world == null)
        {
            if (VisualSpeedFactor > 0.08f)
            {
                float walkSquash = MathF.Sin(WalkCyclePhase * 2f) * 0.055f * VisualSpeedFactor;
                actor.Squash.SetContinuousWobble(walkSquash);
            }
            else
            {
                actor.Squash.SetContinuousWobble(MathF.Sin(IdlePhase) * 0.018f);
            }
        }
        else
        {
            float airStretch = -MathHelper.Clamp(MathF.Abs(body.Velocity.Y) / 2200f, 0f, 0.10f);
            actor.Squash.SetContinuousWobble(airStretch);
        }

        // 4. 更新手部后坐力弹簧与四肢竹节角弹簧
        _handRecoilSpring = MathHelper2D.SpringDamp(_handRecoilSpring, 0f, ref _handRecoilVelocity, 240f, 14f, dt);
        UpdateBambooAngularSprings(actor, input, world, dt);
    }

    /// <summary>
    /// 解算双腿与双手竹节的二阶胡克定律角弹簧。
    /// </summary>
    private void UpdateBambooAngularSprings(Actor actor, ControlInputState input, CollisionWorld? world, float dt)
    {
        var body = actor.Body;
        Vector2 currentAim = actor.CurrentAim;
        float aimSign = currentAim.X >= 0f ? 1f : -1f;
        bool isGrounded = body.IsGrounded || world == null;

        // 计算躯干瞬态加速度与瞄准转动角速度
        Vector2 effectiveVel = world != null ? body.Velocity : new Vector2(input.MoveVector.X * 240f, 0f);
        Vector2 rawAccel = (effectiveVel - _prevVelocity) / MathF.Max(dt, 0.002f);
        Vector2 bodyAccel = MathHelper2D.ClampLength(rawAccel, 9000f);
        _prevVelocity = effectiveVel;

        float currentAimAngle = MathF.Atan2(currentAim.Y, currentAim.X);
        float aimAngularVel = MathHelper.WrapAngle(currentAimAngle - _prevAimAngle) / MathF.Max(dt, 0.002f);
        aimAngularVel = MathHelper.Clamp(aimAngularVel, -35f, 35f);
        _prevAimAngle = currentAimAngle;

        // 1. 双腿竹节弯曲角弹簧更新（步态提膝弯曲 + 惯性弯曲）
        for (int i = 0; i < 2; i++)
        {
            float phase = WalkCyclePhase + i * MathHelper.Pi;
            float sideSign = i == 0 ? 1f : -1f;

            float targetLegBend;
            if (isGrounded)
            {
                float stepFlex = MathF.Sin(phase) * 0.72f * VisualSpeedFactor * VisualMoveDir;
                float idleFlex = sideSign * 0.24f * (1f - VisualSpeedFactor);
                targetLegBend = stepFlex + idleFlex;
            }
            else
            {
                float airVelBend = MathHelper.Clamp(body.Velocity.X * 0.0014f, -0.65f, 0.65f);
                float fallArch = MathHelper.Clamp(body.Velocity.Y * 0.0006f, -0.45f, 0.45f) * sideSign;
                targetLegBend = airVelBend + fallArch;
            }

            _legBendAngularVel[i] += (bodyAccel.X * 0.0032f + bodyAccel.Y * 0.0018f * sideSign) * dt;
            _legBendAngle[i] = MathHelper2D.SpringDamp(_legBendAngle[i], targetLegBend, ref _legBendAngularVel[i], 215f, 7.8f, dt);
            _legBendAngle[i] = MathHelper.Clamp(_legBendAngle[i], -1.25f, 1.25f);
        }

        // 2. 主手竹节弯曲角弹簧更新（瞄准甩动 + 射击后坐弯曲 + 运动惯性）
        float targetMainBend = aimSign * 0.22f + MathF.Sin(IdlePhase) * 0.06f;
        _mainArmBendAngularVel += (-aimAngularVel * 0.45f + bodyAccel.Y * 0.0028f * aimSign) * dt;
        _mainArmBendAngle = MathHelper2D.SpringDamp(_mainArmBendAngle, targetMainBend, ref _mainArmBendAngularVel, 230f, 8.2f, dt);
        _mainArmBendAngle = MathHelper.Clamp(_mainArmBendAngle, -1.20f, 1.20f);

        // 3. 副手竹节弯曲角弹簧更新（步伐摆动弯曲 + 空中飘动弯曲）
        float targetOffBend;
        if (isGrounded)
        {
            targetOffBend = -aimSign * (0.35f + MathF.Sin(WalkCyclePhase) * 0.55f * VisualSpeedFactor);
        }
        else
        {
            targetOffBend = -aimSign * (0.45f + MathHelper.Clamp(body.Velocity.Y * 0.0008f, -0.5f, 0.5f));
        }
        _offArmBendAngularVel += (-bodyAccel.X * 0.0030f - bodyAccel.Y * 0.0032f * aimSign) * dt;
        _offArmBendAngle = MathHelper2D.SpringDamp(_offArmBendAngle, targetOffBend, ref _offArmBendAngularVel, 195f, 7.2f, dt);
        _offArmBendAngle = MathHelper.Clamp(_offArmBendAngle, -1.25f, 1.25f);
    }

    /// <summary>
    /// 触发地面起跳时的四肢竹节向下弹性弯曲冲量。
    /// </summary>
    public void OnGroundJump(float aimSign, float strength = 1.0f)
    {
        _legBendAngularVel[0] += 8.5f * strength;
        _legBendAngularVel[1] -= 8.5f * strength;
        _mainArmBendAngularVel += aimSign * 9.5f * strength;
        _offArmBendAngularVel -= aimSign * 11.0f * strength;
    }

    /// <summary>
    /// 触发贴墙蹬跳时的四肢竹节反向摆动弯曲冲量。
    /// </summary>
    public void OnWallJump(float wallNormalX, float aimSign)
    {
        _legBendAngularVel[0] += wallNormalX * 12f;
        _legBendAngularVel[1] += wallNormalX * 12f;
        _mainArmBendAngularVel += aimSign * 10f;
        _offArmBendAngularVel -= aimSign * 12f;
    }

    /// <summary>
    /// 触发高处落地时的双腿外扩缓冲弯曲冲量。
    /// </summary>
    public void OnLandImpact(float landIntensity, float aimSign)
    {
        float bendKick = landIntensity * 32f;
        _legBendAngularVel[0] += bendKick;
        _legBendAngularVel[1] -= bendKick;
        _mainArmBendAngularVel += aimSign * bendKick * 0.7f;
        _offArmBendAngularVel -= aimSign * bendKick * 0.85f;
    }

    /// <summary>
    /// 触发换弹完成上膛时的手臂震动反馈。
    /// </summary>
    public void OnReloadComplete(float aimSign)
    {
        _handRecoilVelocity -= 10f;
        _mainArmBendAngularVel -= aimSign * 8.0f;
    }

    /// <summary>
    /// 触发主武器射击时的手部后坐回缩与四肢竹节震荡。
    /// </summary>
    public void OnShootRecoil(float aimSign, Vector2 recoilDir)
    {
        _handRecoilVelocity += 16f;
        _mainArmBendAngularVel += aimSign * 14.5f;
        _offArmBendAngularVel -= aimSign * 11.5f;
        _legBendAngularVel[0] += recoilDir.X * 8.5f;
        _legBendAngularVel[1] += recoilDir.X * 8.5f;
    }

    /// <summary>
    /// 触发受击时的四肢惯性甩动弯曲。
    /// </summary>
    public void OnHitImpact(float hitSign)
    {
        _legBendAngularVel[0] += hitSign * 14f;
        _legBendAngularVel[1] += hitSign * 14f;
        _mainArmBendAngularVel -= hitSign * 15f;
        _offArmBendAngularVel += hitSign * 16f;
    }
}
