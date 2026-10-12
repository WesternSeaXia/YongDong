using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色输入控制意图状态（解耦玩家触屏/键鼠与 AI 决策）。
/// </summary>
public struct ControlInputState
{
    public Vector2 MoveVector;       // 移动力矢量意图 (-1 到 1)
    public Vector2 AimVector;        // 瞄准方向矢量
    public bool IsFireDown;          // 开火扳机按下
    public bool IsJumpDown;          // 跳跃按键按下
    public bool IsSecondaryDown;     // 副武器按键按下
}

/// <summary>
/// 角色面部眼神风格枚举（支持经典、可爱、猥琐、搞怪等多种神态定制）。
/// </summary>
public enum EyeStyle
{
    Classic,    // 经典斗士
    Cute,       // 水汪萌眼
    Sneaky,     // 猥琐眯眼
    Derp,       // 智慧斗鸡
    Happy,      // 腹黑笑眼
    Crazy       // 狂暴死鱼
}

/// <summary>
/// 纯程序化生成的 Rounds 核心角色实体。
/// 包含矢量力驱动物理刚体、阻尼形变骨骼、Rounds 风格线条手脚 IK 动画、副武器插槽以及卡牌修饰符管线。
/// </summary>
public class Actor
{
    /// <summary>
    /// 角色名称标识。
    /// </summary>
    public string Name { get; set; } = "Player";

    /// <summary>
    /// 是否为 AI 控制器托管。
    /// </summary>
    public bool IsAi { get; set; } = false;

    /// <summary>
    /// 角色眼神外观配置。
    /// </summary>
    public EyeStyle EyeStyle { get; set; } = EyeStyle.Classic;

    /// <summary>
    /// 物理刚体组件。
    /// </summary>
    public RigidBody2D Body { get; }

    /// <summary>
    /// 物理碰撞体。
    /// </summary>
    public CircleCollider Collider { get; }

    /// <summary>
    /// 虎克定律弹簧阻尼形变模型 (Squash & Stretch)。
    /// </summary>
    public SpringSquashStretch Squash { get; } = new();

    // --- 核心基准力学常数 (遵循 Rounds 经典手感，适配 2600 重力场) ---
    public const float BaseRadius = 26f;
    public const float BaseMass = 1.0f;
    public const float BaseMoveForce = 2220f;
    public const float BaseJumpImpulse = 1100f;

    /// <summary>
    /// 角色当前半径 (像素)，受 MaxHealth 影响动态缩放。
    /// </summary>
    public float Radius { get; set; } = BaseRadius;

    /// <summary>
    /// 角色主色调。
    /// </summary>
    public Color ThemeColor { get; set; } = ColorPalette.PlayerBlue;

    // --- 属性数值体系 ---
    public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth { get; set; } = 100f;
    public bool IsDead => CurrentHealth <= 0f;

    public float MoveForce { get; set; } = BaseMoveForce;
    public float JumpImpulse { get; set; } = BaseJumpImpulse;

    // 枪械武器与弹匣换弹属性
    public int MaxAmmo { get; set; } = 3;
    public int CurrentAmmo { get; set; } = 3;
    public float ReloadDuration { get; set; } = 1.45f;
    public float ReloadTimer { get; set; } = 0f;
    public bool IsReloading => ReloadTimer > 0f;

    public float FireRateCooldown { get; set; } = 0.35f;
    public float CurrentFireCooldown { get; set; } = 0f;
    public float BulletDamage { get; set; } = 28f;
    public float BulletSpeed { get; set; } = 1040f;
    public float RecoilImpulse { get; set; } = 320f;
    public int BulletBounces { get; set; } = 0;
    public int BulletsPerShot { get; set; } = 1;
    public float BulletSpread { get; set; } = 0.05f;
    public float ExplosionRadius { get; set; } = 0f;
    public bool PoisonBullets { get; set; } = false;
    public bool ImplosionBullets { get; set; } = false;
    public int SplitOnBounceCount { get; set; } = 0;

    // 副武器插槽
    public ISubWeapon? SubWeapon { get; set; }

    // 状态机制
    public bool IsInvulnerable { get; set; } = false;
    public float HitFlashTimer { get; set; } = 0f;
    public float PoisonTimer { get; set; } = 0f;

    /// <summary>
    /// 当前是否正贴着墙壁向下滑行。
    /// </summary>
    public bool IsWallSliding { get; private set; } = false;

    // 平台跳跃手感缓冲与土狼时间计时器
    private float _groundCoyoteTimer = 0f;
    private float _wallCoyoteTimer = 0f;
    private Vector2 _lastWallNormal = Vector2.Zero;
    private float _jumpBufferTimer = 0f;
    private bool _prevJumpDown = false;
    private bool _wasGroundedLastFrame = true;
    private float _prevFallVelocity = 0f;

    // --- 动画与定长竹节弹性手脚角弹簧内部状态 ---
    private float _walkCyclePhase = 0f;
    private float _idlePhase = 0f;
    private float _visualSpeedFactor = 0f;
    private float _visualMoveDir = 1f;
    private float _handRecoilSpring = 0f;
    private float _handRecoilVelocity = 0f;
    private Vector2 _currentAim = Vector2.UnitX;

    private Vector2 _prevVelocity = Vector2.Zero;
    private float _prevAimAngle = 0f;

    // 双腿与双手的受力弯曲角位移与角速度 (严格恒定弧长，仅通过弯曲角变化表现竹节弹性)
    private readonly float[] _legBendAngle = new float[2];
    private readonly float[] _legBendAngularVel = new float[2];
    private float _mainArmBendAngle = 0f;
    private float _mainArmBendAngularVel = 0f;
    private float _offArmBendAngle = 0f;
    private float _offArmBendAngularVel = 0f;

    // 装备的卡牌管线列表
    public List<ICardPlugin> AppliedCards { get; } = new();

    /// <summary>
    /// 构造角色实体。
    /// </summary>
    public Actor(Vector2 position, Color themeColor, bool isAi = false)
    {
        ThemeColor = themeColor;
        IsAi = isAi;

        Body = new RigidBody2D(position, BaseMass)
        {
            Drag = 0.11f,
            GravityScale = 1.0f,
            FallGravityMultiplier = 1.28f,
            Restitution = 0.05f
        };

        Collider = new CircleCollider(Body, Radius);
        RecalculateHealthScaling();
    }

