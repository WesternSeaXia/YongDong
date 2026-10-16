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
    }

    /// <inheritdoc />
    public void Update(float rawDt)
    {
        float dt = rawDt * _timeScale;

        // 1. 采集玩家输入与 AI 决策（传入摄像机视口逆映射以校准鼠标瞄准准星）
        ControlInputState playerInput = _manager.Input.GetPlayerInputState(_player, _camera.ScreenToWorld);
        ControlInputState aiInput = _aiBrain.Think(_ai, _player, _projectiles, _world, _map, dt);

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

        // 6. 更新与清理活跃子弹
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Update(dt, _manager.Particles);

            // 检查副武器格挡力场偏转
            CheckBlockDeflection(p, _player);
            CheckBlockDeflection(p, _ai);

            if (p.IsDestroyed)
            {
                _world.RemoveCollider(p.Collider);
                _projectiles.RemoveAt(i);
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
        CheckProjectileCollision(m.ColliderA, m.ColliderB, m);
        CheckProjectileCollision(m.ColliderB, m.ColliderA, m);

        CheckHazardCollision(m.ColliderA, m.ColliderB, m);
        CheckHazardCollision(m.ColliderB, m.ColliderA, m);
    }

    private void CheckHazardCollision(Collider actorCol, Collider otherCol, CollisionManifold m)
    {
        Actor? actor = (actorCol.Body == _player.Body) ? _player : (actorCol.Body == _ai.Body ? _ai : null);
        if (actor == null || actor.IsDead || actor.IsInvulnerable) return;

        var terrain = _map.TerrainObjects.Find(t => t.Collider == otherCol);
        if (terrain != null && terrain.Style == TerrainStyle.HazardSpike)
        {
            Vector2 spikeForce = -m.Normal * 450f;
            actor.TakeDamage(18f, null, spikeForce, _manager.Particles, _manager.Shake);
        }
    }

    private void CheckProjectileCollision(Collider maybeProjCol, Collider otherCol, CollisionManifold m)
    {
        Projectile? proj = _projectiles.Find(p => p.Collider == maybeProjCol && !p.IsDestroyed);
        if (proj == null) return;

        // 1. 子弹命中角色
        if (otherCol is CircleCollider cc && (cc.Body == _player.Body || cc.Body == _ai.Body))
        {
            Actor? hitTarget = (cc.Body == _player.Body) ? _player : _ai;
            if (hitTarget != proj.Owner && !hitTarget.IsInvulnerable)
            {
                Vector2 hitForce = MathHelper2D.SafeNormalize(proj.Body.Velocity) * proj.KnockbackImpulse;
                hitTarget.TakeDamage(proj.Damage, proj.Owner, hitForce, _manager.Particles, _manager.Shake);

                TriggerProjectileImpact(proj, hitTarget, m);
                proj.Destroy();
            }
            return;
        }

        // 2. 子弹撞击地形（支持矩形、多边形、圆形、动态物理箱与可破坏掩体）
        var hitTerrain = _map.TerrainObjects.Find(t => t.Collider == otherCol);
        if (hitTerrain != null)
        {
            // 动态物理箱受子弹强力物理冲量驱动（大幅推动并引起链条摆动）
            if (!hitTerrain.IsStatic)
            {
                Vector2 hitImpulse = MathHelper2D.SafeNormalize(proj.Body.Velocity) * 380f;
                hitTerrain.Body.ApplyImpulse(hitImpulse);
            }

            // 可破坏地形扣减生命值
            if (hitTerrain.IsDestructible)
            {
                hitTerrain.TakeDamage(proj.Damage, _manager.Particles, _manager.Shake);
            }

            if (proj.RemainingBounces > 0)
            {
                proj.OnBounce(m.Normal);
                foreach (var card in proj.Owner.AppliedCards)
                {
                    card.OnProjectileBounce(proj, m, SpawnProjectile, _manager.Particles);
                }
                _manager.Particles.EmitHitSparks(m.ContactPoint, m.Normal, proj.BulletColor, 6);
            }
            else
            {
                TriggerProjectileImpact(proj, null, m);
                _manager.Particles.EmitHitSparks(m.ContactPoint, m.Normal, proj.BulletColor, 8);
                proj.Destroy();
            }
            return;
        }

        // 3. 子弹撞击默认外墙与边界
        if (otherCol is BoxCollider or PolygonCollider)
        {
            if (proj.RemainingBounces > 0)
            {
                proj.OnBounce(m.Normal);
                foreach (var card in proj.Owner.AppliedCards)
                {
                    card.OnProjectileBounce(proj, m, SpawnProjectile, _manager.Particles);
                }
                _manager.Particles.EmitHitSparks(m.ContactPoint, m.Normal, proj.BulletColor, 6);
            }
            else
            {
                TriggerProjectileImpact(proj, null, m);
                _manager.Particles.EmitHitSparks(m.ContactPoint, m.Normal, proj.BulletColor, 8);
                proj.Destroy();
            }
        }
    }

    /// <summary>
    /// 当子弹最终命中任何物体（包括角色身体、地形方块、可破坏砖块与外墙）时统一触发引力黑洞与范围爆破等卡牌连锁效果。
    /// </summary>
    private void TriggerProjectileImpact(Projectile proj, Actor? hitTarget, CollisionManifold m)
    {
        Vector2 impactPos = proj.Body.Position;

        // 1. 触发所有已挂载卡牌的 OnProjectileHit 拦截器
        foreach (var card in proj.Owner.AppliedCards)
        {
            card.OnProjectileHit(proj, hitTarget, m, _manager.Particles, _manager.Shake);
        }

        // 2. 黑洞引力坍缩拉扯（命中角色或地形均触发，将范围内所有活动角色吸向爆心）
        if (proj.Owner.AppliedCards.Exists(c => c is ImplosionNovaCard) || proj.Owner.ImplosionBullets)
        {
            PullActorTowardsImpact(impactPos, _player, 640f);
            PullActorTowardsImpact(impactPos, _ai, 640f);
        }

        // 3. 范围爆破冲击波溅射伤害（若装备集束爆破或具备爆炸属性，产生真实范围杀伤）
        float splashRadius = MathF.Max(proj.ExplosionRadius, proj.Owner.AppliedCards.Exists(c => c is ClusterBombCard) ? 140f : 0f);
        if (splashRadius > 0f)
        {
            ApplySplashDamage(proj, impactPos, splashRadius, hitTarget);
        }
    }

    private void PullActorTowardsImpact(Vector2 impactPos, Actor actor, float maxImpulse)
    {
        if (actor.IsDead) return;
        float dist = Vector2.Distance(impactPos, actor.Body.Position);
        if (dist < 420f && dist > 12f)
        {
            float falloff = 1f - (dist / 420f);
            Vector2 pullDir = MathHelper2D.SafeNormalize(impactPos - actor.Body.Position);
            actor.Body.ApplyImpulse(pullDir * (maxImpulse * falloff));
        }
    }

    private void ApplySplashDamage(Projectile proj, Vector2 impactPos, float radius, Actor? directHitTarget)
    {
        for (int i = 0; i < 2; i++)
        {
            var actor = (i == 0) ? _player : _ai;
            if (actor.IsDead || actor == proj.Owner || actor == directHitTarget || actor.IsInvulnerable) continue;

            float dist = Vector2.Distance(impactPos, actor.Body.Position);
            if (dist < radius)
            {
                float factor = 1f - (dist / radius);
                float splashDamage = proj.Damage * 0.65f * factor;
                Vector2 blastForce = MathHelper2D.SafeNormalize(actor.Body.Position - impactPos) * (proj.KnockbackImpulse * 0.8f * factor);
                actor.TakeDamage(splashDamage, proj.Owner, blastForce, _manager.Particles, _manager.Shake);
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

        foreach (var p in _projectiles)
        {
            p.Draw(batcher, spriteBatch);
        }

        _player.Draw(batcher, spriteBatch);
        _ai.Draw(batcher, spriteBatch);

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

        // 保持批处理开启返回，以便匹配宿主 RoundsGame.Draw 的统一 End() 调用
    }

    /// <inheritdoc />
    public void Exit()
    {
        _map.CleanPhysicsWorld(_world);
    }
}
