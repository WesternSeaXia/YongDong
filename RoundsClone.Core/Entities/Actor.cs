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
/// 游戏核心角色实体。
/// 职责边界：负责维护角色的战斗属性数值、刚体与碰撞体绑定、矢量力移动与贴墙跳跃机制、武器换弹射击、高空坠落撞击伤害及受击状态结算；
/// 四肢竹节弹簧动画委托给 <see cref="ActorLimbAnimator"/>，外观与五官绘制委托给 <see cref="ActorRenderer"/>。
/// </summary>
public class Actor
{
    /// <summary>
    /// 角色名称标识。
    /// </summary>
    public string Name { get; set; } = "Player";

    /// <summary>
    /// 是否由 AI 控制器托管。
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
    /// 圆形物理碰撞体。
    /// </summary>
    public CircleCollider Collider { get; }

    /// <summary>
    /// 虎克定律弹簧阻尼形变模型 (Squash & Stretch)。
    /// </summary>
    public SpringSquashStretch Squash { get; } = new();

    /// <summary>
    /// 定长竹节四肢与步态角弹簧动画控制器。
    /// </summary>
    public ActorLimbAnimator LimbAnimator { get; } = new();

    // --- 核心基准力学常数 ---
    public const float BaseRadius = 26f;
    public const float BaseMass = 1.0f;
    public const float BaseMoveForce = 2600f; // 基础水平推力 2600 N，终端奔跑最高时速约 394 px/s（约 6.6 格/秒）
    public const float BaseJumpImpulse = 1263f; // 起跳初速度 1263 px/s，对应极限跳跃高度 182 px（精确等于角色身高 52 px 的 3.5 倍）

    /// <summary>
    /// 角色当前物理碰撞半径（像素），随最大生命值动态缩放。
    /// </summary>
    public float Radius { get; set; } = BaseRadius;

    /// <summary>
    /// 角色主题色。
    /// </summary>
    public Color ThemeColor { get; set; } = ColorPalette.PlayerBlue;

    // --- 生命与机动属性体系 ---
    public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth { get; set; } = 100f;
    public bool IsDead => CurrentHealth <= 0f;

    public float MoveForce { get; set; } = BaseMoveForce;
    public float JumpImpulse { get; set; } = BaseJumpImpulse;

    /// <summary>
    /// 角色最大连续跳跃次数（默认 1 次：高处走下平台在空中仍可跳跃 1 次，着陆后刷新；跳过一次后空中不可二段跳）。
    /// </summary>
    public int MaxJumps { get; set; } = 1;

    /// <summary>
    /// 角色当前剩余可用跳跃次数（着陆或触地后立即无条件刷新为 MaxJumps）。
    /// </summary>
    public int RemainingJumps { get; set; } = 1;

    // --- 主武器与弹匣换弹属性 ---
    public int MaxAmmo { get; set; } = 3;
    public int CurrentAmmo { get; set; } = 3;
    public float ReloadDuration { get; set; } = 1.5f;
    public float ReloadTimer { get; set; } = 0f;
    public bool IsReloading => ReloadTimer > 0f;

    public float FireRateCooldown { get; set; } = 0.35f;
    public float CurrentFireCooldown { get; set; } = 0f;
    public float BulletDamage { get; set; } = 25f;
    public float BulletSpeed { get; set; } = 1200f;
    public float BulletSizeMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// 初始子弹后坐力冲量（已调低以避免过度震退，配合身体/脚物理分离回正）。
    /// </summary>
    public float RecoilImpulse { get; set; } = 100f;

    public int BulletBounces { get; set; } = 0;
    public int BulletsPerShot { get; set; } = 1;
    public float BulletSpread { get; set; } = 0.05f;
    public float ExplosionRadius { get; set; } = 0f;
    public bool PoisonBullets { get; set; } = false;
    public bool ImplosionBullets { get; set; } = false;
    public int SplitOnBounceCount { get; set; } = 0;

    /// <summary>
    /// 副武器插槽（如力场格挡、疾风冲刺）。
    /// </summary>
    public ISubWeapon? SubWeapon { get; set; }

    /// <summary>
    /// 已生效的技能卡牌插件列表。
    /// </summary>
    public List<ICardPlugin> AppliedCards { get; } = new();

    // --- 战斗状态与平台跳跃缓冲计时器 ---
    public bool IsInvulnerable { get; set; } = false;
    public float HitFlashTimer { get; set; } = 0f;
    public float PoisonTimer { get; set; } = 0f;
    public float PoisonDps { get; set; } = 0f;
    public Actor? PoisonSource { get; set; } = null;
    public float SlowTimer { get; set; } = 0f;
    public float SlowFactor { get; set; } = 1.0f;
    public bool IsWallSliding { get; private set; } = false;

