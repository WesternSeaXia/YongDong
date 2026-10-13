using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.AI;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Maps;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 核心战斗决斗场场景。
/// 职责边界：负责协调单局战斗的物理世界步进、角色与 AI 行为更新、子弹碰撞与格挡偏转判定以及回合胜负结算流转；UI 绘制委托给 <see cref="BattleHudRenderer"/>。
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

        _roundEnded = false;
        _roundEndTimer = 0f;
        _timeScale = 1.0f;
        _projectiles.Clear();
    }

    /// <inheritdoc />
    public void Update(float rawDt)
    {
        // 慢动作缩放 (击杀定格冲击力)
        float dt = rawDt * _timeScale;

        // 1. 采集玩家输入与 AI 决策
        ControlInputState playerInput = _manager.Input.GetPlayerInputState(_player);
        ControlInputState aiInput = _aiBrain.Think(_ai, _player, _projectiles, _world, dt);

        // 2. 更新角色实体
        _player.Update(dt, playerInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);
        _ai.Update(dt, aiInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);

        // 3. 物理世界模拟步进与碰撞判定
        _world.Step(dt, OnPhysicsCollision);

        // 4. 注册新派生子弹
        if (_spawnQueue.Count > 0)
        {
            foreach (var p in _spawnQueue)
            {
                _projectiles.Add(p);
                _world.AddCollider(p.Collider);
            }
            _spawnQueue.Clear();
        }

        // 5. 更新与清理活跃子弹
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

        // 6. 血条平滑插值
        _playerHealthDisplay = MathHelper.Lerp(_playerHealthDisplay, _player.CurrentHealth, dt * 10f);
        _aiHealthDisplay = MathHelper.Lerp(_aiHealthDisplay, _ai.CurrentHealth, dt * 10f);

        // 7. 回合终局判定与流转
        UpdateRoundLifecycle(rawDt);

        // ESC 键随时返回主菜单
        if (_manager.Input.IsKeyJustPressed(Keys.Escape))
        {
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    /// <summary>
    /// 维护单局胜负结算、慢动作定格与下一场景跳转。
    /// </summary>
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
                    // 回合落后者挑选强化卡牌反制
                    _manager.ChangeScene(new CardSelectScene(_manager, _exitAction, isForPlayer: true));
                }
                else
                {
                    // AI 落败时自动从卡池抽取一张卡牌强化，随后进入地图选择
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
    }

    /// <summary>
    /// 处理子弹与墙体或角色碰撞的伤害、击退及技能卡钩子派发。
    /// </summary>
    private void CheckProjectileCollision(Collider maybeProjCol, Collider otherCol, CollisionManifold m)
    {
        Projectile? proj = _projectiles.Find(p => p.Collider == maybeProjCol && !p.IsDestroyed);
        if (proj == null) return;

        // 1. 子弹撞击墙体
        if (otherCol is BoxCollider)
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
                _manager.Particles.EmitHitSparks(m.ContactPoint, m.Normal, proj.BulletColor, 8);
                proj.Destroy();
            }
        }
        // 2. 子弹命中角色
        else if (otherCol is CircleCollider cc)
        {
            Actor? hitTarget = (cc.Body == _player.Body) ? _player : (cc.Body == _ai.Body ? _ai : null);
            if (hitTarget != null && hitTarget != proj.Owner && !hitTarget.IsInvulnerable)
            {
                Vector2 hitForce = MathHelper2D.SafeNormalize(proj.Body.Velocity) * proj.KnockbackImpulse;
                hitTarget.TakeDamage(proj.Damage, proj.Owner, hitForce, _manager.Particles, _manager.Shake);

                foreach (var card in proj.Owner.AppliedCards)
                {
                    card.OnProjectileHit(proj, hitTarget, m, _manager.Particles, _manager.Shake);
                }

                proj.Destroy();
            }
        }
    }

    /// <summary>
    /// 检测并执行副武器能量护盾对来袭子弹的弹反偏转。
    /// </summary>
    private void CheckBlockDeflection(Projectile proj, Actor defender)
    {
        if (proj.IsDestroyed || proj.Owner == defender) return;

        if (defender.SubWeapon is BlockSubWeapon block && block.IsActive)
        {
            float dist = Vector2.Distance(proj.Body.Position, defender.Body.Position);
            if (dist <= block.ShieldRadius + proj.Radius)
            {
                proj.Body.Velocity = -proj.Body.Velocity * 1.4f;
                proj.Owner = defender;
                proj.BulletColor = defender.ThemeColor;

                _manager.Particles.EmitShieldDeflectWave(proj.Body.Position, 40f, defender.ThemeColor);
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

        // 1. 渲染地图
        _map.Draw(batcher, spriteBatch);

        // 2. 渲染粒子
        _manager.Particles.Draw(batcher, spriteBatch);

        // 3. 渲染子弹
        foreach (var p in _projectiles)
        {
            p.Draw(batcher, spriteBatch);
        }

        // 4. 渲染角色
        _player.Draw(batcher, spriteBatch);
        _ai.Draw(batcher, spriteBatch);

        // 5. 渲染顶部对战 HUD
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

        // 6. 渲染触控虚拟摇杆与按钮
        _manager.Input.DrawTouchControls(batcher, spriteBatch, font, _player);

        // 7. 回合结算大字横幅
        if (_roundEnded)
        {
            BattleHudRenderer.DrawRoundBanner(batcher, spriteBatch, font, _playerWonRound);
        }
    }

    /// <inheritdoc />
    public void Exit()
    {
        _map.CleanPhysicsWorld(_world);
    }
}
