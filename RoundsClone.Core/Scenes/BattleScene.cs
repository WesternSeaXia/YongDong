using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.AI;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Maps;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 核心战斗决斗场场景。
/// 职责边界：负责协调单局战斗的物理世界步进、角色与 AI 行为更新、平滑动态摄像机运镜 (<see cref="Camera2D"/>)、
/// 子弹碰撞与反弹回源判定以及回合胜负流转；UI 绘制委托给 <see cref="BattleHudRenderer"/>。
/// </summary>
public class BattleScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;

    private readonly CollisionWorld _world = new();
    private readonly MapData _map;
    private readonly Actor _player;
    private readonly Actor _ai;
    private readonly IAiBrain _aiBrain = new TacticalCombatAi();
    private readonly Camera2D _camera = new();

    private readonly List<Projectile> _projectiles = new();
    private readonly List<Projectile> _spawnQueue = new();
    private readonly List<ToxicCloud> _toxicClouds = new();
    private readonly DebugCardDrawer _debugDrawer = new();

    // 回合生命周期状态
    private bool _roundEnded = false;
    private float _roundEndTimer = 0f;
    private bool _playerWonRound = false;
    private float _timeScale = 1.0f;

    // 平滑血条显示阻尼
    private float _playerHealthDisplay = 100f;
    private float _aiHealthDisplay = 100f;

    public BattleScene(SceneManager manager, Action exitAction)
    {
        _manager = manager;
        _exitAction = exitAction;
        _map = _manager.CurrentMap ?? MapRegistry.CreateClassicArena();

        _player = new Actor(_map.PlayerSpawn, _manager.PlayerColor, isAi: false)
        {
            Name = "玩家",
            EyeStyle = _manager.PlayerEyeStyle,
            SubWeapon = _manager.PlayerSubWeaponFactory()
        };

        _ai = new Actor(_map.AiSpawn, _manager.AiColor, isAi: true)
        {
            Name = "AI 对手",
            EyeStyle = _manager.AiEyeStyle,
            SubWeapon = _manager.AiSubWeaponFactory()
        };
    }

    /// <inheritdoc />
    public void Enter()
    {
        _world.Clear();
        _map.BuildPhysicsWorld(_world);

        _world.AddCollider(_player.Collider);
        _world.AddCollider(_ai.Collider);

        // 应用双方累计卡牌插件
        foreach (var card in _manager.PlayerCards)
        {
            _player.AppliedCards.Add(card);
            card.OnApply(_player);
        }
        foreach (var card in _manager.AiCards)
        {
            _ai.AppliedCards.Add(card);
            card.OnApply(_ai);
        }

        // 重新同步生命值体型与速度缩放
        _player.RecalculateHealthScaling();
        _ai.RecalculateHealthScaling();

        _player.ResetState(_map.PlayerSpawn);
        _ai.ResetState(_map.AiSpawn);
        _playerHealthDisplay = _player.MaxHealth;
        _aiHealthDisplay = _ai.MaxHealth;

        _camera.Reset();
        _roundEnded = false;
        _roundEndTimer = 0f;
        _timeScale = 1.0f;
        _projectiles.Clear();
        _toxicClouds.Clear();

        _player.OnRadiancePulse -= HandleRadiancePulse;
        _player.OnRadiancePulse += HandleRadiancePulse;
        _ai.OnRadiancePulse -= HandleRadiancePulse;
        _ai.OnRadiancePulse += HandleRadiancePulse;
    }

    /// <inheritdoc />
    public void Update(float rawDt)
    {
        float dt = rawDt * _timeScale;

        // 0. 更新自选卡牌调试抽屉与靶桩控制器
        _debugDrawer.Update(rawDt, _manager.Input, _player, _ai);
        if (_debugDrawer.InfiniteAmmo)
        {
            _player.CurrentAmmo = _player.MaxAmmo;
            _player.ReloadTimer = 0f;
        }

        // 1. 采集玩家输入与 AI 决策（若开启靶桩模式，AI 变为不攻击不移动的物理木桩）
        ControlInputState playerInput = _manager.Input.GetPlayerInputState(_player, _camera.ScreenToWorld);
        ControlInputState aiInput = _debugDrawer.IsDummyMode
            ? ControlInputState.Empty
            : _aiBrain.Think(_ai, _player, _projectiles, _world, _map, dt);

        // 2. 更新角色实体
        _player.Update(dt, playerInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);
        _ai.Update(dt, aiInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);

        // 3. 动态摄像机追踪平滑更新
        _camera.Update(_player, _ai, dt);

        // 3.5. 更新地图组件（运动平台先计算出本帧位移与速度）
        _map.Update(dt, _world);

        // 4. 物理世界模拟步进与碰撞判定（平台在此速度下与站立角色接触，带动角色平滑跟随位移）
        _world.Step(dt, OnPhysicsCollision);

        // 5. 注册新派生子弹
        if (_spawnQueue.Count > 0)
        {
            foreach (var p in _spawnQueue)
            {
                _projectiles.Add(p);
                _world.AddCollider(p.Collider);
            }
            _spawnQueue.Clear();
        }

        // 6. 更新与步进活跃子弹（基于连续几何扫掠 CCD，彻底杜绝高速穿墙与反弹角畸变）
        UpdateProjectilesCCD(dt);

        // 6.5. 更新毒云弥漫实体
        if (_toxicClouds.Count > 0)
        {
            var actors = new List<Actor> { _player, _ai };
            for (int i = _toxicClouds.Count - 1; i >= 0; i--)
            {
                var cloud = _toxicClouds[i];
                cloud.Update(dt, actors, _manager.Particles, _manager.Shake);
                if (cloud.IsExpired)
                {
                    _toxicClouds.RemoveAt(i);
                }
            }
        }

        // 7. 血条平滑插值
        _playerHealthDisplay = MathHelper.Lerp(_playerHealthDisplay, _player.CurrentHealth, dt * 10f);
        _aiHealthDisplay = MathHelper.Lerp(_aiHealthDisplay, _ai.CurrentHealth, dt * 10f);

        // 8. 回合终局判定与流转
        UpdateRoundLifecycle(rawDt);

        // ESC 键随时返回主菜单
        if (_manager.Input.IsKeyJustPressed(Keys.Escape))
        {
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    private void UpdateRoundLifecycle(float rawDt)
    {
        if (_debugDrawer.IsDummyMode) return;

        if (!_roundEnded)
        {
            if (_player.IsDead || _ai.IsDead)
            {
                _roundEnded = true;
                _roundEndTimer = 0f;
                _timeScale = 0.25f;
                _manager.Shake.AddTrauma(0.7f);

                _playerWonRound = !(_player.IsDead && _ai.IsDead) && !_player.IsDead;

                if (_playerWonRound) _manager.PlayerScore++;
                else _manager.AiScore++;

                _manager.LastRoundPlayerWon = _playerWonRound;
            }
        }
        else
        {
            _roundEndTimer += rawDt;
            if (_roundEndTimer >= 1.6f)
            {
                if (_manager.PlayerScore >= SceneManager.TargetWins || _manager.AiScore >= SceneManager.TargetWins)
                {
                    _manager.ChangeScene(new GameOverScene(_manager, _exitAction));
                }
                else if (!_playerWonRound)
                {
                    _manager.ChangeScene(new CardSelectScene(_manager, _exitAction, isForPlayer: true));
                }
                else
                {
                    var draft = CardRegistry.DraftCards(1);
                    if (draft.Count > 0)
                    {
                        _manager.AiCards.Add(draft[0]);
                    }
                    _manager.ChangeScene(new StageSelectScene(_manager, _exitAction));
                }
            }
        }
    }

    private void SpawnProjectile(Projectile p)
    {
        _spawnQueue.Add(p);
    }

    private void OnPhysicsCollision(CollisionManifold m)
    {
        CheckHazardCollision(m.ColliderA, m.ColliderB, m);
        CheckHazardCollision(m.ColliderB, m.ColliderA, m);
    }

    private void CheckHazardCollision(Collider actorCol, Collider otherCol, CollisionManifold m)
    {
        Actor? actor = (actorCol.Body == _player.Body) ? _player : (actorCol.Body == _ai.Body ? _ai : null);
        if (actor == null || actor.IsDead || actor.IsInvulnerable || actor.InvulnerabilityTimer > 0f) return;

        var terrain = _map.TerrainObjects.Find(t => t.Collider == otherCol);
        if (terrain != null && terrain.Style == TerrainStyle.HazardSpike)
        {
            Vector2 spikeForce = -m.Normal * 450f;
            actor.TakeDamage(18f, null, spikeForce, _manager.Particles, _manager.Shake);
            if (_debugDrawer.IsDummyMode && actor == _ai)
            {
                _ai.CurrentHealth = _ai.MaxHealth;
            }
        }
    }

    /// <summary>
    /// 统一调度活跃子弹的格挡偏转、状态更新与连续几何扫掠 (CCD) 运动解算。
    /// 彻底消除火箭助推等超高速子弹隧穿方块与倒弹/畸变反弹角问题。
    /// </summary>
    private void UpdateProjectilesCCD(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var proj = _projectiles[i];
            if (proj.IsDestroyed)
            {
                _world.RemoveCollider(proj.Collider);
                _projectiles.RemoveAt(i);
                continue;
            }

            // 1. 检查副武器能量格挡盾弹反
            CheckBlockDeflection(proj, _player);
            CheckBlockDeflection(proj, _ai);

            if (proj.IsDestroyed)
            {
                _world.RemoveCollider(proj.Collider);
                _projectiles.RemoveAt(i);
                continue;
            }

            // 2. 状态更新（体型生长、贴地滑翔、寿命超时、尾迹采样与粒子）
            proj.Update(dt, _manager.Particles, _world);
            if (proj.IsDestroyed)
            {
                _world.RemoveCollider(proj.Collider);
                _projectiles.RemoveAt(i);
                continue;
            }

            // 3. 连续几何扫掠 (CCD) 步进与碰撞解算
            SimulateProjectileMotionCCD(proj, dt);

            if (proj.IsDestroyed)
            {
                _world.RemoveCollider(proj.Collider);
                _projectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 对单枚子弹执行连续几何扫掠 (CCD) 运动推进。
    /// 严格遵循用户定制判定时序：碰撞发生时，先判定是否还有反跳次数，再触发物理效果！
    /// </summary>
    private void SimulateProjectileMotionCCD(Projectile proj, float dt)
    {
        const int MaxBouncesPerFrame = 4;
        int bouncesDone = 0;
        float remainingDt = dt;

        while (remainingDt > 0.00001f && !proj.IsDestroyed && bouncesDone < MaxBouncesPerFrame)
        {
            float speed = proj.Body.Velocity.Length();
            if (speed < 0.001f)
            {
                proj.Body.Position += proj.Body.Velocity * remainingDt;
                break;
            }

            float travelDist = speed * remainingDt;
            Vector2 travelDir = proj.Body.Velocity / speed;

            // 执行连续几何扫掠检测
            var hit = _world.CircleCast(proj.Body.Position, proj.Radius, travelDir, travelDist, proj.Collider, col =>
            {
                if (proj.Owner != null && col == proj.Owner.Collider) return false;
                return true;
            });

            if (!hit.HasHit)
            {
                // 无碰撞：安全推进完整剩余位移
                proj.Body.Position += proj.Body.Velocity * remainingDt;
                break;
            }

            // 碰撞发生：已消耗的时间比例
            float timeToHit = hit.Distance / speed;
            remainingDt = MathF.Max(0f, remainingDt - timeToHit);

            CollisionManifold m = new()
            {
                HasCollision = true,
                ColliderA = proj.Collider,
                ColliderB = hit.HitCollider,
                Normal = hit.Normal,
                ContactPoint = hit.Point,
                Penetration = 0f
            };

            // 1. 判定是否击中角色 (Actor)
            if (hit.HitCollider is CircleCollider cc && (cc.Body == _player.Body || cc.Body == _ai.Body))
            {
                Actor hitTarget = (cc.Body == _player.Body) ? _player : _ai;
                proj.Body.Position = hit.Point;

                if (hitTarget != proj.Owner && !hitTarget.IsInvulnerable && hitTarget.InvulnerabilityTimer <= 0f)
                {
                    Vector2 hitForce = travelDir * proj.KnockbackImpulse;

                    if (proj.IsPoisonous)
                    {
                        // 剧毒催化：50% 直接伤害，50% 转化为持续 4 秒的中毒持续伤害
                        float directDmg = proj.Damage * 0.50f;
                        float poisonDmg = proj.Damage * 0.50f;
                        hitTarget.TakeDamage(directDmg, proj.Owner, hitForce, _manager.Particles, _manager.Shake);
                        hitTarget.ApplyPoison(proj.Owner, poisonDmg, 4.0f);
                        _manager.Particles.EmitHitSparks(hitTarget.Body.Position, Vector2.UnitY, ColorPalette.PoisonGreen, 16);
                    }
                    else
                    {
                        hitTarget.TakeDamage(proj.Damage, proj.Owner, hitForce, _manager.Particles, _manager.Shake);
                    }

                    // 靶桩模式：记录有效伤害并锁血复原
                    if (_debugDrawer.IsDummyMode && hitTarget == _ai)
                    {
                        _debugDrawer.RecordDummyDamage(proj.Damage);
                        _ai.CurrentHealth = _ai.MaxHealth;
                    }

                    // 以战养战 (Scavenger)：直接子弹伤害命中即刻返还 1 发弹药（打出末发后进入正常换弹，该末发不加子弹）
                    if (proj.CanScavengeAmmo && proj.Owner != null && proj.Owner.HasScavenger)
                    {
                        if (proj.Owner.CurrentAmmo < proj.Owner.MaxAmmo)
                        {
                            proj.Owner.CurrentAmmo = Math.Min(proj.Owner.MaxAmmo, proj.Owner.CurrentAmmo + 1);
                            _manager.Particles.EmitHitSparks(proj.Owner.Body.Position, Vector2.UnitY, ColorPalette.PlayerGreen, 6);
                        }
                    }

                    TriggerProjectileImpact(proj, hitTarget, m);
                    proj.Destroy();
                }
                break;
            }

            // 2. 击中地形 / 墙体 / 障碍物
            var hitTerrain = _map.TerrainObjects.Find(t => t.Collider == hit.HitCollider);

            // 动态物理箱受子弹强力物理冲量驱动
            if (hitTerrain != null && !hitTerrain.IsStatic)
            {
                Vector2 hitImpulse = travelDir * 380f;
                hitTerrain.Body.ApplyImpulse(hitImpulse);
            }

            // 可破坏地形扣减生命值
            if (hitTerrain != null && hitTerrain.IsDestructible)
            {
                hitTerrain.TakeDamage(proj.Damage, _manager.Particles, _manager.Shake);
            }

            // 【贴地滑翔专属机制 (Sneaky)】：
            // 规则（方案B）：
            // 1. 坡度 < 90°：地形表面具有向上支撑法向分量 (hit.Normal.Y <= -0.05f)
            // 2. 入射角 >= 30°：入射方向与表面外法线夹角 >= 30°（与地表夹角 <= 60°，即 -Vector2.Dot(travelDir, hit.Normal) <= 0.866025f）
            // 满足时判定为顺地表掠射滑翔：偏转速度顺沿切线滑行，不消耗反跳次数！
            // 反之（坡度 >= 90° 如垂直立墙/天花板，或入射角 < 30° 极陡直砸地面）则进入正常反弹时序，消耗反跳次数。
            bool isGroundSlope = hit.Normal.Y <= -0.05f;
            float normalImpactDot = -Vector2.Dot(travelDir, hit.Normal);
            bool isGlancingAngle = normalImpactDot <= 0.866025f;

            if (proj.IsSneakyBullet && isGroundSlope && isGlancingAngle)
            {
                Vector2 tangent = new(-hit.Normal.Y, hit.Normal.X);
                if (Vector2.Dot(travelDir, tangent) < 0f) tangent = -tangent;

                proj.Body.Velocity = tangent * speed;
                proj.Body.Position = hit.Point + hit.Normal * (proj.Radius + 1.5f);

                _manager.Particles.EmitHitSparks(hit.Point, tangent, proj.BulletColor, 4);

                // 顺切线方向推进剩余帧位移
                proj.Body.Position += proj.Body.Velocity * remainingDt;
                break;
            }

            // 【核心时序】：先判定反跳次数，再触发物理效果！
            if (proj.RemainingBounces <= 0)
            {
                // 无剩余反跳次数：当场定格在表面接触点，绝不发生物理反射与推挤
                proj.Body.Position = hit.Point;
                TriggerProjectileImpact(proj, null, m);
                _manager.Particles.EmitHitSparks(hit.Point, hit.Normal, proj.BulletColor, 8);
                proj.Destroy();
                break;
            }
            else
            {
                // 有剩余反跳次数：先消耗反跳次数，再触发物理反射效果！
                proj.RemainingBounces--;
                bouncesDone++;

                // 纯几何表面镜面反射（严格校验仅在迎面撞击时反转法向分量，绝不反向反弹砸入墙体）
                Vector2 normal = hit.Normal;
                Vector2 incomingVel = proj.Body.Velocity;
                float dot = Vector2.Dot(incomingVel, normal);
                Vector2 reflectedVel = dot < 0f ? incomingVel - 2f * dot * normal : incomingVel;

                // 弹性速度倍率与上限保护
                reflectedVel *= proj.BounceSpeedMultiplier;
                float maxBounceSpeed = 2400f;
                if (reflectedVel.LengthSquared() > maxBounceSpeed * maxBounceSpeed)
                {
                    reflectedVel = Vector2.Normalize(reflectedVel) * maxBounceSpeed;
                }
                proj.Body.Velocity = reflectedVel;

                // 安全移出墙体表面防粘连
                proj.Body.Position = hit.Point + normal * (proj.Radius + 1.5f);

                // 目标折射 (Target Bounce)：仅当敌人在子弹反弹朝向的前半平面（垂线前方）时才触发追踪折射
                // 若敌人在反弹朝向的后方半平面（Dot <= 0），则保持自然物理反射，彻底避免往左开枪反弹回射右侧敌人的荒谬现象
                if (proj.HasTargetBounce && !proj.HasTargetBounced)
                {
                    Actor enemy = (proj.Owner == _player) ? _ai : _player;
                    if (!enemy.IsDead)
                    {
                        Vector2 toEnemy = enemy.Body.Position - hit.Point;
                        // 垂线半平面判定：反弹物理速度与目标方位向量的点积必须大于 0（处于子弹反射前方 180 度半球）
                        if (Vector2.Dot(reflectedVel, toEnemy) > 0f)
                        {
                            proj.HasTargetBounced = true;
                            Vector2 startPos = proj.Body.Position;
                            Vector2 targetPos = enemy.Body.Position; // 锁定子弹碰撞瞬间敌人的原始坐标

                            float effG = _world.Gravity.Y * proj.Body.GravityScale * 1.075f;
                            float desiredSpeed = MathF.Max(proj.Body.Velocity.Length(), 820f);

                            Vector2 ballisticVel = CalculateBallisticVelocity(startPos, targetPos, desiredSpeed, effG, normal);
                            proj.Body.Velocity = ballisticVel;

                            _manager.Particles.EmitHitSparks(hit.Point, Vector2.Normalize(ballisticVel), Color.Gold, 16);
                        }
                    }
                }

                // 毒云弥漫 (Toxic Cloud)：每发生一次碰撞/反弹，在碰撞点生成持续毒雾
                if (proj.SpawnsToxicCloud)
                {
                    _toxicClouds.Add(new ToxicCloud(hit.Point, proj.Owner, 3.5f, ToxicCloud.DefaultRadius));
                }

                // 卡牌钩子通知与火花
                foreach (var card in proj.Owner.AppliedCards)
                {
                    card.OnProjectileBounce(proj, m, SpawnProjectile, _manager.Particles);
                }
                _manager.Particles.EmitHitSparks(hit.Point, normal, proj.BulletColor, 6);
            }
        }
    }

    /// <summary>
    /// 当子弹最终命中任何物体（包括角色身体、地形方块、可破坏砖块与外墙）时统一触发引力黑洞与范围爆破等卡牌连锁效果。
    /// </summary>
    private void TriggerProjectileImpact(Projectile proj, Actor? hitTarget, CollisionManifold m)
    {
        Vector2 impactPos = proj.Body.Position;

        // 毒云弥漫 (Toxic Cloud)：子弹撞击留下滞留 3.5 秒减速毒雾
        if (proj.SpawnsToxicCloud)
        {
            _toxicClouds.Add(new ToxicCloud(impactPos, proj.Owner, 3.5f, ToxicCloud.DefaultRadius));
        }

        // 1. 触发所有已挂载卡牌的 OnProjectileHit 拦截器
        foreach (var card in proj.Owner.AppliedCards)
        {
            card.OnProjectileHit(proj, hitTarget, m, _manager.Particles, _manager.Shake);
        }

        // 2. 黑洞引力坍缩拉扯（命中角色或地形均触发，将范围内所有活动角色与动态物理箱吸向爆心）
        if (proj.Owner.AppliedCards.Exists(c => c is ImplosionNovaCard) || proj.Owner.ImplosionBullets || proj.HasImplosion)
        {
            PullActorTowardsImpact(impactPos, _player, 750f, ImplosionNovaCard.PullRadius);
            PullActorTowardsImpact(impactPos, _ai, 750f, ImplosionNovaCard.PullRadius);
            PullDynamicBodiesTowardsImpact(impactPos, 750f, ImplosionNovaCard.PullRadius);
            if (!proj.Owner.AppliedCards.Exists(c => c is ImplosionNovaCard))
            {
                // 特效半径为 75px
                _manager.Particles.EmitBlackHoleImplosion(impactPos, 75f, ColorPalette.ElectricPurple);
                _manager.Shake.AddTrauma(0.42f);
            }
        }

        // 3. 范围爆破冲击波溅射伤害（若装备集束爆破或具备爆炸属性，产生真实范围杀伤）
        float splashRadius = MathF.Max(proj.ExplosionRadius, proj.Owner.AppliedCards.Exists(c => c is ClusterBombCard) ? 100f : 0f);
        if (splashRadius > 0f)
        {
            ApplySplashDamage(proj, impactPos, splashRadius, hitTarget);
        }
    }

    private void PullActorTowardsImpact(Vector2 impactPos, Actor actor, float maxImpulse = 750f, float pullRadius = ImplosionNovaCard.PullRadius)
    {
        if (actor.IsDead) return;
        float centerDist = Vector2.Distance(impactPos, actor.Body.Position);
        float edgeDist = MathF.Max(0f, centerDist - actor.Radius);
        if (edgeDist < pullRadius && centerDist > 12f)
        {
            // 强化保底吸附曲线：边缘保底具备 45% 强吸力，越靠近奇点冲量越强，确保进入 100px 圈内必定被猛烈吸附
            float normalized = MathHelper.Clamp(1f - (edgeDist / pullRadius), 0f, 1f);
            float falloff = 0.45f + 0.55f * normalized;
            Vector2 pullDir = MathHelper2D.SafeNormalize(impactPos - actor.Body.Position);

            // 若角色贴地，轻微向上抬起脱离地面摩擦束缚，确保平滑腾空吸向黑洞中心
            if (actor.Body.IsGrounded && pullDir.Y >= -0.1f)
            {
                pullDir = MathHelper2D.SafeNormalize(pullDir + new Vector2(0f, -0.35f));
                actor.Body.IsGrounded = false;
            }

            actor.Body.ApplyImpulse(pullDir * (maxImpulse * falloff));
        }
    }

    private void PullDynamicBodiesTowardsImpact(Vector2 impactPos, float maxImpulse = 750f, float pullRadius = ImplosionNovaCard.PullRadius)
    {
        foreach (var terrain in _map.TerrainObjects)
        {
            if (terrain.IsDestroyed || terrain.Body.IsStatic) continue;

            float dist;
            Vector2 toCenter;
            if (terrain.Collider is BoxCollider box)
            {
                Rectangle bounds = box.Bounds;
                Vector2 closest = new(
                    MathHelper.Clamp(impactPos.X, bounds.Left, bounds.Right),
                    MathHelper.Clamp(impactPos.Y, bounds.Top, bounds.Bottom)
                );
                dist = Vector2.Distance(impactPos, closest);
                toCenter = impactPos - terrain.Body.Position;
            }
            else
            {
                dist = Vector2.Distance(impactPos, terrain.Body.Position);
                toCenter = impactPos - terrain.Body.Position;
            }

            if (dist < pullRadius && toCenter.LengthSquared() > 144f)
            {
                float normalized = MathHelper.Clamp(1f - (dist / pullRadius), 0f, 1f);
                float falloff = 0.45f + 0.55f * normalized;
                Vector2 pullDir = MathHelper2D.SafeNormalize(toCenter);
                // 动态物理箱冲量按自身质量等比缩放，确保大箱子与小箱子均获得显著的向心拉扯位移
                terrain.Body.ApplyImpulse(pullDir * (maxImpulse * falloff * terrain.Body.Mass));
            }
        }
    }

    private void ApplySplashDamage(Projectile proj, Vector2 impactPos, float radius, Actor? directHitTarget)
    {
        // 1. 角色伤害与击退
        for (int i = 0; i < 2; i++)
        {
            var actor = (i == 0) ? _player : _ai;
            if (actor.IsDead || actor == proj.Owner || actor == directHitTarget || actor.IsInvulnerable) continue;

            float centerDist = Vector2.Distance(impactPos, actor.Body.Position);
            float edgeDist = MathF.Max(0f, centerDist - actor.Radius);
            if (edgeDist < radius)
            {
                float factor = 1f - (edgeDist / radius);
                float splashDamage = proj.Damage * 0.65f * factor;
                Vector2 blastForce = MathHelper2D.SafeNormalize(actor.Body.Position - impactPos) * (proj.KnockbackImpulse * 0.8f * factor);
                actor.TakeDamage(splashDamage, proj.Owner, blastForce, _manager.Particles, _manager.Shake);
            }
        }

        // 2. 地图物理碰撞箱：可破坏砖块承受溅射破坏，动态物理箱承受冲击波震退
        foreach (var terrain in _map.TerrainObjects)
        {
            if (terrain.IsDestroyed) continue;

            float dist;
            Vector2 fromImpact;
            if (terrain.Collider is BoxCollider box)
            {
                Rectangle bounds = box.Bounds;
                Vector2 closest = new(
                    MathHelper.Clamp(impactPos.X, bounds.Left, bounds.Right),
                    MathHelper.Clamp(impactPos.Y, bounds.Top, bounds.Bottom)
                );
                dist = Vector2.Distance(impactPos, closest);
                fromImpact = terrain.Body.Position - impactPos;
            }
            else
            {
                dist = Vector2.Distance(impactPos, terrain.Body.Position);
                fromImpact = terrain.Body.Position - impactPos;
            }

            if (dist < radius)
            {
                float factor = 1f - (dist / radius);

                // 可破坏砖块/掩体扣减耐久
                if (terrain.IsDestructible)
                {
                    float splashDamage = proj.Damage * 0.75f * factor;
                    terrain.TakeDamage(splashDamage, _manager.Particles, _manager.Shake);
                }

                // 动态物理箱体承受冲击波推力
                if (!terrain.Body.IsStatic)
                {
                    Vector2 blastDir = MathHelper2D.SafeNormalize(fromImpact);
                    if (blastDir == Vector2.Zero) blastDir = -Vector2.UnitY;
                    // 默认轻微向上掀起，形成生动的击飞与晃动效果
                    blastDir = MathHelper2D.SafeNormalize(blastDir + new Vector2(0f, -0.25f));
                    float impulseMagnitude = (proj.KnockbackImpulse * 1.2f * factor) * terrain.Body.Mass;
                    terrain.Body.ApplyImpulse(blastDir * impulseMagnitude);
                }
            }
        }
    }

    /// <summary>
    /// 检测并执行副武器能量护盾对来袭子弹的弹反偏转（反弹子弹的方向修改为子弹来时的方向）。
    /// </summary>
    private void CheckBlockDeflection(Projectile proj, Actor defender)
    {
        if (proj.IsDestroyed || proj.Owner == defender) return;

        if (defender.SubWeapon is BlockSubWeapon block && block.IsActive)
        {
            float dist = Vector2.Distance(proj.Body.Position, defender.Body.Position);
            if (dist <= block.ShieldRadius + proj.Radius)
            {
                // 反弹子弹的方向改为子弹来时的方向（反向沿原路高速射回发射源）
                Vector2 incomingDir = MathHelper2D.SafeNormalize(proj.Body.Velocity);
                float returnSpeed = MathF.Max(proj.Body.Velocity.Length() * 1.30f, 980f);
                proj.Body.Velocity = -incomingDir * returnSpeed;
                proj.Owner = defender;
                proj.BulletColor = defender.ThemeColor;

                // 纯白高光偏转火花与闪光特效
                _manager.Particles.EmitHitSparks(proj.Body.Position, -incomingDir, Color.White, 14);
                _manager.Shake.AddTrauma(0.35f);

                if (defender.AppliedCards.Exists(c => c is AbsoluteParryCard))
                {
                    block.CurrentCooldown = 0f;
                    proj.Damage *= 1.5f;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;

        // 结束默认传入的通用视口批次，以切换为摄像机世界矩阵渲染
        spriteBatch.End();

        // 1. 使用 Camera2D 世界变换矩阵绘制战场元素（地图、粒子、子弹、角色）
        Matrix cameraWorldMatrix = _camera.GetMatrix()
                                   * Matrix.CreateTranslation(_manager.Shake.OffsetX, _manager.Shake.OffsetY, 0f)
                                   * Matrix.CreateRotationZ(_manager.Shake.Angle)
                                   * _manager.Viewport.TransformMatrix;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            cameraWorldMatrix
        );

        _map.Draw(batcher, spriteBatch);
        _manager.Particles.Draw(batcher, spriteBatch);

        foreach (var cloud in _toxicClouds)
        {
            cloud.Draw(batcher, spriteBatch);
        }

        foreach (var p in _projectiles)
        {
            p.Draw(batcher, spriteBatch);
        }

        _player.Draw(batcher, spriteBatch);
        _ai.Draw(batcher, spriteBatch);

        // 绘制训练靶桩头顶信息（DPS、单发、累计）
        _debugDrawer.DrawDummyWorldHud(batcher, spriteBatch, font, _ai);

        spriteBatch.End();

        // 2. 切换回 1920x1080 屏幕空间矩阵，绘制固定 HUD、回合横幅与触控虚拟摇杆
        Matrix screenMatrix = Matrix.CreateTranslation(_manager.Shake.OffsetX * 0.2f, _manager.Shake.OffsetY * 0.2f, 0f)
                             * _manager.Viewport.TransformMatrix;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            screenMatrix
        );

        BattleHudRenderer.DrawTopBar(
            batcher,
            spriteBatch,
            font,
            _player,
            _ai,
            _playerHealthDisplay,
            _aiHealthDisplay,
            _manager.PlayerScore,
            _manager.AiScore);

        if (_roundEnded)
        {
            BattleHudRenderer.DrawRoundBanner(batcher, spriteBatch, font, _playerWonRound);
        }

        _manager.Input.DrawTouchControls(batcher, spriteBatch, font, _player);

        // 绘制卡牌实验室 / 调试抽屉与悬浮提示框
        _debugDrawer.Draw(batcher, spriteBatch, font, _player, _ai);

        // 保持批处理开启返回，以便匹配宿主 RoundsGame.Draw 的统一 End() 调用
    }

    private void HandleRadiancePulse(Actor source)
    {
        Actor target = (source == _player) ? _ai : _player;
        if (!target.IsDead && !target.IsInvulnerable && target.InvulnerabilityTimer <= 0f)
        {
            float dist = Vector2.Distance(source.Body.Position, target.Body.Position);
            if (dist <= 150f)
            {
                Vector2 blastDir = MathHelper2D.SafeNormalize(target.Body.Position - source.Body.Position);
                target.TakeDamage(10f, source, blastDir * 360f, _manager.Particles, _manager.Shake);
                _manager.Particles.EmitHitSparks(target.Body.Position, blastDir, Color.Gold, 12);
            }
        }
    }

    /// <summary>
    /// 基于物理世界重力加速度计算弹道发射速度矢量，使子弹在抛物线重力作用下精确掠过目标碰撞锁定时所在的原坐标。
    /// </summary>
    private static Vector2 CalculateBallisticVelocity(Vector2 startPos, Vector2 targetPos, float desiredSpeed, float gravity, Vector2 surfaceNormal)
    {
        float dx = targetPos.X - startPos.X;
        float dy = targetPos.Y - startPos.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 12f)
        {
            Vector2 directDir = MathHelper2D.SafeNormalize(new Vector2(dx, dy));
            return directDir * desiredSpeed;
        }

        // 计算当前重力场下到达目标所需的最低理论初速度: v^2 >= g * (dist - dy)
        // dy 为向下位移量 (屏幕坐标系向下为正):
        // 若目标在下方 (dy > 0)，重力助力，所需速度更小；若目标在上方 (dy < 0)，重力对抗，所需速度更大
        float minSpeedRequired = MathF.Sqrt(MathF.Max(0f, gravity * (dist - dy))) + 45f;
        float speed = MathF.Max(desiredSpeed, minSpeedRequired);
        speed = MathHelper.Clamp(speed, 780f, 2100f);

        float speedSq = speed * speed;
        float g = gravity;

        // 求解关于 flightTime^2 (即 u = t^2) 的二次方程:
        // A * u^2 + B * u + C = 0
        // 其中 A = 0.25 * g^2, B = -(v^2 + g * dy), C = dist^2
        float a = 0.25f * g * g;
        float b = -(speedSq + g * dy);
        float c = dist * dist;

        float discriminant = b * b - 4f * a * c;

        Vector2 launchVel;
        if (discriminant >= 0f)
        {
            float sqrtDisc = MathF.Sqrt(discriminant);
            // 选取较小的正根（平射快速弹道），飞行时间短且命中更快，杜绝高抛慢速迫击炮弧线
            float tSq = (-b - sqrtDisc) / (2f * a);
            if (tSq <= 0.0001f)
            {
                tSq = (-b + sqrtDisc) / (2f * a);
            }

            if (tSq > 0.0001f)
            {
                float t = MathF.Sqrt(tSq);
                float vx = dx / t;
                float vy = (dy / t) - (0.5f * g * t);
                launchVel = new Vector2(vx, vy);
            }
            else
            {
                launchVel = MathHelper2D.SafeNormalize(new Vector2(dx, dy)) * speed;
            }
        }
        else
        {
            // 极低概率兜底（以最大射程抛角或直射补偿朝目标方向发射）
            launchVel = MathHelper2D.SafeNormalize(new Vector2(dx, dy - 0.45f * dist)) * speed;
        }

        // 防止反弹后的发射矢量倒扣扎入刚刚碰撞的墙面表面
        if (surfaceNormal != Vector2.Zero)
        {
            float normSpeed = launchVel.Length();
            if (normSpeed > 0.001f)
            {
                Vector2 dir = launchVel / normSpeed;
                float dot = Vector2.Dot(dir, surfaceNormal);
                if (dot < 0.15f)
                {
                    dir = MathHelper2D.SafeNormalize(dir + surfaceNormal * (0.25f - dot));
                    launchVel = dir * speed;
                }
            }
        }

        return launchVel;
    }

    /// <inheritdoc />
    public void Exit()
    {
        _player.OnRadiancePulse -= HandleRadiancePulse;
        _ai.OnRadiancePulse -= HandleRadiancePulse;
        _map.CleanPhysicsWorld(_world);
    }
}