    /// <summary>
    /// 根据当前最大生命值动态重新计算角色体型、物理质量、移动速度与跳跃冲量。
    /// 遵循 Rounds 经典力学机制：血量越高，体积越大、质量越沉、移动速度越慢、跳跃高度越低。
    /// </summary>
    public void RecalculateHealthScaling()
    {
        float ratio = MathF.Max(0.5f, MaxHealth / 100f);

        // 1. 体积与碰撞半径随血量非线性放大
        Radius = BaseRadius * MathF.Pow(ratio, 0.40f);
        Collider.Radius = Radius;

        // 2. 刚体质量随血量线性递增 (抗击退稳定性提升)
        Body.Mass = BaseMass * ratio;

        // 3. 移动推力随血量递减 (更重装坦克的机动性更缓)
        MoveForce = BaseMoveForce * MathF.Pow(1.0f / ratio, 0.35f);

        // 4. 起跳冲量随血量递减 (体重越大，跳跃高度越低)
        JumpImpulse = BaseJumpImpulse * MathF.Pow(1.0f / ratio, 0.25f);
    }

    /// <summary>
    /// 帧更新角色逻辑、定长竹节手脚受力弯曲角弹簧与冷却机制。
    /// </summary>
    public void Update(
        float dt,
        ControlInputState input,
        CollisionWorld? world,
        ParticleSystem particles,
        ScreenShake shake,
        Action<Projectile> onSpawnProjectile)
    {
        if (IsDead) return;

        // 1. 更新受击白光与毒素状态
        if (HitFlashTimer > 0f) HitFlashTimer -= dt;
        if (PoisonTimer > 0f)
        {
            PoisonTimer -= dt;
            TakeDamage(12f * dt, null, Vector2.Zero, particles, shake);
            if (Random.Shared.NextDouble() < 0.2)
            {
                particles.EmitHitSparks(Body.Position, Vector2.UnitY, ColorPalette.PoisonGreen, 2);
            }
        }

        // 2. 更新瞄准方向
        if (input.AimVector.LengthSquared() > 0.001f)
        {
            _currentAim = MathHelper2D.SafeNormalize(input.AimVector);
        }

        // 3. 施加水平移动矢量力 (F = ma) 与步态律动
        bool isMovingHorizontally = MathF.Abs(input.MoveVector.X) > 0.05f;
        if (isMovingHorizontally)
        {
            if (world != null)
            {
                Body.AddForce(new Vector2(input.MoveVector.X * MoveForce, 0f));
            }
            float moveSpeed = world != null ? MathF.Abs(Body.Velocity.X) : 240f * MathF.Abs(input.MoveVector.X);
            _walkCyclePhase += dt * MathF.Max(moveSpeed, 150f) * 0.052f;
            _visualMoveDir = MathF.Sign(input.MoveVector.X);
        }
        else if (world != null && MathF.Abs(Body.Velocity.X) > 20f)
        {
            _walkCyclePhase += dt * MathF.Abs(Body.Velocity.X) * 0.045f;
            _visualMoveDir = MathF.Sign(Body.Velocity.X);
        }

        _idlePhase += dt * 3.8f;

        float targetSpeedFactor = world != null
            ? MathHelper.Clamp(MathF.Abs(Body.Velocity.X) / 340f, 0f, 1f)
            : MathHelper.Clamp(MathF.Abs(input.MoveVector.X) * 1.4f, 0f, 0.85f);
        _visualSpeedFactor = MathHelper.Lerp(_visualSpeedFactor, targetSpeedFactor, MathHelper.Clamp(dt * 14f, 0f, 1f));

        // 计算行走/呼吸/空中的适度弹性波动（收敛幅度，避免果冻感过头）
        if (Body.IsGrounded || world == null)
        {
            if (_visualSpeedFactor > 0.08f)
            {
                float walkSquash = MathF.Sin(_walkCyclePhase * 2f) * 0.055f * _visualSpeedFactor;
                Squash.SetContinuousWobble(walkSquash);
            }
            else
            {
                Squash.SetContinuousWobble(MathF.Sin(_idlePhase) * 0.018f);
            }
        }
        else
        {
            float airStretch = -MathHelper.Clamp(MathF.Abs(Body.Velocity.Y) / 2200f, 0f, 0.10f);
            Squash.SetContinuousWobble(airStretch);
        }

        // 4. 跳跃缓冲与土狼时间更新
        bool jumpJustPressed = input.IsJumpDown && !_prevJumpDown;
        _prevJumpDown = input.IsJumpDown;

        if (jumpJustPressed)
        {
            _jumpBufferTimer = 0.14f;
        }
        else if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer -= dt;
        }

        if (Body.IsGrounded)
        {
            _groundCoyoteTimer = 0.10f;
        }
        else if (_groundCoyoteTimer > 0f)
        {
            _groundCoyoteTimer -= dt;
        }

        if (Body.IsTouchingWall && !Body.IsGrounded)
        {
            _wallCoyoteTimer = 0.16f;
            _lastWallNormal = Body.WallNormal;
        }
        else if (_wallCoyoteTimer > 0f)
        {
            _wallCoyoteTimer -= dt;
        }

        // 5. 贴墙下滑机制 (Wall Slide) - 贴墙缓缓下滑并喷射摩擦火花
        IsWallSliding = Body.IsTouchingWall && !Body.IsGrounded;
        if (IsWallSliding)
        {
            if (Body.Velocity.Y > 0f)
            {
                float maxSlideSpeed = 175f;
                if (Body.Velocity.Y > maxSlideSpeed)
                {
                    Body.Velocity = new Vector2(Body.Velocity.X, maxSlideSpeed);
                }

                if (Random.Shared.NextDouble() < 0.35)
                {
                    Vector2 wallContactPos = Body.Position - Body.WallNormal * Radius;
                    particles.EmitWallSlideFriction(wallContactPos, Body.WallNormal, ThemeColor);
                }
            }
        }

        float aimSign = _currentAim.X >= 0f ? 1f : -1f;