    // --- 进阶卡牌机制开关与计时器 ---
    public bool HasPhoenixRevive { get; set; } = false;
    public bool HasUsedPhoenix { get; set; } = false;
    public float InvulnerabilityTimer { get; set; } = 0f;
    public bool HasRadiance { get; set; } = false;
    public bool HasShieldsUp { get; set; } = false;
    public bool HasTeleport { get; set; } = false;
    public bool HasTargetBounce { get; set; } = false;
    public bool HasGrowBullet { get; set; } = false;
    public bool HasSneakyBullet { get; set; } = false;
    public bool HasToxicCloud { get; set; } = false;
    public bool HasScavenger { get; set; } = false;
    public bool HasVampiricLeech { get; set; } = false;
    public float LifestealRatio { get; set; } = 0f;
    public float SpeedBoostTimer { get; set; } = 0f;
    public float SpeedBoostFactor { get; set; } = 1.0f;
    public float RadianceTimer { get; set; } = 0f;
    public float ActiveTimer { get; private set; } = 0f;
    public event Action<Actor>? OnRadiancePulse;
    public event Action<Actor>? OnReloadCompleted;
    public event Action<Actor, ISubWeapon>? OnSubWeaponActivated;
    public event Action<Actor>? OnShieldTriggered;
    public event Action<Actor>? OnJumped;
    public event Action<Actor, int>? OnStep;
    public event Action<Actor>? OnPhoenixRevived;
    public event Action<Actor>? OnDryFire;
    public event Action<Actor>? OnPoisonTick;
    public event Action<Actor>? OnTeleported;

    private readonly BlockSubWeapon _passiveShield = new();

    /// <summary>
    /// 被动技能（如终极之盾）激发的紧急力场护盾。
    /// </summary>
    public BlockSubWeapon PassiveShield => _passiveShield;

    /// <summary>
    /// 角色当前是否有任何活跃的偏转护盾（主动力场格挡或终极之盾激发）。
    /// </summary>
    public bool HasActiveShield => (SubWeapon is BlockSubWeapon b && b.IsActive) || _passiveShield.IsActive;

    private float _stepTimer = 0f;
    private int _stepIndex = 0;
    private float _dryFireCooldown = 0f;

    /// <summary>
    /// 为角色施加移动加速增益（在持续时间内提升移动推力）。
    /// </summary>
    public void ApplySpeedBoost(float duration, float factor = 1.5f)
    {
        SpeedBoostTimer = MathF.Max(SpeedBoostTimer, duration);
        SpeedBoostFactor = MathF.Max(SpeedBoostFactor, factor);
    }

    private readonly List<FrostCrystalData> _frostCrystals = new();
    public IReadOnlyList<FrostCrystalData> FrostCrystals => _frostCrystals;

    /// <summary>
    /// 为角色施加移动减速负面状态，并在身体上随机生成晶莹的多面双锥棱柱冰晶。
    /// </summary>
    public void ApplySlow(float duration, float factor = 0.5f, bool isFrost = true)
    {
        SlowTimer = MathF.Max(SlowTimer, duration);
        SlowFactor = MathF.Min(SlowFactor, factor);

        if (isFrost && _frostCrystals.Count == 0)
        {
            GenerateRandomFrostCrystals();
        }
    }

    private void GenerateRandomFrostCrystals()
    {
        _frostCrystals.Clear();
        var rand = Random.Shared;
        int count = rand.Next(7, 11); // 随机 7 ~ 10 枚附着在身周与表面的细小冰渣
        for (int i = 0; i < count; i++)
        {
            float r = Radius * (0.45f + (float)rand.NextDouble() * 0.55f);
            float theta = (float)(rand.NextDouble() * MathHelper.TwoPi);
            Vector2 offset = new(MathF.Cos(theta) * r, MathF.Sin(theta) * r);
            float angle = (float)(rand.NextDouble() * MathHelper.TwoPi);
            float prismLen = (float)(rand.NextDouble() * 2.5 + 4.5); // 4.5px ~ 7.0px 细小冰渣长度
            float prismW = (float)(rand.NextDouble() * 1.0 + 1.8);   // 1.8px ~ 2.8px 细小冰渣宽度
            float pyrH = (float)(rand.NextDouble() * 0.8 + 1.5);     // 1.5px ~ 2.3px 冰渣微尖端高
            _frostCrystals.Add(new FrostCrystalData(offset, angle, prismLen, prismW, pyrH));
        }
    }

    /// <summary>
    /// 为角色施加带伤害来源归属的剧毒持续腐蚀。
    /// </summary>
    public void ApplyPoison(Actor? source, float totalPoisonDamage, float duration = 4.0f)
    {
        if (duration <= 0f) return;
        float remaining = PoisonDps * PoisonTimer;
        float newTotal = remaining + totalPoisonDamage;
        PoisonTimer = duration;
        PoisonDps = newTotal / duration;
        PoisonSource = source;
    }

    /// <summary>
    /// 当前归一化的瞄准朝向矢量。
    /// </summary>
    public Vector2 CurrentAim => _currentAim;
    private Vector2 _currentAim = Vector2.UnitX;

    private float _groundCoyoteTimer = 0f;
    private float _wallCoyoteTimer = 0f;
    private Vector2 _lastWallNormal = Vector2.Zero;
    private float _jumpBufferTimer = 0f;
    private bool _prevJumpDown = false;
    private bool _wasGroundedLastFrame = true;
    private float _prevFallVelocity = 0f;

