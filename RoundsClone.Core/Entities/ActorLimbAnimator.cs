using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色定长竹节手脚、躯干后坐弹簧与身体/脚物理分离控制器。
/// 职责边界：专门解算双脚的地面静摩擦锚定、身体射击后坐微移与自适应回正弹簧、步态相位演进，
/// 以及高处坠落双腿受压弯曲与定长竹节四肢二阶胡克定律角弹簧动力学。
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
    /// 身体相对脚部的射击后坐/受力局部物理位移（静摩擦力下脚不动，身体后移并自动回正）。
    /// </summary>
    public Vector2 TorsoRecoilOffset => _torsoRecoilOffset;
    private Vector2 _torsoRecoilOffset = Vector2.Zero;
    private Vector2 _torsoRecoilVel = Vector2.Zero;

    /// <summary>
    /// 主手射击后坐力回缩弹簧位移。
    /// </summary>
    public float HandRecoilSpring => _handRecoilSpring;
    private float _handRecoilSpring = 0f;
    private float _handRecoilVelocity = 0f;

    /// <summary>
    /// 高处落地时双腿被身体重力压弯的外扩变形强度 (0.0 到 1.0)。
    /// </summary>
    public float HeavyLandBuckle => _heavyLandBuckle;
    private float _heavyLandBuckle = 0f;
    private float _heavyLandBuckleVel = 0f;

    // 上一帧线速度与瞄准角
    private Vector2 _prevVelocity = Vector2.Zero;
    private float _prevAimAngle = 0f;

    // 双脚静摩擦世界锚点记录（当角色着地且未主动大幅迈步时锁定脚掌位置）
    private readonly Vector2[] _footPlantWorldPos = new Vector2[2];
    private bool _feetPlanted = false;

    // 双腿与双手受力弯曲角位移与角速度
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
    /// 重置所有步态相位与手脚角弹簧状态。
    /// </summary>
    public void Reset()
    {
        WalkCyclePhase = 0f;
        IdlePhase = 0f;
        VisualSpeedFactor = 0f;
        _torsoRecoilOffset = Vector2.Zero;
        _torsoRecoilVel = Vector2.Zero;
        _handRecoilSpring = 0f;
        _handRecoilVelocity = 0f;
        _heavyLandBuckle = 0f;
        _heavyLandBuckleVel = 0f;
        _prevVelocity = Vector2.Zero;
        _feetPlanted = false;
        _legBendAngle[0] = _legBendAngle[1] = 0f;
        _legBendAngularVel[0] = _legBendAngularVel[1] = 0f;
        _mainArmBendAngle = _mainArmBendAngularVel = 0f;
        _offArmBendAngle = _offArmBendAngularVel = 0f;
    }

    /// <summary>
    /// 帧更新步态相位、躯干后坐力回正弹簧、脚部静摩擦与四肢竹节角弹簧。
    /// </summary>
    public void Update(Actor actor, ControlInputState input, CollisionWorld? world, float dt)
    {
        var body = actor.Body;
        bool isGrounded = body.IsGrounded || world == null;

        // 1. 累积行走步态相位与移动朝向
        bool isMovingHorizontally = MathF.Abs(input.MoveVector.X) > 0.05f;
        if (isMovingHorizontally)
        {
            float moveSpeed = world != null ? MathF.Abs(body.Velocity.X) : 240f * MathF.Abs(input.MoveVector.X);
            WalkCyclePhase += dt * MathF.Max(moveSpeed, 150f) * 0.052f;
            VisualMoveDir = MathF.Sign(input.MoveVector.X);
            _feetPlanted = false; // 移动中解除静摩擦力锁定
        }
        else if (world != null && MathF.Abs(body.Velocity.X) > 20f)
        {
            WalkCyclePhase += dt * MathF.Abs(body.Velocity.X) * 0.045f;
            VisualMoveDir = MathF.Sign(body.Velocity.X);
            _feetPlanted = false;
        }
        else if (isGrounded)
        {
            // 静止站立时：激活脚部静摩擦力地面锚定
            if (!_feetPlanted)
            {
                _feetPlanted = true;
                _footPlantWorldPos[0] = body.Position + new Vector2(-actor.Radius * 0.35f, actor.Radius);
                _footPlantWorldPos[1] = body.Position + new Vector2(actor.Radius * 0.35f, actor.Radius);
            }
        }
        else
        {
            _feetPlanted = false;
        }

        IdlePhase += dt * 3.8f;

        // 2. 平滑插值视觉速度因子
        float targetSpeedFactor = world != null
            ? MathHelper.Clamp(MathF.Abs(body.Velocity.X) / 340f, 0f, 1f)
            : MathHelper.Clamp(MathF.Abs(input.MoveVector.X) * 1.4f, 0f, 0.85f);
        VisualSpeedFactor = MathHelper.Lerp(VisualSpeedFactor, targetSpeedFactor, MathHelper.Clamp(dt * 14f, 0f, 1f));

        // 3. 躯干后坐力回正弹簧（射击时躯干后移，射击后在强恢复力作用下自动平滑回正）
        _torsoRecoilOffset = MathHelper2D.SpringDamp2D(_torsoRecoilOffset, Vector2.Zero, ref _torsoRecoilVel, 260f, 16f, dt);

        // 4. 高处坠落压弯弹簧恢复
        _heavyLandBuckle = MathHelper2D.SpringDamp(_heavyLandBuckle, 0f, ref _heavyLandBuckleVel, 190f, 13f, dt);

        // 5. 更新躯干呼吸/行走连续弹性形变
        if (isGrounded)
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

        // 6. 更新手部射击后坐力弹簧与四肢竹节角弹簧
        _handRecoilSpring = MathHelper2D.SpringDamp(_handRecoilSpring, 0f, ref _handRecoilVelocity, 260f, 16f, dt);
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

        Vector2 effectiveVel = world != null ? body.Velocity : new Vector2(input.MoveVector.X * 240f, 0f);
        Vector2 rawAccel = (effectiveVel - _prevVelocity) / MathF.Max(dt, 0.002f);
        Vector2 bodyAccel = MathHelper2D.ClampLength(rawAccel, 9000f);
        _prevVelocity = effectiveVel;

        float currentAimAngle = MathF.Atan2(currentAim.Y, currentAim.X);
        float aimAngularVel = MathHelper.WrapAngle(currentAimAngle - _prevAimAngle) / MathF.Max(dt, 0.002f);
        aimAngularVel = MathHelper.Clamp(aimAngularVel, -35f, 35f);
        _prevAimAngle = currentAimAngle;

        // 躯干后坐位移对双腿带来的附加物理弯曲补偿（身体向后靠，腿部由于脚底静摩擦向前弓起）
        float torsoRecoilFlex = -_torsoRecoilOffset.X * 0.065f;

        // 1. 双腿竹节弯曲角弹簧更新
        for (int i = 0; i < 2; i++)
        {
            float phase = WalkCyclePhase + i * MathHelper.Pi;
            float sideSign = i == 0 ? 1f : -1f;

            float targetLegBend;
            if (isGrounded)
            {
                float stepFlex = MathF.Sin(phase) * 0.72f * VisualSpeedFactor * VisualMoveDir;
                float idleFlex = sideSign * 0.22f * (1f - VisualSpeedFactor);
                // 叠加高处落地压弯与射击后坐受力
                float buckleExtra = _heavyLandBuckle * (sideSign * 0.95f);
                targetLegBend = stepFlex + idleFlex + torsoRecoilFlex + buckleExtra;
            }
            else
            {
                float airVelBend = MathHelper.Clamp(body.Velocity.X * 0.0014f, -0.65f, 0.65f);
                float fallArch = MathHelper.Clamp(body.Velocity.Y * 0.0006f, -0.45f, 0.45f) * sideSign;
                targetLegBend = airVelBend + fallArch;
            }

            _legBendAngularVel[i] += (bodyAccel.X * 0.0032f + bodyAccel.Y * 0.0018f * sideSign) * dt;
            _legBendAngle[i] = MathHelper2D.SpringDamp(_legBendAngle[i], targetLegBend, ref _legBendAngularVel[i], 225f, 8.4f, dt);
            _legBendAngle[i] = MathHelper.Clamp(_legBendAngle[i], -1.35f, 1.35f);
        }

        // 2. 主手竹节弯曲角弹簧更新
        float targetMainBend = aimSign * 0.22f + MathF.Sin(IdlePhase) * 0.06f;
        _mainArmBendAngularVel += (-aimAngularVel * 0.45f + bodyAccel.Y * 0.0028f * aimSign) * dt;
        _mainArmBendAngle = MathHelper2D.SpringDamp(_mainArmBendAngle, targetMainBend, ref _mainArmBendAngularVel, 240f, 8.6f, dt);
        _mainArmBendAngle = MathHelper.Clamp(_mainArmBendAngle, -1.25f, 1.25f);

        // 3. 副手竹节弯曲角弹簧更新
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
        _offArmBendAngle = MathHelper2D.SpringDamp(_offArmBendAngle, targetOffBend, ref _offArmBendAngularVel, 205f, 7.6f, dt);
        _offArmBendAngle = MathHelper.Clamp(_offArmBendAngle, -1.25f, 1.25f);
    }

    /// <summary>
    /// 触发射击时的身体局部后坐冲量（脚掌因静摩擦锚定原地，身体向后微退并自动回正）。
    /// </summary>
    public void ApplyShootingRecoil(Vector2 recoilDir, float strength = 180f)
    {
        _torsoRecoilVel += recoilDir * strength;
        _handRecoilVelocity += 14f;
    }

    /// <summary>
    /// 触发高处极速落地时的强力撞击压弯冲量（身体把脚压弯）。
    /// </summary>
    public void OnHeavyFallImpact(float intensity, float aimSign)
    {
        _heavyLandBuckle += intensity;
        _legBendAngularVel[0] += intensity * 42f;
        _legBendAngularVel[1] -= intensity * 42f;
        _mainArmBendAngularVel += aimSign * intensity * 26f;
        _offArmBendAngularVel -= aimSign * intensity * 28f;
    }

    /// <summary>
    /// 触发普通地面跳跃时的四肢弹簧弯曲。
    /// </summary>
    public void OnGroundJump(float aimSign, float strength = 1.0f)
    {
        _legBendAngularVel[0] += 8.5f * strength;
        _legBendAngularVel[1] -= 8.5f * strength;
        _mainArmBendAngularVel += aimSign * 9.5f * strength;
        _offArmBendAngularVel -= aimSign * 11.0f * strength;
    }

    /// <summary>
    /// 触发空中跳跃（二段跳）时的四肢弹簧弯曲与凌空蹬踏反馈。
    /// </summary>
    public void OnAirJump(float aimSign)
    {
        _legBendAngularVel[0] += 11.5f;
        _legBendAngularVel[1] -= 11.5f;
        _mainArmBendAngularVel += aimSign * 12.0f;
        _offArmBendAngularVel -= aimSign * 13.5f;
    }

    /// <summary>
    /// 触发贴墙蹬跳时的四肢弹簧弯曲。
    /// </summary>
    public void OnWallJump(float wallNormalX, float aimSign)
    {
        _legBendAngularVel[0] += wallNormalX * 12f;
        _legBendAngularVel[1] += wallNormalX * 12f;
        _mainArmBendAngularVel += aimSign * 10f;
        _offArmBendAngularVel -= aimSign * 12f;
    }

    /// <summary>
    /// 触发普通着陆缓冲。
    /// </summary>
    public void OnLandImpact(float landIntensity, float aimSign)
    {
        float bendKick = landIntensity * 28f;
        _legBendAngularVel[0] += bendKick;
        _legBendAngularVel[1] -= bendKick;
        _mainArmBendAngularVel += aimSign * bendKick * 0.7f;
        _offArmBendAngularVel -= aimSign * bendKick * 0.85f;
    }

    /// <summary>
    /// 换弹完成手臂上膛反馈。
    /// </summary>
    public void OnReloadComplete(float aimSign)
    {
        _handRecoilVelocity -= 9f;
        _mainArmBendAngularVel -= aimSign * 7.5f;
    }

    /// <summary>
    /// 受击时的四肢惯性甩动弯曲。
    /// </summary>
    public void OnHitImpact(float hitSign)
    {
        _legBendAngularVel[0] += hitSign * 14f;
        _legBendAngularVel[1] += hitSign * 14f;
        _mainArmBendAngularVel -= hitSign * 15f;
        _offArmBendAngularVel += hitSign * 16f;
    }
}
