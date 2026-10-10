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
/// 纯程序化生成的 Rounds 核心角色实体。
/// 包含矢量力驱动物理刚体、阻尼形变骨骼、手脚联动动画、副武器插槽以及卡牌修饰符管线。
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
    public const float BaseMoveForce = 1850f;
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
    public float BulletSpeed { get; set; } = 880f;
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

    // 动画内部状态
    private float _walkCyclePhase = 0f;
    private float _idlePhase = 0f;
    private float _handRecoilSpring = 0f;
    private float _handRecoilVelocity = 0f;
    private Vector2 _currentAim = Vector2.UnitX;

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
    /// 帧更新角色逻辑、物理动画与冷却机制。
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

        // 3. 施加水平移动矢量力 (F = ma) 与步态果冻形变律动
        bool isMovingHorizontally = MathF.Abs(input.MoveVector.X) > 0.05f;
        if (isMovingHorizontally)
        {
            if (world != null)
            {
                Body.AddForce(new Vector2(input.MoveVector.X * MoveForce, 0f));
            }
            float moveSpeed = world != null ? MathF.Abs(Body.Velocity.X) : 260f;
            _walkCyclePhase += dt * MathF.Max(moveSpeed, 140f) * 0.048f;
        }
        else
        {
            _idlePhase += dt * 4.2f;
        }

        // 计算行走/呼吸/空中的连续Q弹果冻波动
        if (Body.IsGrounded || world == null)
        {
            float speedFactor = world != null
                ? MathHelper.Clamp(MathF.Abs(Body.Velocity.X) / 320f, 0f, 1f)
                : (isMovingHorizontally ? 0.65f : 0f);

            if (speedFactor > 0.08f)
            {
                // 走路时每一步都带有明显的Q弹上下挤压与回弹
                float walkSquash = MathF.Sin(_walkCyclePhase * 2f) * 0.17f * speedFactor;
                Squash.SetContinuousWobble(walkSquash);
            }
            else
            {
                // 待机呼吸Q弹律动
                Squash.SetContinuousWobble(MathF.Sin(_idlePhase) * 0.045f);
            }
        }
        else
        {
            // 空中高速下坠或上升时自然纵向拉长
            float airStretch = -MathHelper.Clamp(MathF.Abs(Body.Velocity.Y) / 1800f, 0f, 0.24f);
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

        // 6. 跳跃与贴墙跳逻辑 (Ground Jump & Wall Jump)
        if (_jumpBufferTimer > 0f)
        {
            if (_groundCoyoteTimer > 0f)
            {
                // 地面起跳: 沿垂直轴向强力Q弹拉伸
                Body.Velocity = new Vector2(Body.Velocity.X, -JumpImpulse);
                Body.IsGrounded = false;
                _groundCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Squash.ApplyStretch(Vector2.UnitY, 0.52f);
                particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 7);
            }
            else if (_wallCoyoteTimer > 0f && _lastWallNormal != Vector2.Zero)
            {
                // 贴墙跳 (Wall Jump): 蹬墙向反方向反弹并赋予充沛纵向升力
                float wallKickX = _lastWallNormal.X * 720f;
                float wallJumpY = -JumpImpulse * 0.94f;

                Body.Velocity = new Vector2(wallKickX, wallJumpY);
                Body.IsTouchingWall = false;
                _wallCoyoteTimer = 0f;
                _jumpBufferTimer = 0f;

                Vector2 jumpDir = MathHelper2D.SafeNormalize(new Vector2(wallKickX, wallJumpY));
                Squash.ApplyStretch(jumpDir, 0.55f);

                Vector2 wallContactPos = Body.Position - _lastWallNormal * Radius;
                particles.EmitHitSparks(wallContactPos, _lastWallNormal, ThemeColor, 10);
            }
        }
        else if (input.IsJumpDown && Body.IsGrounded)
        {
            // 长按跳跃持续连跳判定
            Body.Velocity = new Vector2(Body.Velocity.X, -JumpImpulse);
            Body.IsGrounded = false;
            _groundCoyoteTimer = 0f;
            Squash.ApplyStretch(Vector2.UnitY, 0.52f);
            particles.EmitJumpDust(Body.Position + new Vector2(0, Radius), ThemeColor, 6);
        }

        // 7. 检测落地撞击压扁形变与扬尘
        if (Body.IsGrounded && !_wasGroundedLastFrame)
        {
            if (_prevFallVelocity > 160f)
            {
                float landIntensity = MathHelper.Clamp(_prevFallVelocity / 1350f, 0.25f, 0.56f);
                Squash.ApplySquash(Vector2.UnitY, landIntensity);
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
                _handRecoilVelocity -= 12f;
                Squash.ApplySquash(Vector2.UnitY, 0.24f);
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

        // 10. 手部后坐力弹簧与形变弹簧阻尼更新
        _handRecoilSpring = MathHelper2D.SpringDamp(_handRecoilSpring, 0f, ref _handRecoilVelocity, 240f, 14f, dt);
        Squash.Update(dt);
    }

    /// <summary>
    /// 执行一次射击，消耗弹匣子弹并施加矢量后坐力。
    /// </summary>
    private void Shoot(ParticleSystem particles, ScreenShake shake, Action<Projectile> onSpawnProjectile)
    {
        CurrentFireCooldown = FireRateCooldown;
        CurrentAmmo--;
        if (CurrentAmmo <= 0)
        {
            ReloadTimer = ReloadDuration;
        }

        // 产生枪械后坐力反向冲量
        Vector2 recoilDir = -_currentAim;
        Body.ApplyImpulse(recoilDir * RecoilImpulse);

        // 触发受力挤压形变 (沿弹道轴向Q弹压扁)
        Squash.ApplySquash(recoilDir, 0.42f);

        // 手部反冲弹簧回缩
        _handRecoilVelocity += 18f;

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
    /// 受到外部打击承受伤害并产生打击冲量。
    /// </summary>
    public void TakeDamage(float amount, Actor? attacker, Vector2 hitForce, ParticleSystem particles, ScreenShake shake)
    {
        if (IsInvulnerable || IsDead) return;

        CurrentHealth = MathF.Max(0f, CurrentHealth - amount);
        HitFlashTimer = 0.08f;

        // 吸收冲击动量与强力Q弹压扁形变
        if (hitForce.LengthSquared() > 0.01f)
        {
            Body.ApplyImpulse(hitForce);
            Squash.ApplySquash(MathHelper2D.SafeNormalize(hitForce), 0.54f);
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
        RecalculateHealthScaling();
        if (SubWeapon != null) SubWeapon.CurrentCooldown = 0f;
    }

    /// <summary>
    /// 纯程序化渲染角色Q弹果冻身体、灵动横向双眼、步态小脚、双手枪械与头顶弹匣指示器。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (IsDead) return;

        Vector2 pos = Body.Position;
        Color renderColor = HitFlashTimer > 0f ? ColorPalette.WhiteFlash : ThemeColor;
        Color darkAccent = Color.Lerp(renderColor, Color.Black, 0.42f);
        Color outlineColor = new(18, 22, 32, 220);
        float sizeRatio = Radius / BaseRadius;

        // 计算步行时的身体颠簸高度偏移，强化Q弹步态感
        float speedFactor = MathHelper.Clamp(MathF.Abs(Body.Velocity.X) / 300f, 0f, 1f);
        float walkHopY = Body.IsGrounded ? -MathF.Abs(MathF.Sin(_walkCyclePhase)) * 5.0f * sizeRatio * speedFactor : 0f;
        Vector2 bodyPos = pos + new Vector2(0f, walkHopY);

        // 1. 程序化步态小脚 (椭圆形小鞋，行走时呈真实迈步弧线，腾空时随气流后摆)
        float baseFootX = 9.5f * sizeRatio;
        float baseFootY = Radius * 0.88f;
        Vector2 foot1Pos, foot2Pos;
        float footAngle1 = 0f, footAngle2 = 0f;

        if (Body.IsGrounded || MathF.Abs(Body.Velocity.Y) < 10f)
        {
            float moveDir = MathF.Abs(Body.Velocity.X) > 15f ? MathF.Sign(Body.Velocity.X) : MathF.Sign(_currentAim.X);
            float strideX1 = MathF.Cos(_walkCyclePhase) * 9.5f * sizeRatio * speedFactor * moveDir;
            float liftY1 = -MathF.Max(0f, MathF.Sin(_walkCyclePhase)) * 8.5f * sizeRatio * speedFactor;

            float strideX2 = MathF.Cos(_walkCyclePhase + MathHelper.Pi) * 9.5f * sizeRatio * speedFactor * moveDir;
            float liftY2 = -MathF.Max(0f, MathF.Sin(_walkCyclePhase + MathHelper.Pi)) * 8.5f * sizeRatio * speedFactor;

            foot1Pos = pos + new Vector2(-baseFootX + strideX1, baseFootY + liftY1);
            foot2Pos = pos + new Vector2(baseFootX + strideX2, baseFootY + liftY2);
            footAngle1 = liftY1 * 0.04f * moveDir;
            footAngle2 = liftY2 * 0.04f * moveDir;
        }
        else
        {
            // 腾空状态：双脚随速度反方向自然微收下垂
            Vector2 airTrail = MathHelper2D.ClampLength(-Body.Velocity * 0.012f, 10f * sizeRatio);
            foot1Pos = bodyPos + new Vector2(-baseFootX * 0.8f, baseFootY * 0.95f) + airTrail;
            foot2Pos = bodyPos + new Vector2(baseFootX * 0.8f, baseFootY * 0.95f) + airTrail;
            footAngle1 = 0.25f * MathF.Sign(Body.Velocity.X);
            footAngle2 = -0.15f * MathF.Sign(Body.Velocity.X);
        }

        Vector2 shoeScale = new(1.35f, 0.82f);
        float footR = 6.2f * sizeRatio;
        // 后脚
        batcher.DrawDeformedCircle(spriteBatch, foot1Pos, footR, shoeScale, footAngle1, darkAccent);
        batcher.DrawDeformedCircleOutline(spriteBatch, foot1Pos, footR, shoeScale, footAngle1, outlineColor, 2f);
        // 前脚
        batcher.DrawDeformedCircle(spriteBatch, foot2Pos, footR, shoeScale, footAngle2, darkAccent);
        batcher.DrawDeformedCircleOutline(spriteBatch, foot2Pos, footR, shoeScale, footAngle2, outlineColor, 2f);

        // 2. 副手 (背侧平衡小圆手，随步伐摆动)
        float aimSign = _currentAim.X >= 0f ? 1f : -1f;
        float offHandBob = MathF.Sin(_walkCyclePhase) * 4f * sizeRatio * speedFactor;
        Vector2 offHandPos = bodyPos + new Vector2(-aimSign * Radius * 0.92f, Radius * 0.12f + offHandBob);
        float offHandR = 5.5f * sizeRatio;
        batcher.DrawFilledCircle(spriteBatch, offHandPos, offHandR, darkAccent);
        batcher.DrawCircleOutline(spriteBatch, offHandPos, offHandR, outlineColor, 2f);

        // 3. 角色Q弹果冻身体 (带物理 Squash & Stretch 弹簧形变、果冻高光与同步描边)
        batcher.DrawDeformedCircle(
            spriteBatch,
            bodyPos,
            Radius,
            Squash.CurrentScale,
            Squash.DeformationAngle,
            renderColor
        );

        // 果冻内部顶部半透明柔和立体高光
        Vector2 highlightPos = bodyPos + new Vector2(-Radius * 0.22f, -Radius * 0.28f);
        batcher.DrawDeformedCircle(
            spriteBatch,
            highlightPos,
            Radius * 0.42f,
            Squash.CurrentScale * new Vector2(0.95f, 0.65f),
            Squash.DeformationAngle - 0.3f,
            Color.White * 0.24f
        );

        // 身体外轮廓与形变完全同步
        batcher.DrawDeformedCircleOutline(
            spriteBatch,
            bodyPos,
            Radius,
            Squash.CurrentScale,
            Squash.DeformationAngle,
            outlineColor,
            3f
        );

        // 4. 角色灵动横向双眼 (始终保持水平左右排列，彻底告别比目鱼垂直叠眼)
        Vector2 lookShift = new(_currentAim.X * Radius * 0.30f, _currentAim.Y * Radius * 0.18f - Radius * 0.08f);
        Vector2 faceCenter = bodyPos + lookShift;
        float eyeSpacingX = 7.2f * sizeRatio;
        Vector2 leftEyePos = faceCenter + new Vector2(-eyeSpacingX, 0f);
        Vector2 rightEyePos = faceCenter + new Vector2(eyeSpacingX, 0f);

        if (HitFlashTimer > 0f)
        {
            // 受击时眩晕 X 眼
            float cross = 4.2f * sizeRatio;
            batcher.DrawLine(spriteBatch, leftEyePos - new Vector2(cross), leftEyePos + new Vector2(cross), outlineColor, 2.5f);
            batcher.DrawLine(spriteBatch, leftEyePos + new Vector2(-cross, cross), leftEyePos + new Vector2(cross, -cross), outlineColor, 2.5f);
            batcher.DrawLine(spriteBatch, rightEyePos - new Vector2(cross), rightEyePos + new Vector2(cross), outlineColor, 2.5f);
            batcher.DrawLine(spriteBatch, rightEyePos + new Vector2(-cross, cross), rightEyePos + new Vector2(cross, -cross), outlineColor, 2.5f);
        }
        else
        {
            // 近大远小微透视：朝向侧的眼睛略大，营造立体感
            float leftScale = _currentAim.X < 0f ? 1.08f : 0.92f;
            float rightScale = _currentAim.X >= 0f ? 1.08f : 0.92f;
            float baseEyeR = 5.0f * sizeRatio;

            DrawExpressiveEye(batcher, spriteBatch, leftEyePos, baseEyeR * leftScale, _currentAim, outlineColor, isLeft: true);
            DrawExpressiveEye(batcher, spriteBatch, rightEyePos, baseEyeR * rightScale, _currentAim, outlineColor, isLeft: false);
        }

        // 5. 程序化主手与枪械武器 (射击后坐回弹 + 换弹转枪动画)
        float handDist = Radius * 0.85f + 8f * sizeRatio - _handRecoilSpring * 0.65f;
        Vector2 handPos = bodyPos + _currentAim * handDist + new Vector2(0f, 3f * sizeRatio);

        // 换弹时枪身翻转旋转一周
        float gunAngle = MathF.Atan2(_currentAim.Y, _currentAim.X);
        if (IsReloading)
        {
            float reloadProgress = 1f - (ReloadTimer / ReloadDuration);
            gunAngle += reloadProgress * MathHelper.TwoPi * aimSign;
        }
        Vector2 gunDir = new(MathF.Cos(gunAngle), MathF.Sin(gunAngle));
        Vector2 gunPerp = new(-gunDir.Y, gunDir.X);
        if (aimSign < 0f) gunPerp = -gunPerp;

        // 枪柄 (Grip)
        batcher.DrawLine(spriteBatch, handPos, handPos - gunPerp * (9f * sizeRatio) - gunDir * (3f * sizeRatio), new Color(32, 36, 46), 5.5f * sizeRatio);
        // 枪管外框与护木 (Receiver & Barrel)
        Vector2 gunStart = handPos - gunDir * (5f * sizeRatio);
        Vector2 gunEnd = handPos + gunDir * (20f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd, outlineColor, 9.5f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd - gunDir * (2f * sizeRatio), new Color(55, 62, 78), 6.0f * sizeRatio);
        // 枪身主题色能量槽 (有弹药时高亮发光)
        Color energyColor = IsReloading ? Color.Gray * 0.5f : ThemeColor;
        batcher.DrawLine(spriteBatch, handPos, handPos + gunDir * (11f * sizeRatio), energyColor, 2.8f * sizeRatio);

        // 握枪主手
        float mainHandR = 6.2f * sizeRatio;
        batcher.DrawFilledCircle(spriteBatch, handPos, mainHandR, renderColor);
        batcher.DrawCircleOutline(spriteBatch, handPos, mainHandR, outlineColor, 2f);

        // 6. 头顶弹匣余量与换弹进度条
        DrawAmmoAndReloadIndicator(batcher, spriteBatch, pos, sizeRatio);

        // 7. 渲染副武器特效 (如格挡力场护盾)
        SubWeapon?.Draw(batcher, spriteBatch, this);
    }

    private void DrawExpressiveEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft)
    {
        // 眼眶描边与眼白
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.4f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, Color.White);

        // 聚焦瞳孔
        Vector2 pupilPos = eyePos + aim * (eyeR * 0.36f);
        float pupilR = eyeR * 0.60f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(18, 20, 30));

        // 灵动眼神高光点
        Vector2 highlightPos = pupilPos + new Vector2(-pupilR * 0.35f, -pupilR * 0.35f);
        batcher.DrawFilledCircle(spriteBatch, highlightPos, pupilR * 0.36f, Color.White);

        // 战术眉毛 (微微内倾展现斗志)
        float browTilt = isLeft ? 1.8f : -1.8f;
        Vector2 browLeft = eyePos + new Vector2(-eyeR * 0.85f, -eyeR * 1.15f - browTilt);
        Vector2 browRight = eyePos + new Vector2(eyeR * 0.85f, -eyeR * 1.15f + browTilt);
        batcher.DrawLine(spriteBatch, browLeft, browRight, outlineColor, 2.2f);
    }

    private void DrawAmmoAndReloadIndicator(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 pos, float sizeRatio)
    {
        Vector2 indicatorCenter = pos + new Vector2(0f, -Radius * 1.35f - 12f);

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