    /// <summary>
    /// 构造角色实体并初始化刚体与碰撞体。
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
            Restitution = 0.05f,
            SlopeGrip = 1.0f // 启用角色专属坡道抓地，确保斜坡站立不溜坡、上下坡均速
        };

        Collider = new CircleCollider(Body, Radius);
        RecalculateHealthScaling();
    }

    /// <summary>
    /// 根据当前最大生命值动态重新计算角色体型与物理碰撞体半径。
    /// （移动推力、加速度与跳跃高度不再受体型牵连，统一由基础物理参数或卡牌词条显式控制）
    /// </summary>
    public void RecalculateHealthScaling()
    {
        float ratio = MathF.Max(0.5f, MaxHealth / 100f);

        Radius = BaseRadius * MathF.Pow(ratio, 0.40f);
        Collider.Radius = Radius;
        Body.Mass = BaseMass;
    }

    /// <summary>
    /// 帧更新角色移动、跳跃、武器射击、副武器及四肢弹簧动画。
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

        ActiveTimer += dt;

        // 1. 更新受击闪烁与持续毒素状态
        UpdateStatusEffects(dt, particles, shake);

        // 2. 更新瞄准方向与地面/斜坡移动推力
        if (input.AimVector.LengthSquared() > 0.001f)
        {
            _currentAim = MathHelper2D.SafeNormalize(input.AimVector);
        }

        if (world != null)
        {
            float moveInput = input.MoveVector.X;
            if (MathF.Abs(moveInput) > 0.05f)
            {
                float slowMult = (SlowTimer > 0f ? SlowFactor : 1.0f);
                float boostMult = (SpeedBoostTimer > 0f ? SpeedBoostFactor : 1.0f);
                float currentMoveForce = MoveForce * slowMult * boostMult;
                if (Body.IsGrounded && !Body.IsOnBouncySurface && MathF.Abs(Body.GroundNormal.X) > 0.05f)
                {
                    // 站在斜坡上移动：推力沿斜坡切线方向施加，让上下坡速度与平地完全一致
                    Vector2 tangent = MathHelper2D.SafeNormalize(new Vector2(-Body.GroundNormal.Y, Body.GroundNormal.X));
                    if (tangent.X < 0f) tangent = -tangent; // 确保正切线朝右
                    Body.AddForce(tangent * (moveInput * currentMoveForce));
                }
                else
                {
                    // 平地或空中：常规水平推力
                    Body.AddForce(new Vector2(moveInput * currentMoveForce, 0f));
                }
            }
            else if (Body.IsGrounded && !Body.IsOnBouncySurface && MathF.Abs(Body.GroundNormal.X) > 0.05f)
            {
                // 玩家松手未输入时：斜坡提供静摩擦防滑，防止重力自然滑坡；但不阻断击退与黑洞等强外力冲量
                Vector2 tangent = MathHelper2D.SafeNormalize(new Vector2(-Body.GroundNormal.Y, Body.GroundNormal.X));
                float tangentVel = Vector2.Dot(Body.Velocity, tangent);
                if (MathF.Abs(tangentVel) < 140f)
                {
                    // 自然重力微弱滑坡时彻底锁紧站定
                    Body.Velocity -= tangent * tangentVel;
                }
                else
                {
                    // 受到外界强冲量（黑洞牵引、爆炸冲击波、子弹击退）时应用平滑阻尼衰减，杜绝一帧瞬间死锁抹除速度
                    Body.Velocity -= tangent * (tangentVel * MathF.Min(1f, dt * 5.0f));
                }
            }
        }

        // 2.5. 更新地面行走脚步声节奏触发（支持斜坡下行不脱节）
        bool isSlopeWalking = false;
        if (!Body.IsGrounded && world != null && Body.Velocity.Y >= -20f && !input.IsJumpDown)
        {
            var downHit = world.Raycast(Body.Position, Vector2.UnitY, Radius + 14f, Collider);
            if (downHit.HasHit && downHit.Normal.Y < -0.3f)
            {
                isSlopeWalking = true;
                // 下坡微吸附，保持斜坡平滑贴地感
                if (downHit.Distance > Radius && downHit.Distance <= Radius + 10f && Body.Velocity.Y >= 0f)
                {
                    Body.Position = new Vector2(Body.Position.X, downHit.Point.Y - Radius);
                    Body.Velocity = new Vector2(Body.Velocity.X, MathF.Max(0f, Body.Velocity.Y * 0.2f));
                    Body.IsGrounded = true;
                    Body.GroundNormal = downHit.Normal;
                    _groundCoyoteTimer = 0.10f;
                }
            }
        }

        bool isGroundedOrSlope = (Body.IsGrounded || isSlopeWalking || (_groundCoyoteTimer > 0f && Body.Velocity.Y >= -10f && !input.IsJumpDown)) && !IsDead;

        if (isGroundedOrSlope && (MathF.Abs(Body.Velocity.X) > 35f || MathF.Abs(input.MoveVector.X) > 0.05f))
        {
            float speed = MathF.Max(MathF.Abs(Body.Velocity.X), MathF.Abs(input.MoveVector.X) * 220f);
            float stepInterval = MathHelper.Clamp(62f / MathF.Max(speed, 100f), 0.18f, 0.36f);
            _stepTimer += dt;
            if (_stepTimer >= stepInterval)
            {
                _stepTimer = 0f;
                int foot = _stepIndex++ % 2;
                OnStep?.Invoke(this, foot);
            }
        }
        else
        {
            // 离开地面或停步时保留一定步频记忆，杜绝下坡微颠簸单帧抹除步频
            if (_stepTimer > 0.10f)
            {
                _stepTimer -= dt * 0.5f;
            }
        }

        // 3. 更新跳跃缓冲、土狼时间、贴墙滑行与坠落缓冲/撞击伤害
        UpdateJumpAndWallSlide(dt, input, world, particles, shake);

        // 4. 更新主武器射击与弹匣换弹
        UpdateWeaponAndReload(dt, input, world, particles, shake, onSpawnProjectile);

        // 5. 更新副武器与被动护盾
        UpdateSubWeapon(dt, input, world, particles, shake);
        _passiveShield.Update(dt);

        // 6. 更新果冻形变与竹节四肢角弹簧动画器
        Squash.Update(dt);
        LimbAnimator.Update(this, input, world, dt);
    }

    private void UpdateStatusEffects(float dt, ParticleSystem particles, ScreenShake shake)
    {
        if (HitFlashTimer > 0f) HitFlashTimer -= dt;
        if (InvulnerabilityTimer > 0f)
        {
            InvulnerabilityTimer -= dt;
            if (InvulnerabilityTimer < 0f) InvulnerabilityTimer = 0f;
        }

        if (SlowTimer > 0f)
        {
            SlowTimer -= dt;
            if (SlowTimer <= 0f)
            {
                SlowFactor = 1.0f;
                _frostCrystals.Clear();
            }
        }
        else if (_frostCrystals.Count > 0)
        {
            _frostCrystals.Clear();
        }

        if (PoisonTimer > 0f)
        {
            PoisonTimer -= dt;
            float dmg = PoisonDps > 0f ? (PoisonDps * dt) : (12f * dt);
            TakeDamage(dmg, PoisonSource, Vector2.Zero, particles, shake);
            OnPoisonTick?.Invoke(this);
            if (Random.Shared.NextDouble() < 0.25)
            {
                particles.EmitHitSparks(Body.Position, Vector2.UnitY, ColorPalette.PoisonGreen, 2);
            }
            if (PoisonTimer <= 0f)
            {
                PoisonDps = 0f;
                PoisonSource = null;
            }
        }

        if (SpeedBoostTimer > 0f)
        {
            SpeedBoostTimer -= dt;
            if (SpeedBoostTimer <= 0f)
            {
                SpeedBoostFactor = 1.0f;
            }
            if (Random.Shared.NextDouble() < 0.30)
            {
                Vector2 sparkPos = Body.Position + new Vector2(
                    (float)(Random.Shared.NextDouble() - 0.5) * Radius * 1.2f,
                    (float)(Random.Shared.NextDouble() - 0.5) * Radius * 1.2f
                );
                Vector2 trailDir = Body.Velocity.LengthSquared() > 10f ? -MathHelper2D.SafeNormalize(Body.Velocity) : -Vector2.UnitY;
                particles.EmitHitSparks(sparkPos, trailDir, ColorPalette.PlayerGreen, 2);
            }
        }
    }

    private void UpdateJumpAndWallSlide(float dt, ControlInputState input, CollisionWorld? world, ParticleSystem particles, ScreenShake shake)
    {
        bool jumpJustPressed = input.IsJumpDown && !_prevJumpDown;
        _prevJumpDown = input.IsJumpDown;

        if (jumpJustPressed) _jumpBufferTimer = 0.14f;
        else if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;

        if (Body.IsGrounded)
        {
            _groundCoyoteTimer = 0.10f;
            RemainingJumps = MaxJumps; // 处于地面时持续保持跳跃次数充满
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

        // 贴墙缓缓下滑与摩擦火花（无论普通静态墙体还是悬挂箱，侧面均可贴边滑行）
        IsWallSliding = Body.IsTouchingWall && !Body.IsGrounded;
        if (IsWallSliding && Body.Velocity.Y > 0f)
        {
            const float maxSlideSpeed = 60f;
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

        float aimSign = _currentAim.X >= 0f ? 1f : -1f;

        // 执行跳跃逻辑（包含地面起跳、贴墙蹬跳与空中多段跳）
        if (_jumpBufferTimer > 0f)
        {
            if (_groundCoyoteTimer > 0f)
            {
                // 1. 地面跳跃（包括土狼时间内的起跳）
                float jumpX = MathF.Abs(input.MoveVector.X) > 0.05f ? Body.Velocity.X : 0f;
                Body.Velocity = new Vector2(jumpX, -JumpImpulse);
                Body.IsGrounded = false;
                Body.GroundNormal = -Vector2.UnitY;
                _groundCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                RemainingJumps = Math.Max(0, RemainingJumps - 1);

                Squash.ApplyStretch(Vector2.UnitY, 0.30f);
                LimbAnimator.OnGroundJump(aimSign, 1.0f);
                particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 7);
                OnJumped?.Invoke(this);
            }
            else if (_wallCoyoteTimer > 0f && _lastWallNormal != Vector2.Zero)
            {
                // 2. 贴墙蹬跳
                float wallKickX = _lastWallNormal.X * 300f;
                float wallJumpY = -JumpImpulse * 0.94f;

                Body.Velocity = new Vector2(wallKickX, wallJumpY);
                Body.IsTouchingWall = false;
                _wallCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                // 蹬墙起跳后消耗当前跳跃，落地或再次贴墙可重置
                RemainingJumps = 0;

                Vector2 jumpDir = MathHelper2D.SafeNormalize(new Vector2(wallKickX, wallJumpY));
                Squash.ApplyStretch(jumpDir, 0.32f);
                LimbAnimator.OnWallJump(_lastWallNormal.X, aimSign);

                Vector2 wallContactPos = Body.Position - _lastWallNormal * Radius;
                particles.EmitHitSparks(wallContactPos, _lastWallNormal, ThemeColor, 10);
                OnJumped?.Invoke(this);
            }
            else if (RemainingJumps > 0)
            {
                // 3. 空中跳跃（从高台走下/未起跳直接滞空时，仍可跳跃 1 次，落地刷新）
                float jumpX = Body.Velocity.X;
                if (MathF.Abs(input.MoveVector.X) > 0.05f)
                {
                    float targetDir = MathF.Sign(input.MoveVector.X);
                    // 空中机动变向强化：反向时大幅纠偏，同向时保持迅猛动力
                    if (MathF.Sign(jumpX) != targetDir)
                    {
                        jumpX = targetDir * MathF.Max(MathF.Abs(jumpX) * 0.5f, 220f);
                    }
                    else
                    {
                        jumpX = targetDir * MathF.Max(MathF.Abs(jumpX), 240f);
                    }
                }

                // 斩断空中急速下坠速度，凌空强力起跳升空
                Body.Velocity = new Vector2(jumpX, -JumpImpulse);
                RemainingJumps = 0; // 消耗本次跳跃，不可在空中二次起跳（非二段跳）
                _jumpBufferTimer = 0f;

                Squash.ApplyStretch(Vector2.UnitY, 0.35f);
                LimbAnimator.OnAirJump(aimSign);
                particles.EmitAirJump(Body.Position + new Vector2(0, Radius * 0.65f), ThemeColor);
                OnJumped?.Invoke(this);
            }
        }
        else if (input.IsJumpDown && Body.IsGrounded)
        {
            // 按住跳跃键在平地连续起跳
            float jumpX = MathF.Abs(input.MoveVector.X) > 0.05f ? Body.Velocity.X : 0f;
            Body.Velocity = new Vector2(jumpX, -JumpImpulse);
            Body.IsGrounded = false;
            Body.GroundNormal = -Vector2.UnitY;
            _groundCoyoteTimer = 0f;
            RemainingJumps = Math.Max(0, RemainingJumps - 1);

            Squash.ApplyStretch(Vector2.UnitY, 0.30f);
            LimbAnimator.OnGroundJump(aimSign, 0.94f);
            particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 6);
            OnJumped?.Invoke(this);
        }

        // 落地冲击判定与斜坡着陆吸附：区分高处极速坠落压弯受到撞击伤害 vs 普通落地缓冲
        if (Body.IsGrounded && !_wasGroundedLastFrame)
        {
            // 着陆时无条件刷新可用跳跃次数
            RemainingJumps = MaxJumps;

            // 斜坡着陆吸附：若无水平输入，消除切向滑冲量，手感同平地着陆稳稳站定不溜车
            if (!Body.IsOnBouncySurface && MathF.Abs(Body.GroundNormal.X) > 0.05f && MathF.Abs(input.MoveVector.X) <= 0.05f)
            {
                Body.Velocity = Vector2.Zero;
            }

            if (_prevFallVelocity > 150f)
            {
                if (_prevFallVelocity > 1600f && !Body.IsOnBouncySurface)
                {
                    // 超高空极速坠落或向下强力下砸时，触发身体深蹲受挫视觉压弯动画与扬尘（无掉落扣血伤害）
                    float excessSpeed = _prevFallVelocity - 1600f;
                    float buckleIntensity = MathHelper.Clamp(excessSpeed / 600f, 0.40f, 1.10f);
                    Squash.ApplySquash(Vector2.UnitY, MathHelper.Clamp(buckleIntensity * 0.40f, 0.25f, 0.55f));
                    LimbAnimator.OnHeavyFallImpact(buckleIntensity, aimSign);
                    particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 10);
                }
                else
                {
                    float landIntensity = MathHelper.Clamp(_prevFallVelocity / 1600f, 0.12f, 0.30f);
                    Squash.ApplySquash(Vector2.UnitY, landIntensity);
                    LimbAnimator.OnLandImpact(landIntensity, aimSign);
                    particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 6);
                }
            }
        }
        _wasGroundedLastFrame = Body.IsGrounded;
        _prevFallVelocity = Body.Velocity.Y;
    }

    private void UpdateWeaponAndReload(
        float dt,
        ControlInputState input,
        CollisionWorld? world,
        ParticleSystem particles,
        ScreenShake shake,
        Action<Projectile> onSpawnProjectile)
    {
        if (CurrentFireCooldown > 0f) CurrentFireCooldown -= dt;
        if (_dryFireCooldown > 0f) _dryFireCooldown -= dt;

        if (IsReloading)
        {
            if (input.IsFireDown && _dryFireCooldown <= 0f)
            {
                _dryFireCooldown = 0.22f;
                OnDryFire?.Invoke(this);
            }

            ReloadTimer -= dt;

            // 换弹日冕 (Radiance)：换弹期间持续向四周脉冲辐射太阳能量波
            if (HasRadiance)
            {
                float elapsedReload = ReloadDuration - ReloadTimer;
                // 日冕由小到大平滑展开（展开期 0.35s），展开完成达到最大范围后才开始产生攻击判定并脉冲
                if (elapsedReload >= 0.35f)
                {
                    RadianceTimer += dt;
                    if (RadianceTimer >= 0.28f)
                    {
                        RadianceTimer = 0f;
                        OnRadiancePulse?.Invoke(this);
                    }
                }
            }

            if (ReloadTimer <= 0f)
            {
                ReloadTimer = 0f;
                RadianceTimer = 0f;
                CurrentAmmo = MaxAmmo;
                float aimSign = _currentAim.X >= 0f ? 1f : -1f;
                LimbAnimator.OnReloadComplete(aimSign);
                Squash.ApplySquash(Vector2.UnitY, 0.15f);
                particles.EmitHitSparks(Body.Position + _currentAim * (Radius + 14f), _currentAim, Color.White, 5);
                OnReloadCompleted?.Invoke(this);
            }
        }
        else if (input.IsFireDown && CurrentFireCooldown <= 0f && CurrentAmmo > 0)
        {
            Shoot(world, particles, shake, onSpawnProjectile);
        }
    }

    private void UpdateSubWeapon(float dt, ControlInputState input, CollisionWorld? world, ParticleSystem particles, ScreenShake shake)
    {
        if (SubWeapon == null) return;

        SubWeapon.Update(dt);

        Vector2 subWeaponDir = _currentAim;
        if (VirtualViewport.IsMobile)
        {
            // 手机触屏端：副武器冲刺优先使用玩家当前摇杆所指引的方向
            // 1. 若移动摇杆正在推向某方向（且未处于开火瞄准状态），冲刺严格沿移动摇杆指引方向冲刺
            // 2. 若玩家正在瞄准，冲刺沿瞄准指向冲刺
            // 3. 若摇杆归中静止，则沿角色当前面朝朝向冲刺
            if (input.MoveVector.LengthSquared() > 0.04f && !input.IsFireDown)
            {
                subWeaponDir = MathHelper2D.SafeNormalize(input.MoveVector);
            }
            else if (input.AimVector.LengthSquared() > 0.04f)
            {
                subWeaponDir = MathHelper2D.SafeNormalize(input.AimVector);
            }
            else if (_currentAim.LengthSquared() > 0.04f)
            {
                subWeaponDir = MathHelper2D.SafeNormalize(_currentAim);
            }
            else
            {
                float facingX = _currentAim.X >= 0f ? 1f : -1f;
                subWeaponDir = new Vector2(facingX, 0f);
            }
        }

        if (input.IsSecondaryDown && SubWeapon.TryActivate(this, subWeaponDir))
        {
            OnSubWeaponActivated?.Invoke(this, SubWeapon);
            // 瞬移折跃机制 (Teleport)：在释放冲刺/副武器时，沿瞄准准星发射射线检测墙体距离，并在障碍物前安全贴边停步
            if (HasTeleport && world != null)
            {
                const float maxTeleportDist = 280f;
                Vector2 aimDir = subWeaponDir.LengthSquared() > 0.01f ? MathHelper2D.SafeNormalize(subWeaponDir) : Vector2.UnitX;
                var hit = world.Raycast(Body.Position, aimDir, maxTeleportDist, Collider);
                float actualDist = maxTeleportDist;
                if (hit.HasHit)
                {
                    actualDist = MathF.Max(0f, hit.Distance - Radius - 8f);
                }

                Vector2 oldPos = Body.Position;
                Body.Position += aimDir * actualDist;
                Body.Velocity = Vector2.Zero;

                particles.EmitHitSparks(oldPos, -aimDir, ThemeColor, 12);
                particles.EmitHitSparks(Body.Position, aimDir, Color.Cyan, 18);
                shake.AddTrauma(0.25f);
                OnTeleported?.Invoke(this);
            }

            foreach (var card in AppliedCards)
            {
                card.OnSecondaryUsed(this, SubWeapon);
            }
        }
    }

    /// <summary>
    /// 执行一次射击：消耗弹匣子弹、激发脚底静摩擦与躯干后坐位移、自动回正并派生子弹实体。
    /// 包含枪管穿模防护：若枪口伸进平台/墙体内部，自动将出膛点紧贴收束在近表面，杜绝子弹隔板穿墙生成。
    /// </summary>
    private void Shoot(CollisionWorld? world, ParticleSystem particles, ScreenShake shake, Action<Projectile> onSpawnProjectile)
    {
        CurrentFireCooldown = FireRateCooldown;
        bool isLastBullet = (CurrentAmmo == 1);
        CurrentAmmo--;
        if (CurrentAmmo <= 0)
        {
            ReloadTimer = ReloadDuration;
            RadianceTimer = 0f;
        }

        Vector2 recoilDir = -_currentAim;

        // 后坐力与子弹伤害关联：以 25 点基准伤害为标准后坐力（100f），伤害越低单发后坐力越低
        // 彻底解决高射速/加特林类低伤害武器因密集发射全额后坐力导致反推上天的物理问题
        float damageRecoilScale = MathF.Max(0.05f, BulletDamage / 25f);
        float effectiveRecoil = RecoilImpulse * damageRecoilScale;

        // 身体与脚的物理分离：脚底静摩擦固定，躯干后退并在射击完成后自动平滑回正
        LimbAnimator.ApplyShootingRecoil(recoilDir, 160f * MathF.Min(1.5f, damageRecoilScale));

        if (Body.IsGrounded)
        {
            // 着地时脚掌静摩擦力吸收大部分冲量，避免整个人生硬后滑
            Body.ApplyImpulse(recoilDir * (effectiveRecoil * 0.40f));
        }
        else
        {
            Body.ApplyImpulse(recoilDir * effectiveRecoil);
        }

        Squash.ApplySquash(recoilDir, 0.20f * MathF.Min(1.5f, damageRecoilScale));
        shake.AddTrauma(0.14f * MathF.Min(1.5f, damageRecoilScale));

        float baseAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        float step = BulletsPerShot > 1 ? BulletSpread / (BulletsPerShot - 1) : 0f;
        float startAngle = BulletsPerShot > 1 ? baseAngle - BulletSpread * 0.5f : baseAngle;

        for (int i = 0; i < BulletsPerShot; i++)
        {
            float angle = startAngle + i * step + (float)(Random.Shared.NextDouble() - 0.5) * 0.04f;
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));

            float desiredMuzzleDist = Radius + 16f;
            Vector2 spawnPos = Body.Position + dir * desiredMuzzleDist;

            // 枪管穿模防护：从角色中心沿射击方向检查是否有平台/墙体阻隔，杜绝枪管伸进方块后子弹直接从方块背面生成穿墙
            if (world != null)
            {
                var muzzleHit = world.CircleCast(Body.Position, 4.0f, dir, desiredMuzzleDist, Collider);
                if (muzzleHit.HasHit)
                {
                    // 枪口伸进了障碍物内部：将出膛点紧贴收束在迎弹表面外侧空气中，保留安全裕量杜绝出生时嵌入方块
                    Vector2 safeDir = muzzleHit.Normal != Vector2.Zero ? muzzleHit.Normal : -dir;
                    spawnPos = muzzleHit.Point + safeDir * 7.0f;
                }
            }

            particles.EmitHitSparks(spawnPos, dir, ThemeColor, 7);

            Vector2 bulletVel = dir * BulletSpeed;

            var proj = new Projectile(this, spawnPos, bulletVel, BulletDamage, ThemeColor)
            {
                RemainingBounces = BulletBounces,
                ExplosionRadius = ExplosionRadius,
                IsPoisonous = PoisonBullets,
                HasImplosion = ImplosionBullets,
                SplitOnBounceCount = SplitOnBounceCount,
                IsGrowBullet = HasGrowBullet,
                IsSneakyBullet = HasSneakyBullet,
                HasTargetBounce = HasTargetBounce,
                SpawnsToxicCloud = HasToxicCloud,
                CanScavengeAmmo = true
            };

            if (BulletSizeMultiplier != 1.0f)
            {
                proj.Radius = proj.BaseRadius * BulletSizeMultiplier;
                proj.BaseRadius = proj.Radius;
                proj.Collider.Radius = proj.Radius;
            }

            foreach (var card in AppliedCards)
            {
                card.OnProjectileSpawn(this, proj);
            }

            onSpawnProjectile(proj);
        }

        // 终极之盾 (Shields Up)：射出弹匣最后一发时自动激发力场护盾/格挡防御并触发金属“叮~”护盾音效
        if (HasShieldsUp && CurrentAmmo == 0)
        {
            InvulnerabilityTimer = MathF.Max(InvulnerabilityTimer, 0.45f);
            particles.EmitHitSparks(Body.Position, Vector2.UnitY, ColorPalette.PlayerBlue, 15);

            if (SubWeapon is BlockSubWeapon block)
            {
                block.ForceActivate(this);
            }
            else
            {
                _passiveShield.ForceActivate(this);
            }

            OnShieldTriggered?.Invoke(this);
        }
    }

    /// <summary>
    /// 承受伤害并吸收击退冲量。
    /// </summary>
    public void TakeDamage(float amount, Actor? attacker, Vector2 hitForce, ParticleSystem particles, ScreenShake shake)
    {
        if (IsInvulnerable || InvulnerabilityTimer > 0f || IsDead) return;

        // 凤凰涅槃 (Phoenix)：受到致死伤害时满血满弹复活 1 次，伴随浮空冲击与 1.2 秒无敌金身
        if (CurrentHealth - amount <= 0f && HasPhoenixRevive && !HasUsedPhoenix)
        {
            HasUsedPhoenix = true;
            CurrentHealth = MaxHealth;
            CurrentAmmo = MaxAmmo;
            ReloadTimer = 0f;
            InvulnerabilityTimer = 1.2f;
            HitFlashTimer = 0.5f;

            Body.Velocity = new Vector2(Body.Velocity.X * 0.25f, -340f);
            Squash.ApplyStretch(Vector2.UnitY, 0.45f);
            particles.EmitExplosion(Body.Position, 95f, Color.Gold, 28);
            particles.EmitHitSparks(Body.Position, -Vector2.UnitY, ColorPalette.ExplosiveOrange, 20);
            shake.AddTrauma(0.55f);
            OnPhoenixRevived?.Invoke(this);
            return;
        }

        CurrentHealth = MathF.Max(0f, CurrentHealth - amount);
        HitFlashTimer = 0.08f;

        // 生命虹吸 / 吸血机制：造成的所有伤害（直接命中、剧毒、爆炸、毒雾等）按吸血比例转化为生命恢复
        if (attacker != null && !attacker.IsDead && attacker != this && amount > 0f)
        {
            float lifestealPercent = attacker.LifestealRatio + (attacker.HasVampiricLeech ? 0.30f : 0f);
            if (lifestealPercent > 0f)
            {
                float heal = amount * lifestealPercent;
                attacker.CurrentHealth = MathF.Min(attacker.MaxHealth, attacker.CurrentHealth + heal);
                particles.EmitHitSparks(attacker.Body.Position, -Vector2.UnitY, ColorPalette.PlayerGreen, 2);
            }
        }

        if (hitForce.LengthSquared() > 0.01f)
        {
            Vector2 hitDir = MathHelper2D.SafeNormalize(hitForce);
            Body.ApplyImpulse(hitForce);
            Squash.ApplySquash(hitDir, 0.34f);

            float hitSign = hitDir.X >= 0f ? 1f : -1f;
            LimbAnimator.OnHitImpact(hitSign);
        }

        particles.EmitHitSparks(Body.Position, -MathHelper2D.SafeNormalize(hitForce), ThemeColor, 12);

        // 受伤震屏优化：低伤害极轻微或不晃动，高伤害适度沉稳
        if (amount > 8f)
        {
            float trauma = MathHelper.Clamp((amount - 8f) / 80f * 0.16f, 0.02f, 0.18f);
            shake.AddTrauma(trauma);
        }
    }

    /// <summary>
    /// 重置角色回合状态与位置。
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
        PoisonDps = 0f;
        PoisonSource = null;
        SlowTimer = 0f;
        SlowFactor = 1.0f;
        SpeedBoostTimer = 0f;
        SpeedBoostFactor = 1.0f;
        HitFlashTimer = 0f;
        HasUsedPhoenix = false;
        InvulnerabilityTimer = 0f;
        RadianceTimer = 0f;
        _frostCrystals.Clear();
        _groundCoyoteTimer = 0f;
        _wallCoyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _prevJumpDown = false;
        _wasGroundedLastFrame = true;
        _prevFallVelocity = 0f;
        _lastWallNormal = Vector2.Zero;
        IsWallSliding = false;
        RemainingJumps = MaxJumps;
        LimbAnimator.Reset();
        RecalculateHealthScaling();
        if (SubWeapon != null) SubWeapon.CurrentCooldown = 0f;
        _passiveShield.CurrentCooldown = 0f;
    }

    /// <summary>
    /// 重置为初始白板数值并按顺序重新注入指定卡牌列表，用于调试抽屉自由动态增删卡牌。
    /// </summary>
    public void RebuildFromCards(IEnumerable<ICardPlugin> cards)
    {
        MaxHealth = 100f;
        CurrentHealth = 100f;
        MoveForce = BaseMoveForce;
        JumpImpulse = BaseJumpImpulse;
        MaxJumps = 1;
        RemainingJumps = 1;
        MaxAmmo = 3;
        CurrentAmmo = 3;
        ReloadDuration = 1.5f;
        FireRateCooldown = 0.35f;
        BulletDamage = 25f;
        BulletSpeed = 1200f;
        BulletSizeMultiplier = 1.0f;
        RecoilImpulse = 100f;
        BulletSpread = 0f;
        BulletsPerShot = 1;
        BulletBounces = 0;
        ExplosionRadius = 0f;
        PoisonBullets = false;
        ImplosionBullets = false;
        SplitOnBounceCount = 0;

        HasPhoenixRevive = false;
        HasUsedPhoenix = false;
        InvulnerabilityTimer = 0f;
        HasRadiance = false;
        RadianceTimer = 0f;
        _frostCrystals.Clear();
        HasShieldsUp = false;
        HasTeleport = false;
        HasTargetBounce = false;
        HasGrowBullet = false;
        HasSneakyBullet = false;
        HasToxicCloud = false;
        HasScavenger = false;
        HasVampiricLeech = false;
        LifestealRatio = 0f;
        SpeedBoostTimer = 0f;
        SpeedBoostFactor = 1.0f;

        if (SubWeapon is BlockSubWeapon block)
        {
            block.Cooldown = 2.2f;
        }
        else if (SubWeapon is DashSubWeapon dash)
        {
            dash.Cooldown = 1.6f;
            dash.DashDistance = 200f;
        }

        AppliedCards.Clear();

        foreach (var card in cards)
        {
            AppliedCards.Add(card);
            card.OnApply(this);
        }

        RecalculateHealthScaling();
        CurrentHealth = MaxHealth;
        CurrentAmmo = MaxAmmo;
    }

    /// <summary>
    /// 渲染角色（委托给 <see cref="ActorRenderer"/> 执行绘制）。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        ActorRenderer.Draw(this, batcher, spriteBatch);
    }
}
