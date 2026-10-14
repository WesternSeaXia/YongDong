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
    public const float BaseMoveForce = 2220f;
    public const float BaseJumpImpulse = 1100f;

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

    // --- 主武器与弹匣换弹属性 ---
    public int MaxAmmo { get; set; } = 3;
    public int CurrentAmmo { get; set; } = 3;
    public float ReloadDuration { get; set; } = 1.45f;
    public float ReloadTimer { get; set; } = 0f;
    public bool IsReloading => ReloadTimer > 0f;

    public float FireRateCooldown { get; set; } = 0.35f;
    public float CurrentFireCooldown { get; set; } = 0f;
    public float BulletDamage { get; set; } = 28f;
    public float BulletSpeed { get; set; } = 1200f;

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
    public bool IsWallSliding { get; private set; } = false;

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
            Restitution = 0.05f
        };

        Collider = new CircleCollider(Body, Radius);
        RecalculateHealthScaling();
    }

    /// <summary>
    /// 根据当前最大生命值动态重新计算角色体型、物理质量、移动推力与跳跃冲量。
    /// </summary>
    public void RecalculateHealthScaling()
    {
        float ratio = MathF.Max(0.5f, MaxHealth / 100f);

        Radius = BaseRadius * MathF.Pow(ratio, 0.40f);
        Collider.Radius = Radius;
        Body.Mass = BaseMass * ratio;
        MoveForce = BaseMoveForce * MathF.Pow(1.0f / ratio, 0.35f);
        JumpImpulse = BaseJumpImpulse * MathF.Pow(1.0f / ratio, 0.25f);
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

        // 1. 更新受击闪烁与持续毒素状态
        UpdateStatusEffects(dt, particles, shake);

        // 2. 更新瞄准方向与水平移动推力
        if (input.AimVector.LengthSquared() > 0.001f)
        {
            _currentAim = MathHelper2D.SafeNormalize(input.AimVector);
        }

        if (world != null && MathF.Abs(input.MoveVector.X) > 0.05f)
        {
            Body.AddForce(new Vector2(input.MoveVector.X * MoveForce, 0f));
        }

        // 3. 更新跳跃缓冲、土狼时间、贴墙滑行与坠落缓冲/撞击伤害
        UpdateJumpAndWallSlide(dt, input, particles, shake);

        // 4. 更新主武器射击与弹匣换弹
        UpdateWeaponAndReload(dt, input, particles, shake, onSpawnProjectile);

        // 5. 更新副武器
        UpdateSubWeapon(dt, input);

        // 6. 更新果冻形变与竹节四肢角弹簧动画器
        Squash.Update(dt);
        LimbAnimator.Update(this, input, world, dt);
    }

    private void UpdateStatusEffects(float dt, ParticleSystem particles, ScreenShake shake)
    {
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
    }

    private void UpdateJumpAndWallSlide(float dt, ControlInputState input, ParticleSystem particles, ScreenShake shake)
    {
        bool jumpJustPressed = input.IsJumpDown && !_prevJumpDown;
        _prevJumpDown = input.IsJumpDown;

        if (jumpJustPressed) _jumpBufferTimer = 0.14f;
        else if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;

        if (Body.IsGrounded) _groundCoyoteTimer = 0.10f;
        else if (_groundCoyoteTimer > 0f) _groundCoyoteTimer -= dt;

        if (Body.IsTouchingWall && !Body.IsGrounded)
        {
            _wallCoyoteTimer = 0.16f;
            _lastWallNormal = Body.WallNormal;
        }
        else if (_wallCoyoteTimer > 0f)
        {
            _wallCoyoteTimer -= dt;
        }

        // 贴墙缓缓下滑与摩擦火花
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

        // 执行地面跳跃或贴墙蹬跳
        if (_jumpBufferTimer > 0f)
        {
            if (_groundCoyoteTimer > 0f)
            {
                Body.Velocity = new Vector2(Body.Velocity.X, -JumpImpulse);
                Body.IsGrounded = false;
                _groundCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Squash.ApplyStretch(Vector2.UnitY, 0.30f);
                LimbAnimator.OnGroundJump(aimSign, 1.0f);
                particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 7);
            }
            else if (_wallCoyoteTimer > 0f && _lastWallNormal != Vector2.Zero)
            {
                float wallKickX = _lastWallNormal.X * 300f;
                float wallJumpY = -JumpImpulse * 0.94f;

                Body.Velocity = new Vector2(wallKickX, wallJumpY);
                Body.IsTouchingWall = false;
                _wallCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Vector2 jumpDir = MathHelper2D.SafeNormalize(new Vector2(wallKickX, wallJumpY));
                Squash.ApplyStretch(jumpDir, 0.32f);
                LimbAnimator.OnWallJump(_lastWallNormal.X, aimSign);

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
            LimbAnimator.OnGroundJump(aimSign, 0.94f);
            particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 6);
        }

        // 落地冲击判定：区分高处极速坠落压弯受到撞击伤害 vs 普通落地缓冲
        if (Body.IsGrounded && !_wasGroundedLastFrame && _prevFallVelocity > 150f)
        {
            if (_prevFallVelocity > 1600f && !Body.IsOnBouncySurface)
            {
                // 仅在超高空极速坠落或向下强力下砸时，且非高弹蹦床时，才触发身体重压受挫伤害与深蹲压弯
                float excessSpeed = _prevFallVelocity - 1600f;
                float fallDamage = MathF.Min(excessSpeed * 0.016f, 25f);
                TakeDamage(fallDamage, null, Vector2.UnitY * 20f, particles, shake);

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
        _wasGroundedLastFrame = Body.IsGrounded;
        _prevFallVelocity = Body.Velocity.Y;
    }

    private void UpdateWeaponAndReload(
        float dt,
        ControlInputState input,
        ParticleSystem particles,
        ScreenShake shake,
        Action<Projectile> onSpawnProjectile)
    {
        if (CurrentFireCooldown > 0f) CurrentFireCooldown -= dt;

        if (IsReloading)
        {
            ReloadTimer -= dt;
            if (ReloadTimer <= 0f)
            {
                ReloadTimer = 0f;
                CurrentAmmo = MaxAmmo;
                float aimSign = _currentAim.X >= 0f ? 1f : -1f;
                LimbAnimator.OnReloadComplete(aimSign);
                Squash.ApplySquash(Vector2.UnitY, 0.15f);
                particles.EmitHitSparks(Body.Position + _currentAim * (Radius + 14f), _currentAim, Color.White, 5);
            }
        }
        else if (input.IsFireDown && CurrentFireCooldown <= 0f && CurrentAmmo > 0)
        {
            Shoot(particles, shake, onSpawnProjectile);
        }
    }

    private void UpdateSubWeapon(float dt, ControlInputState input)
    {
        if (SubWeapon == null) return;

        SubWeapon.Update(dt);
        if (input.IsSecondaryDown && SubWeapon.TryActivate(this, _currentAim))
        {
            foreach (var card in AppliedCards)
            {
                card.OnSecondaryUsed(this, SubWeapon);
            }
        }
    }

    /// <summary>
    /// 执行一次射击：消耗弹匣子弹、激发脚底静摩擦与躯干后坐位移、自动回正并派生子弹实体。
    /// </summary>
    private void Shoot(ParticleSystem particles, ScreenShake shake, Action<Projectile> onSpawnProjectile)
    {
        CurrentFireCooldown = FireRateCooldown;
        CurrentAmmo--;
        if (CurrentAmmo <= 0)
        {
            ReloadTimer = ReloadDuration;
        }

        Vector2 recoilDir = -_currentAim;

        // 身体与脚的物理分离：脚底静摩擦固定，躯干后退并在射击完成后自动平滑回正
        LimbAnimator.ApplyShootingRecoil(recoilDir, 160f);

        if (Body.IsGrounded)
        {
            // 着地时脚掌静摩擦力吸收大部分冲量，避免整个人生硬后滑
            Body.ApplyImpulse(recoilDir * (RecoilImpulse * 0.40f));
        }
        else
        {
            Body.ApplyImpulse(recoilDir * RecoilImpulse);
        }

        Squash.ApplySquash(recoilDir, 0.20f);
        particles.EmitHitSparks(Body.Position + _currentAim * (Radius + 16f), _currentAim, ThemeColor, 7);
        shake.AddTrauma(0.14f);

        float baseAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        float step = BulletsPerShot > 1 ? BulletSpread / (BulletsPerShot - 1) : 0f;
        float startAngle = BulletsPerShot > 1 ? baseAngle - BulletSpread * 0.5f : baseAngle;

        for (int i = 0; i < BulletsPerShot; i++)
        {
            float angle = startAngle + i * step + (float)(Random.Shared.NextDouble() - 0.5) * 0.04f;
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));

            Vector2 spawnPos = Body.Position + dir * (Radius + 16f);
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
    /// 承受伤害并吸收击退冲量。
    /// </summary>
    public void TakeDamage(float amount, Actor? attacker, Vector2 hitForce, ParticleSystem particles, ScreenShake shake)
    {
        if (IsInvulnerable || IsDead) return;

        CurrentHealth = MathF.Max(0f, CurrentHealth - amount);
        HitFlashTimer = 0.08f;

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
        HitFlashTimer = 0f;
        _groundCoyoteTimer = 0f;
        _wallCoyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _prevJumpDown = false;
        _wasGroundedLastFrame = true;
        _prevFallVelocity = 0f;
        _lastWallNormal = Vector2.Zero;
        IsWallSliding = false;
        LimbAnimator.Reset();
        RecalculateHealthScaling();
        if (SubWeapon != null) SubWeapon.CurrentCooldown = 0f;
    }

    /// <summary>
    /// 渲染角色（委托给 <see cref="ActorRenderer"/> 执行绘制）。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        ActorRenderer.Draw(this, batcher, spriteBatch);
    }
}