        // 6. 跳跃与贴墙跳逻辑 (Ground Jump & Wall Jump)
        if (_jumpBufferTimer > 0f)
        {
            if (_groundCoyoteTimer > 0f)
            {
                Body.Velocity = new Vector2(Body.Velocity.X, -JumpImpulse);
                Body.IsGrounded = false;
                _groundCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Squash.ApplyStretch(Vector2.UnitY, 0.30f);
                // 起跳瞬间赋予手脚竹节向下弯曲角速度，不改变长度
                _legBendAngularVel[0] += 8.5f;
                _legBendAngularVel[1] -= 8.5f;
                _mainArmBendAngularVel += aimSign * 9.5f;
                _offArmBendAngularVel -= aimSign * 11.0f;
                particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 7);
            }
            else if (_wallCoyoteTimer > 0f && _lastWallNormal != Vector2.Zero)
            {
                float wallKickX = _lastWallNormal.X * 720f;
                float wallJumpY = -JumpImpulse * 0.94f;

                Body.Velocity = new Vector2(wallKickX, wallJumpY);
                Body.IsTouchingWall = false;
                _wallCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Vector2 jumpDir = MathHelper2D.SafeNormalize(new Vector2(wallKickX, wallJumpY));
                Squash.ApplyStretch(jumpDir, 0.32f);
                _legBendAngularVel[0] += _lastWallNormal.X * 12f;
                _legBendAngularVel[1] += _lastWallNormal.X * 12f;
                _mainArmBendAngularVel += aimSign * 10f;
                _offArmBendAngularVel -= aimSign * 12f;

                Vector2 wallContactPos = Body.Position - _lastWallNormal * Radius;
                particles.EmitHitSparks(wallContactPos, _lastWallNormal, ThemeColor, 10);
            }
        }
        else if (input.IsJumpDown && Body.IsGrounded)
        {
            Body.Velocity = new Vector2(Body.Velocity.X, -JumpImpulse);
            Body.IsGrounded = false;
            _groundCoyoteTimer = 0f;
            Squash.ApplyStretch(Vector2.UnitY, 0.30f);
            _legBendAngularVel[0] += 8.0f;
            _legBendAngularVel[1] -= 8.0f;
            _mainArmBendAngularVel += aimSign * 8.5f;
            _offArmBendAngularVel -= aimSign * 10.0f;
            particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 6);
        }

        // 7. 检测落地撞击：竹节双腿向外弹性弯曲吸收冲击（长度恒定不变）
        if (Body.IsGrounded && !_wasGroundedLastFrame)
        {
            if (_prevFallVelocity > 150f)
            {
                float landIntensity = MathHelper.Clamp(_prevFallVelocity / 1600f, 0.14f, 0.34f);
                Squash.ApplySquash(Vector2.UnitY, landIntensity);
                float bendKick = landIntensity * 32f;
                _legBendAngularVel[0] += bendKick;
                _legBendAngularVel[1] -= bendKick;
                _mainArmBendAngularVel += aimSign * bendKick * 0.7f;
                _offArmBendAngularVel -= aimSign * bendKick * 0.85f;
                particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 7);
            }
        }
        _wasGroundedLastFrame = Body.IsGrounded;
        _prevFallVelocity = Body.Velocity.Y;

        // 8. 弹匣换弹与武器射击逻辑
        if (CurrentFireCooldown > 0f) CurrentFireCooldown -= dt;

        if (IsReloading)
        {
            ReloadTimer -= dt;
            if (ReloadTimer <= 0f)
            {
                ReloadTimer = 0f;
                CurrentAmmo = MaxAmmo;
                _handRecoilVelocity -= 10f;
                _mainArmBendAngularVel -= aimSign * 8.0f;
                Squash.ApplySquash(Vector2.UnitY, 0.15f);
                particles.EmitHitSparks(Body.Position + _currentAim * (Radius + 14f), _currentAim, Color.White, 5);
            }
        }
        else if (input.IsFireDown && CurrentFireCooldown <= 0f && CurrentAmmo > 0)
        {
            Shoot(particles, shake, onSpawnProjectile);
        }

        // 9. 副武器逻辑
        if (SubWeapon != null)
        {
            SubWeapon.Update(dt);
            if (input.IsSecondaryDown)
            {
                if (SubWeapon.TryActivate(this, _currentAim))
                {
                    foreach (var card in AppliedCards)
                    {
                        card.OnSecondaryUsed(this, SubWeapon);
                    }
                }
            }
        }

        // 10. 更新手部反冲弹簧、身体形变弹簧以及定长竹节手脚弯曲角弹簧
        _handRecoilSpring = MathHelper2D.SpringDamp(_handRecoilSpring, 0f, ref _handRecoilVelocity, 240f, 14f, dt);
        Squash.Update(dt);
        UpdateBambooAngularSprings(dt, input, world);
    }

    /// <summary>
    /// 更新定长竹节四肢的受力弯曲角弹簧（仅改变弯曲曲率角，绝对不拉伸手脚长度）。
    /// </summary>
    private void UpdateBambooAngularSprings(float dt, ControlInputState input, CollisionWorld? world)
    {
        float aimSign = _currentAim.X >= 0f ? 1f : -1f;
        bool isGrounded = Body.IsGrounded || world == null;

        Vector2 effectiveVel = world != null ? Body.Velocity : new Vector2(input.MoveVector.X * 240f, 0f);
        Vector2 rawAccel = (effectiveVel - _prevVelocity) / MathF.Max(dt, 0.002f);
        Vector2 bodyAccel = MathHelper2D.ClampLength(rawAccel, 9000f);
        _prevVelocity = effectiveVel;

        float currentAimAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        float aimAngularVel = MathHelper.WrapAngle(currentAimAngle - _prevAimAngle) / MathF.Max(dt, 0.002f);
        aimAngularVel = MathHelper.Clamp(aimAngularVel, -35f, 35f);
        _prevAimAngle = currentAimAngle;

        // 1. 双腿竹节弯曲角弹簧更新 (步态提膝弯曲 + 水平/垂直加速度惯性弯曲)
        for (int i = 0; i < 2; i++)
        {
            float phase = _walkCyclePhase + i * MathHelper.Pi;
            float sideSign = i == 0 ? 1f : -1f;

            float targetLegBend;
            if (isGrounded)
            {
                // 行走时随步伐交替弯曲，站立时微曲
                float stepFlex = MathF.Sin(phase) * 0.72f * _visualSpeedFactor * _visualMoveDir;
                float idleFlex = sideSign * 0.24f * (1f - _visualSpeedFactor);
                targetLegBend = stepFlex + idleFlex;
            }
            else
            {
                // 腾空时根据速度产生优雅流线型弯曲，绝不拉长腿部
                float airVelBend = MathHelper.Clamp(Body.Velocity.X * 0.0014f, -0.65f, 0.65f);
                float fallArch = MathHelper.Clamp(Body.Velocity.Y * 0.0006f, -0.45f, 0.45f) * sideSign;
                targetLegBend = airVelBend + fallArch;
            }

            // 惯性力矩激励竹节弹性回振
            _legBendAngularVel[i] += (bodyAccel.X * 0.0032f + bodyAccel.Y * 0.0018f * sideSign) * dt;
            _legBendAngle[i] = MathHelper2D.SpringDamp(_legBendAngle[i], targetLegBend, ref _legBendAngularVel[i], 215f, 7.8f, dt);
            _legBendAngle[i] = MathHelper.Clamp(_legBendAngle[i], -1.25f, 1.25f);
        }

        // 2. 主手竹节弯曲角弹簧更新 (瞄准甩动 + 射击后坐弯曲 + 运动惯性)
        float targetMainBend = aimSign * 0.22f + MathF.Sin(_idlePhase) * 0.06f;
        _mainArmBendAngularVel += (-aimAngularVel * 0.45f + bodyAccel.Y * 0.0028f * aimSign) * dt;
        _mainArmBendAngle = MathHelper2D.SpringDamp(_mainArmBendAngle, targetMainBend, ref _mainArmBendAngularVel, 230f, 8.2f, dt);
        _mainArmBendAngle = MathHelper.Clamp(_mainArmBendAngle, -1.20f, 1.20f);

        // 3. 副手竹节弯曲角弹簧更新 (步伐摆动弯曲 + 空中飘动弯曲)
        float targetOffBend;
        if (isGrounded)
        {
            targetOffBend = -aimSign * (0.35f + MathF.Sin(_walkCyclePhase) * 0.55f * _visualSpeedFactor);
        }
        else
        {
            targetOffBend = -aimSign * (0.45f + MathHelper.Clamp(Body.Velocity.Y * 0.0008f, -0.5f, 0.5f));
        }
        _offArmBendAngularVel += (-bodyAccel.X * 0.0030f - bodyAccel.Y * 0.0032f * aimSign) * dt;
        _offArmBendAngle = MathHelper2D.SpringDamp(_offArmBendAngle, targetOffBend, ref _offArmBendAngularVel, 195f, 7.2f, dt);
        _offArmBendAngle = MathHelper.Clamp(_offArmBendAngle, -1.25f, 1.25f);
    }

    /// <summary>
    /// 执行一次射击，消耗弹匣子弹并向躯干与竹节手脚施加矢量后坐力弯曲。
    /// </summary>
    private void Shoot(ParticleSystem particles, ScreenShake shake, Action<Projectile> onSpawnProjectile)
    {
        CurrentFireCooldown = FireRateCooldown;
        CurrentAmmo--;
        if (CurrentAmmo <= 0)
        {
            ReloadTimer = ReloadDuration;
        }

        float aimSign = _currentAim.X >= 0f ? 1f : -1f;

        // 产生枪械后坐力反向冲量
        Vector2 recoilDir = -_currentAim;
        Body.ApplyImpulse(recoilDir * RecoilImpulse);

        // 触发受力挤压形变 (适度弹性反冲)
        Squash.ApplySquash(recoilDir, 0.24f);

        // 手部反冲弹簧回缩 + 定长竹节手脚产生后坐弯曲震荡
        _handRecoilVelocity += 16f;
        _mainArmBendAngularVel += aimSign * 14.5f;
        _offArmBendAngularVel -= aimSign * 11.5f;
        _legBendAngularVel[0] += recoilDir.X * 8.5f;
        _legBendAngularVel[1] += recoilDir.X * 8.5f;

        // 枪口火光与屏幕震颤
        particles.EmitHitSparks(Body.Position + _currentAim * (Radius + 18f), _currentAim, ThemeColor, 8);
        shake.AddTrauma(0.18f);

        // 循环发射子弹 (支持多重散射卡牌)
        float baseAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        float step = BulletsPerShot > 1 ? BulletSpread / (BulletsPerShot - 1) : 0f;
        float startAngle = BulletsPerShot > 1 ? baseAngle - BulletSpread * 0.5f : baseAngle;

        for (int i = 0; i < BulletsPerShot; i++)
        {
            float angle = startAngle + i * step;
            angle += (float)(Random.Shared.NextDouble() - 0.5) * 0.04f;
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));

            Vector2 spawnPos = Body.Position + dir * (Radius + 18f);
            Vector2 bulletVel = dir * BulletSpeed;

            var proj = new Projectile(this, spawnPos, bulletVel, BulletDamage, ThemeColor)
            {
                RemainingBounces = BulletBounces,
                ExplosionRadius = ExplosionRadius,
                IsPoisonous = PoisonBullets,
                HasImplosion = ImplosionBullets,
                SplitOnBounceCount = SplitOnBounceCount
            };

            foreach (var card in AppliedCards)
            {
                card.OnProjectileSpawn(this, proj);
            }

            onSpawnProjectile(proj);
        }
    }

    /// <summary>
    /// 受到外部打击承受伤害并产生打击冲量，带动定长竹节手脚弹性弯曲。
    /// </summary>
    public void TakeDamage(float amount, Actor? attacker, Vector2 hitForce, ParticleSystem particles, ScreenShake shake)
    {
        if (IsInvulnerable || IsDead) return;

        CurrentHealth = MathF.Max(0f, CurrentHealth - amount);
        HitFlashTimer = 0.08f;

        // 吸收冲击动量与适度压扁形变
        if (hitForce.LengthSquared() > 0.01f)
        {
            Vector2 hitDir = MathHelper2D.SafeNormalize(hitForce);
            Body.ApplyImpulse(hitForce);
            Squash.ApplySquash(hitDir, 0.34f);

            // 受击冲击力使竹节手脚向反方向大幅度弯曲震荡
            float hitSign = hitDir.X >= 0f ? 1f : -1f;
            _legBendAngularVel[0] += hitSign * 14f;
            _legBendAngularVel[1] += hitSign * 14f;
            _mainArmBendAngularVel -= hitSign * 15f;
            _offArmBendAngularVel += hitSign * 16f;
        }

        particles.EmitHitSparks(Body.Position, -MathHelper2D.SafeNormalize(hitForce), ThemeColor, 14);
        shake.AddTrauma(0.25f);
    }

    /// <summary>
    /// 重置角色的战斗生命状态与位置（用于新回合启动）。
    /// </summary>
    public void ResetState(Vector2 position)
    {
        Body.Position = position;
        Body.Velocity = Vector2.Zero;
        Body.IsGrounded = false;
        Body.IsTouchingWall = false;
        Body.WallNormal = Vector2.Zero;
        CurrentHealth = MaxHealth;
        CurrentAmmo = MaxAmmo;
        ReloadTimer = 0f;
        CurrentFireCooldown = 0f;
        PoisonTimer = 0f;
        HitFlashTimer = 0f;
        _groundCoyoteTimer = 0f;
        _wallCoyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _prevJumpDown = false;
        _wasGroundedLastFrame = true;
        _prevFallVelocity = 0f;
        _lastWallNormal = Vector2.Zero;
        IsWallSliding = false;
        _prevVelocity = Vector2.Zero;
        _legBendAngle[0] = _legBendAngle[1] = 0f;
        _legBendAngularVel[0] = _legBendAngularVel[1] = 0f;
        _mainArmBendAngle = _mainArmBendAngularVel = 0f;
        _offArmBendAngle = _offArmBendAngularVel = 0f;
        RecalculateHealthScaling();
        if (SubWeapon != null) SubWeapon.CurrentCooldown = 0f;
    }

    /// <summary>
    /// 纯程序化渲染角色：身体边缘锚定的黑色定长弹性竹节手脚、球形身体、多样化眼神表情与非旋转换弹枪械。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDead) return;

        Vector2 pos = Body.Position;
        Color renderColor = HitFlashTimer > 0f ? ColorPalette.WhiteFlash : ThemeColor;
        Color limbBlack = new(10, 12, 16);
        Color limbRim = new(44, 50, 66, 150);
        Color outlineColor = new(14, 18, 28, 235);
        float sizeRatio = Radius / BaseRadius;
        float aimSign = _currentAim.X >= 0f ? 1f : -1f;
        float moveDir = _visualSpeedFactor > 0.08f ? _visualMoveDir : aimSign;

        float walkBobY = (Body.IsGrounded || MathF.Abs(Body.Velocity.Y) < 10f)
            ? -MathF.Abs(MathF.Sin(_walkCyclePhase)) * 2.0f * sizeRatio * _visualSpeedFactor
            : 0f;
        float visualBodyRadius = Radius * 0.78f;
        Vector2 bodyPos = pos + new Vector2(0f, -Radius * 0.22f + walkBobY);

        // 精简紧凑的固定手脚长度（无论跳跃或高速运动，弧长恒定不变）
        float legFixedLen = 14.5f * sizeRatio;
        float armFixedLen = 11.5f * sizeRatio;
        float limbStartW = 3.0f * sizeRatio;
        float limbEndW = 2.2f * sizeRatio;

        // =========================================================================
        // 1. 绘制身体下边缘锚定的黑色定长竹节双腿 (仅随受力和步态弯曲，绝不拉长)
        // =========================================================================
        Vector2 leftLegRoot = GetDeformedPerimeterPoint(bodyPos, visualBodyRadius * 0.96f, MathHelper.PiOver2 + 0.34f);
        Vector2 rightLegRoot = GetDeformedPerimeterPoint(bodyPos, visualBodyRadius * 0.96f, MathHelper.PiOver2 - 0.34f);

        float strideSwing0 = MathF.Sin(_walkCyclePhase) * 0.45f * _visualSpeedFactor * moveDir;
        float strideSwing1 = MathF.Sin(_walkCyclePhase + MathHelper.Pi) * 0.45f * _visualSpeedFactor * moveDir;
        float airTilt = (!Body.IsGrounded && MathF.Abs(Body.Velocity.Y) > 10f)
            ? MathHelper.Clamp(Body.Velocity.X * 0.0007f, -0.35f, 0.35f)
            : 0f;

        float leftLegBaseAngle = MathHelper.PiOver2 + 0.12f * (1f - _visualSpeedFactor) + strideSwing0 + airTilt;
        float rightLegBaseAngle = MathHelper.PiOver2 - 0.12f * (1f - _visualSpeedFactor) + strideSwing1 + airTilt;

        batcher.DrawFixedLengthBambooLimb(spriteBatch, leftLegRoot, legFixedLen, leftLegBaseAngle, _legBendAngle[0], limbStartW, limbEndW, limbBlack, limbRim, 0.9f);
        batcher.DrawFixedLengthBambooLimb(spriteBatch, rightLegRoot, legFixedLen, rightLegBaseAngle, _legBendAngle[1], limbStartW, limbEndW, limbBlack, limbRim, 0.9f);

        // =========================================================================
        // 2. 绘制身体后侧边缘锚定的黑色定长竹节副手
        // =========================================================================
        float offEdgeAngle = aimSign >= 0f ? (MathHelper.Pi - 0.20f) : 0.20f;
        Vector2 offArmRoot = GetDeformedPerimeterPoint(bodyPos, visualBodyRadius * 0.96f, offEdgeAngle);
        float offSwing = MathF.Sin(_walkCyclePhase + MathHelper.Pi) * 0.38f * _visualSpeedFactor;
        float offBaseAngle = (aimSign >= 0f ? (MathHelper.Pi - 0.55f) : 0.55f) + offSwing;

        batcher.DrawFixedLengthBambooLimb(spriteBatch, offArmRoot, armFixedLen, offBaseAngle, _offArmBendAngle, 2.8f * sizeRatio, 2.0f * sizeRatio, limbBlack, limbRim, 0.9f);

        // =========================================================================
        // 3. 绘制角色弹性球形身体 (带适度 Squash & Stretch、内部高光与同步描边)
        // =========================================================================
        batcher.DrawDeformedCircle(
            spriteBatch,
            bodyPos,
            visualBodyRadius,
            Squash.CurrentScale,
            Squash.DeformationAngle,
            renderColor
        );

        Vector2 highlightPos = bodyPos + new Vector2(-visualBodyRadius * 0.22f, -visualBodyRadius * 0.28f);
        batcher.DrawDeformedCircle(
            spriteBatch,
            highlightPos,
            visualBodyRadius * 0.40f,
            Squash.CurrentScale * new Vector2(0.95f, 0.65f),
            Squash.DeformationAngle - 0.3f,
            Color.White * 0.22f
        );

        batcher.DrawDeformedCircleOutline(
            spriteBatch,
            bodyPos,
            visualBodyRadius,
            Squash.CurrentScale,
            Squash.DeformationAngle,
            outlineColor,
            2.8f
        );

        // =========================================================================
        // 4. 绘制角色面部眼神表情 (支持 6 种预设：经典 / 卖萌 / 猥琐 / 搞怪 / 笑眼 / 死鱼)
        // =========================================================================
        DrawEyesAndExpression(batcher, spriteBatch, bodyPos, visualBodyRadius, sizeRatio, renderColor, outlineColor);

        // =========================================================================
        // 5. 绘制身体前侧边缘锚定的黑色定长竹节主手与枪械武器 (换弹时枪不旋转)
        // =========================================================================
        float aimAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        float mainEdgeAngle = aimAngle + aimSign * 0.18f;
        Vector2 mainArmRoot = GetDeformedPerimeterPoint(bodyPos, visualBodyRadius * 0.96f, mainEdgeAngle);

        // 后坐力仅改变主手起始仰角与竹节弯曲度，不改变手臂长度
        float recoilAngleOffset = -aimSign * MathHelper.Clamp(_handRecoilSpring * 0.045f, -0.45f, 0.45f);
        Vector2 handTipPos = batcher.DrawFixedLengthBambooLimb(
            spriteBatch,
            mainArmRoot,
            armFixedLen,
            aimAngle + recoilAngleOffset,
            _mainArmBendAngle,
            2.8f * sizeRatio,
            2.0f * sizeRatio,
            limbBlack,
            limbRim,
            0.9f
        );

        // 枪械朝向始终稳定跟随瞄准方向（换弹时不旋转）
        Vector2 gunDir = _currentAim;
        Vector2 gunPerp = new(-gunDir.Y, gunDir.X);
        if (aimSign < 0f) gunPerp = -gunPerp;

        // 枪柄 (Grip)
        batcher.DrawLine(spriteBatch, handTipPos, handTipPos - gunPerp * (7.2f * sizeRatio) - gunDir * (2.0f * sizeRatio), new Color(32, 36, 46), 4.4f * sizeRatio);
        // 枪管外框与护木 (Receiver & Barrel)
        Vector2 gunStart = handTipPos - gunDir * (3.8f * sizeRatio);
        Vector2 gunEnd = handTipPos + gunDir * (16.5f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd, outlineColor, 8.0f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd - gunDir * (1.8f * sizeRatio), new Color(55, 62, 78), 4.8f * sizeRatio);
        // 枪身主题色能量槽
        Color energyColor = IsReloading ? Color.Gray * 0.5f : ThemeColor;
        batcher.DrawLine(spriteBatch, handTipPos, handTipPos + gunDir * (9.0f * sizeRatio), energyColor, 2.3f * sizeRatio);

        // 黑色手部末端握持圆点
        batcher.DrawFilledCircle(spriteBatch, handTipPos, 2.6f * sizeRatio, limbBlack);

        // =========================================================================
        // 6. 头顶弹匣余量与换弹进度条 + 副武器特效
        // =========================================================================
        DrawAmmoAndReloadIndicator(batcher, spriteBatch, pos, sizeRatio);
        SubWeapon?.Draw(batcher, spriteBatch, this);
    }

    /// <summary>
    /// 计算形变椭圆身体边缘指定世界角度方向上的精确锚点坐标，确保手脚始终紧贴在身体边缘。
    /// </summary>
    private Vector2 GetDeformedPerimeterPoint(Vector2 center, float baseRadius, float worldAngle)
    {
        Vector2 scale = Squash.CurrentScale;
        float rot = Squash.DeformationAngle;
        float localAngle = worldAngle - rot;

        float localX = MathF.Cos(localAngle) * baseRadius * scale.X;
        float localY = MathF.Sin(localAngle) * baseRadius * scale.Y;

        float cosR = MathF.Cos(rot);
        float sinR = MathF.Sin(rot);

        return new Vector2(
            center.X + (localX * cosR - localY * sinR),
            center.Y + (localX * sinR + localY * cosR)
        );
    }

    /// <summary>
    /// 根据角色的 EyeStyle 绘制不同性格的横向双眼与表情细节。
    /// </summary>
    private void DrawEyesAndExpression(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        Vector2 bodyPos,
        float visualBodyRadius,
        float sizeRatio,
        Color bodyColor,
        Color outlineColor)
    {
        Vector2 lookShift = new(_currentAim.X * visualBodyRadius * 0.30f, _currentAim.Y * visualBodyRadius * 0.18f - visualBodyRadius * 0.08f);
        Vector2 faceCenter = bodyPos + lookShift;
        float eyeSpacingX = 6.6f * sizeRatio;
        Vector2 leftEyePos = faceCenter + new Vector2(-eyeSpacingX, 0f);
        Vector2 rightEyePos = faceCenter + new Vector2(eyeSpacingX, 0f);

        if (HitFlashTimer > 0f)
        {
            // 受击眩晕 X 眼
            float cross = 3.8f * sizeRatio;
            batcher.DrawLine(spriteBatch, leftEyePos - new Vector2(cross), leftEyePos + new Vector2(cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, leftEyePos + new Vector2(-cross, cross), leftEyePos + new Vector2(cross, -cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, rightEyePos - new Vector2(cross), rightEyePos + new Vector2(cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, rightEyePos + new Vector2(-cross, cross), rightEyePos + new Vector2(cross, -cross), outlineColor, 2.4f);
            return;
        }

        float leftPersp = _currentAim.X < 0f ? 1.08f : 0.92f;
        float rightPersp = _currentAim.X >= 0f ? 1.08f : 0.92f;
        float baseEyeR = 4.6f * sizeRatio;

        switch (EyeStyle)
        {
            case EyeStyle.Cute:
                // 水汪萌眼：超大亮瞳 + 双重星光高光 + 粉嫩腮红 + 弯弯小嘴
                DrawCuteEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.24f * leftPersp, _currentAim, outlineColor, isLeft: true);
                DrawCuteEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.24f * rightPersp, _currentAim, outlineColor, isLeft: false);
                // 粉红可爱腮红
                Color blushColor = new Color(255, 115, 160) * 0.62f;
                batcher.DrawDeformedCircle(spriteBatch, leftEyePos + new Vector2(-2.2f * sizeRatio, 5.2f * sizeRatio), 3.4f * sizeRatio, new Vector2(1.4f, 0.75f), 0f, blushColor);
                batcher.DrawDeformedCircle(spriteBatch, rightEyePos + new Vector2(2.2f * sizeRatio, 5.2f * sizeRatio), 3.4f * sizeRatio, new Vector2(1.4f, 0.75f), 0f, blushColor);
                // 萌系小猫嘴
                Vector2 cuteMouth = faceCenter + new Vector2(0f, 5.5f * sizeRatio);
                batcher.DrawArc(spriteBatch, cuteMouth + new Vector2(-2.0f * sizeRatio, 0f), 2.0f * sizeRatio, 0.2f, MathHelper.Pi - 0.4f, outlineColor, 1.8f);
                batcher.DrawArc(spriteBatch, cuteMouth + new Vector2(2.0f * sizeRatio, 0f), 2.0f * sizeRatio, 0.2f, MathHelper.Pi - 0.4f, outlineColor, 1.8f);
                break;

            case EyeStyle.Sneaky:
                // 猥琐眯眯眼：半阖眼皮 + 贼溜溜斜视小眼珠 + 挑眉坏笑
                DrawSneakyEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.08f * leftPersp, _currentAim, bodyColor, outlineColor, isLeft: true);
                DrawSneakyEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.08f * rightPersp, _currentAim, bodyColor, outlineColor, isLeft: false);
                // 歪嘴猥琐坏笑
                Vector2 smirkCenter = faceCenter + new Vector2(1.5f * sizeRatio, 5.0f * sizeRatio);
                batcher.DrawArc(spriteBatch, smirkCenter, 4.5f * sizeRatio, 0.15f, MathHelper.Pi * 0.65f, outlineColor, 2.2f);
                break;

            case EyeStyle.Derp:
                // 智慧斗鸡眼：一大一小不对称眼眶 + 对眼乱转瞳孔 + 呆萌圆嘴
                float wobble = MathF.Sin(_idlePhase * 1.5f + _walkCyclePhase) * 0.25f;
                Vector2 derpAimL = MathHelper2D.SafeNormalize(new Vector2(0.75f, -0.35f + wobble));
                Vector2 derpAimR = MathHelper2D.SafeNormalize(new Vector2(-0.80f, 0.35f - wobble));
                DrawClassicEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.32f, derpAimL, outlineColor, isLeft: true, browTiltOverride: -1.5f);
                DrawClassicEye(batcher, spriteBatch, rightEyePos, baseEyeR * 0.78f, derpAimR, outlineColor, isLeft: false, browTiltOverride: -1.8f);
                // 呆滞小圆嘴 "o"
                Vector2 derpMouth = faceCenter + new Vector2(0.5f * sizeRatio, 6.2f * sizeRatio);
                batcher.DrawFilledCircle(spriteBatch, derpMouth, 2.6f * sizeRatio, outlineColor);
                batcher.DrawFilledCircle(spriteBatch, derpMouth, 1.3f * sizeRatio, new Color(220, 80, 95));
                break;

            case EyeStyle.Happy:
                // 腹黑笑眼：弯弯月牙笑眼 (^ ^) + 腮红 + 露齿微笑
                DrawHappyArcEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.05f * leftPersp, outlineColor, sizeRatio);
                DrawHappyArcEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.05f * rightPersp, outlineColor, sizeRatio);
                Color happyBlush = new Color(255, 120, 150) * 0.55f;
                batcher.DrawDeformedCircle(spriteBatch, leftEyePos + new Vector2(-2.0f * sizeRatio, 4.2f * sizeRatio), 3.0f * sizeRatio, new Vector2(1.35f, 0.7f), 0f, happyBlush);
                batcher.DrawDeformedCircle(spriteBatch, rightEyePos + new Vector2(2.0f * sizeRatio, 4.2f * sizeRatio), 3.0f * sizeRatio, new Vector2(1.35f, 0.7f), 0f, happyBlush);
                batcher.DrawArc(spriteBatch, faceCenter + new Vector2(0f, 3.8f * sizeRatio), 3.6f * sizeRatio, 0.25f, MathHelper.Pi - 0.5f, outlineColor, 2.0f);
                break;

            case EyeStyle.Crazy:
                // 狂暴死鱼眼：夸张瞪大眼白 + 针尖小瞳孔 + 浓重黑眼圈与暴躁立眉
                DrawCrazyEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.18f * leftPersp, _currentAim, outlineColor, isLeft: true);
                DrawCrazyEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.18f * rightPersp, _currentAim, outlineColor, isLeft: false);
                break;

            case EyeStyle.Classic:
            default:
                DrawClassicEye(batcher, spriteBatch, leftEyePos, baseEyeR * leftPersp, _currentAim, outlineColor, isLeft: true);
                DrawClassicEye(batcher, spriteBatch, rightEyePos, baseEyeR * rightPersp, _currentAim, outlineColor, isLeft: false);
                break;
        }
    }

    private static void DrawClassicEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft, float? browTiltOverride = null)
    {
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.4f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, Color.White);

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.36f);
        float pupilR = eyeR * 0.58f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(18, 20, 30));

        Vector2 highlightPos = pupilPos + new Vector2(-pupilR * 0.35f, -pupilR * 0.35f);
        batcher.DrawFilledCircle(spriteBatch, highlightPos, pupilR * 0.36f, Color.White);

        float browTilt = browTiltOverride ?? (isLeft ? 1.6f : -1.6f);
        Vector2 browLeft = eyePos + new Vector2(-eyeR * 0.85f, -eyeR * 1.15f - browTilt);
        Vector2 browRight = eyePos + new Vector2(eyeR * 0.85f, -eyeR * 1.15f + browTilt);
        batcher.DrawLine(spriteBatch, browLeft, browRight, outlineColor, 2.2f);
    }

    private static void DrawCuteEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft)
    {
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.5f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, Color.White);

        // 水汪汪超大深色瞳孔
        Vector2 pupilPos = eyePos + aim * (eyeR * 0.25f);
        float pupilR = eyeR * 0.74f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(22, 24, 42));

        // 双重闪亮高光（左上大高光 + 右下小星点）
        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(-pupilR * 0.36f, -pupilR * 0.36f), pupilR * 0.42f, Color.White);
        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(pupilR * 0.32f, pupilR * 0.32f), pupilR * 0.20f, Color.White);

        // 外侧俏皮小睫毛
        float lashDir = isLeft ? -1f : 1f;
        Vector2 lashStart = eyePos + new Vector2(lashDir * eyeR * 0.85f, -eyeR * 0.45f);
        batcher.DrawLine(spriteBatch, lashStart, lashStart + new Vector2(lashDir * 2.8f, -2.2f), outlineColor, 2.0f);
    }

    private static void DrawSneakyEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color bodyColor, Color outlineColor, bool isLeft)
    {
        // 底层眼白与贼溜溜小眼珠
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.2f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, new Color(245, 245, 230));

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.48f);
        float pupilR = eyeR * 0.46f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(18, 20, 30));
        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(-pupilR * 0.3f, -pupilR * 0.3f), pupilR * 0.32f, Color.White);

        // 上下合拢的猥琐半眯眼皮遮罩
        Rectangle topLid = new((int)(eyePos.X - eyeR - 2f), (int)(eyePos.Y - eyeR - 2f), (int)((eyeR + 2f) * 2f), (int)(eyeR * 0.95f));
        Rectangle botLid = new((int)(eyePos.X - eyeR - 2f), (int)(eyePos.Y + eyeR * 0.32f), (int)((eyeR + 2f) * 2f), (int)(eyeR * 0.90f));
        batcher.DrawRectangle(spriteBatch, topLid, bodyColor);
        batcher.DrawRectangle(spriteBatch, botLid, bodyColor);

        // 眯眯眼上下眼睑粗线
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 1.15f, -eyeR * 0.12f), eyePos + new Vector2(eyeR * 1.15f, -eyeR * 0.12f), outlineColor, 2.4f);
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 1.05f, eyeR * 0.35f), eyePos + new Vector2(eyeR * 1.05f, eyeR * 0.35f), outlineColor, 2.0f);

        // 猥琐高低挑眉
        float browY = isLeft ? -eyeR * 1.25f : -eyeR * 0.95f;
        float tilt = isLeft ? -1.5f : 1.8f;
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 0.9f, browY - tilt), eyePos + new Vector2(eyeR * 0.9f, browY + tilt), outlineColor, 2.2f);
    }

    private static void DrawHappyArcEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Color outlineColor, float sizeRatio)
    {
        // 向上弯起的月牙笑眼 (^ ^)
        Vector2 arcCenter = eyePos + new Vector2(0f, eyeR * 0.35f);
        batcher.DrawArc(spriteBatch, arcCenter, eyeR * 0.90f, MathHelper.Pi + 0.35f, MathHelper.Pi - 0.70f, outlineColor, 3.0f * sizeRatio);
    }

    private static void DrawCrazyEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft)
    {
        // 浓重黑眼圈下眼袋
        batcher.DrawArc(spriteBatch, eyePos + new Vector2(0f, 1.2f), eyeR + 2.2f, 0.25f, MathHelper.Pi - 0.5f, outlineColor * 0.65f, 2.2f);

        // 夸张暴突大眼白
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.5f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, new Color(255, 248, 245));

        // 针尖大小的高压红环瞳孔
        Vector2 pupilPos = eyePos + aim * (eyeR * 0.32f);
        batcher.DrawFilledCircle(spriteBatch, pupilPos, eyeR * 0.36f, new Color(220, 50, 50));
        batcher.DrawFilledCircle(spriteBatch, pupilPos, eyeR * 0.20f, new Color(15, 15, 22));

        // 暴躁陡峭怒眉
        float browTilt = isLeft ? 2.8f : -2.8f;
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 0.95f, -eyeR * 1.05f - browTilt), eyePos + new Vector2(eyeR * 0.95f, -eyeR * 1.05f + browTilt), outlineColor, 2.6f);
    }

    private void DrawAmmoAndReloadIndicator(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 pos, float sizeRatio)
    {
        Vector2 indicatorCenter = pos + new Vector2(0f, -Radius * 1.42f - 10f);

        if (IsReloading)
        {
            // 换弹进度条
            float barW = 42f * sizeRatio;
            float barH = 6f;
            float progress = MathHelper.Clamp(1f - (ReloadTimer / ReloadDuration), 0f, 1f);
            Rectangle bgRect = new((int)(indicatorCenter.X - barW * 0.5f), (int)(indicatorCenter.Y - barH * 0.5f), (int)barW, (int)barH);
            Rectangle fillRect = new(bgRect.X, bgRect.Y, (int)(barW * progress), (int)barH);

            batcher.DrawRectangle(spriteBatch, bgRect, Color.Black * 0.7f);
            batcher.DrawRectangle(spriteBatch, fillRect, ColorPalette.PlayerYellow);
            batcher.DrawRectangleOutline(spriteBatch, bgRect, Color.White * 0.8f, 1.2f);
        }
        else
        {
            // 弹匣剩余子弹颗粒指示器
            int displayMax = Math.Min(MaxAmmo, 10);
            float spacing = 9.5f;
            float totalW = (displayMax - 1) * spacing;
            float startX = indicatorCenter.X - totalW * 0.5f;

            for (int i = 0; i < displayMax; i++)
            {
                Vector2 pipPos = new(startX + i * spacing, indicatorCenter.Y);
                bool hasBullet = i < CurrentAmmo;
                if (hasBullet)
                {
                    batcher.DrawFilledCircle(spriteBatch, pipPos, 3.6f, ThemeColor);
                    batcher.DrawFilledCircle(spriteBatch, pipPos, 1.8f, Color.White);
                }
                else
                {
                    batcher.DrawCircleOutline(spriteBatch, pipPos, 3.2f, Color.Gray * 0.45f, 1.2f);
                }
            }
        }
    }
}
