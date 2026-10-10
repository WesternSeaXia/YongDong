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
/// 包含物理世界步进、子弹弹道碰撞解算、格挡反弹拦截、慢动作受击反馈、HUD 界面以及回合胜负结算。
/// </summary>
public class BattleScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;

    private CollisionWorld _world = new();
    private MapData _map;
    private Actor _player;
    private Actor _ai;
    private IAiBrain _aiBrain = new TacticalCombatAi();

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

        // 实例化实体
        _player = new Actor(_map.PlayerSpawn, _manager.PlayerColor, isAi: false)
        {
            Name = "玩家",
            SubWeapon = _manager.PlayerSubWeaponFactory()
        };

        _ai = new Actor(_map.AiSpawn, _manager.AiColor, isAi: true)
        {
            Name = "AI 对手",
            SubWeapon = _manager.AiSubWeaponFactory()
        };
    }

    public void Enter()
    {
        _world.Clear();
        _map.BuildPhysicsWorld(_world);

        _world.AddCollider(_player.Collider);
        _world.AddCollider(_ai.Collider);

        // 应用双方累计卡牌
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

    public void Update(float rawDt)
    {
        // 慢动作缩放 (击杀定格冲击力)
        float dt = rawDt * _timeScale;

        // 1. 采集输入
        ControlInputState playerInput = _manager.Input.GetPlayerInputState(_player);
        ControlInputState aiInput = _aiBrain.Think(_ai, _player, _projectiles, _world, dt);

        // 2. 更新实体
        _player.Update(dt, playerInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);
        _ai.Update(dt, aiInput, _world, _manager.Particles, _manager.Shake, SpawnProjectile);

        // 3. 物理世界模拟步进与碰撞判定
        _world.Step(dt, OnPhysicsCollision);

        // 4. 处理新增子弹
        if (_spawnQueue.Count > 0)
        {
            foreach (var p in _spawnQueue)
            {
                _projectiles.Add(p);
                _world.AddCollider(p.Collider);
            }
            _spawnQueue.Clear();
        }

        // 5. 更新与清理子弹
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

        // 7. 回合终局判定
        if (!_roundEnded)
        {
            if (_player.IsDead || _ai.IsDead)
            {
                _roundEnded = true;
                _roundEndTimer = 0f;
                _timeScale = 0.25f; // 慢动作爆发
                _manager.Shake.AddTrauma(0.7f);

                if (_player.IsDead && _ai.IsDead)
                {
                    _playerWonRound = false; // 平局
                }
                else
                {
                    _playerWonRound = !_player.IsDead;
                }

                if (_playerWonRound) _manager.PlayerScore++;
                else _manager.AiScore++;

                _manager.LastRoundPlayerWon = _playerWonRound;
            }
        }
        else
        {
            _roundEndTimer += rawDt; // 使用真实时间倒计时
            if (_roundEndTimer >= 1.6f)
            {
                // 回合结算跳转
                if (_manager.PlayerScore >= SceneManager.TargetWins || _manager.AiScore >= SceneManager.TargetWins)
                {
                    // 比赛完全结束
                    _manager.ChangeScene(new GameOverScene(_manager, _exitAction));
                }
                else
                {
                    // 根据 Rounds 经典平衡机制：回合落后者挑选强化卡牌反制！
                    if (!_playerWonRound)
                    {
                        // 玩家输了，进入玩家技能三选一
                        _manager.ChangeScene(new CardSelectScene(_manager, _exitAction, isForPlayer: true));
                    }
                    else
                    {
                        // AI 输了，AI 自动从卡池中随机抽取一张卡牌强化，然后玩家选下一关
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

        // ESC 键随时返回
        if (_manager.Input.IsKeyJustPressed(Keys.Escape))
        {
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    private void SpawnProjectile(Projectile p)
    {
        _spawnQueue.Add(p);
    }

    private void OnPhysicsCollision(CollisionManifold m)
    {
        // 检查子弹撞击
        CheckProjectileCollision(m.ColliderA, m.ColliderB, m);
        CheckProjectileCollision(m.ColliderB, m.ColliderA, m);
    }

    private void CheckProjectileCollision(Collider maybeProjCol, Collider otherCol, CollisionManifold m)
    {
        Projectile? proj = _projectiles.Find(p => p.Collider == maybeProjCol && !p.IsDestroyed);
        if (proj == null) return;

        // 1. 子弹撞墙
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
                // 无反弹次数：直接粉碎并产生撞击火花
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

    private void CheckBlockDeflection(Projectile proj, Actor defender)
    {
        if (proj.IsDestroyed || proj.Owner == defender) return;

        if (defender.SubWeapon is BlockSubWeapon block && block.IsActive)
        {
            float dist = Vector2.Distance(proj.Body.Position, defender.Body.Position);
            if (dist <= block.ShieldRadius + proj.Radius)
            {
                // 成功格挡偏转：反转速度矢量并加速 40%，拥有者变更为格挡者
                proj.Body.Velocity = -proj.Body.Velocity * 1.4f;
                proj.Owner = defender;
                proj.BulletColor = defender.ThemeColor;

                // 特效反馈
                _manager.Particles.EmitShieldDeflectWave(proj.Body.Position, 40f, defender.ThemeColor);
                _manager.Shake.AddTrauma(0.35f);

                // 检查绝对弹反卡牌
                if (defender.AppliedCards.Exists(c => c is AbsoluteParryCard))
                {
                    block.CurrentCooldown = 0f;
                    proj.Damage *= 1.5f;
                }
            }
        }
    }

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
        DrawBattleHUD(batcher, spriteBatch, font);

        // 6. 渲染触控虚拟摇杆
        _manager.Input.DrawTouchControls(batcher, spriteBatch, font);

        // 7. 回合结算大字横幅
        if (_roundEnded)
        {
            string bannerText = _playerWonRound ? "回合胜利" : "回合失败";
            Color bannerColor = _playerWonRound ? ColorPalette.PlayerGreen : ColorPalette.PlayerRed;
            Vector2 center = new(VirtualViewport.VirtualWidth * 0.5f, 460f);

            batcher.DrawRectangle(spriteBatch, new Rectangle(0, (int)center.Y - 60, VirtualViewport.VirtualWidth, 120), Color.Black * 0.65f);
            font.DrawText(spriteBatch, bannerText, center, bannerColor, 2.6f, new Vector2(0.5f, 0.5f));
        }
    }

    private void DrawBattleHUD(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font)
    {
        // 顶部 HUD 栏背景
        Rectangle topBar = new(0, 0, VirtualViewport.VirtualWidth, 82);
        batcher.DrawRectangle(spriteBatch, topBar, ColorPalette.BackgroundDark * 0.85f);
        batcher.DrawLine(spriteBatch, new Vector2(0, 82), new Vector2(VirtualViewport.VirtualWidth, 82), ColorPalette.WallOutline, 2f);

        // --- 玩家血条 (左) ---
        float pMaxHp = _player.MaxHealth;
        float pHpPct = MathHelper.Clamp(_playerHealthDisplay / pMaxHp, 0f, 1f);
        Rectangle pBarBg = new(80, 22, 420, 26);
        Rectangle pBarFill = new(80, 22, (int)(420 * pHpPct), 26);
        batcher.DrawRoundedRect(spriteBatch, pBarBg, 6f, Color.Black * 0.6f);
        batcher.DrawRoundedRect(spriteBatch, pBarFill, 6f, ColorPalette.PlayerBlue);
        batcher.DrawRectangleOutline(spriteBatch, pBarBg, ColorPalette.WallOutline, 1.5f);
        font.DrawText(spriteBatch, $"{(int)_player.CurrentHealth} / {(int)pMaxHp}", new Vector2(92, 24), Color.White, 0.68f);

        // 玩家副武器 CD 状态条
        if (_player.SubWeapon != null)
        {
            float cdPct = 1f - MathHelper.Clamp(_player.SubWeapon.CurrentCooldown / _player.SubWeapon.Cooldown, 0f, 1f);
            Rectangle cdBar = new(80, 54, 180, 12);
            batcher.DrawRectangle(spriteBatch, cdBar, Color.Black * 0.5f);
            batcher.DrawRectangle(spriteBatch, new Rectangle(80, 54, (int)(180 * cdPct), 12), _player.SubWeapon.IsReady ? ColorPalette.BlockShield : ColorPalette.TextGray);
        }

        // --- 中央回合比分与胜场圆点 ---
        Vector2 centerScore = new(VirtualViewport.VirtualWidth * 0.5f, 41f);
        font.DrawText(spriteBatch, $"{_manager.PlayerScore}  :  {_manager.AiScore}", centerScore, Color.White, 1.5f, new Vector2(0.5f, 0.5f));

        for (int i = 0; i < SceneManager.TargetWins; i++)
        {
            Vector2 pDot = new(centerScore.X - 95f - i * 22f, centerScore.Y);
            if (i < _manager.PlayerScore)
                batcher.DrawFilledCircle(spriteBatch, pDot, 7f, ColorPalette.PlayerBlue);
            else
                batcher.DrawCircleOutline(spriteBatch, pDot, 7f, ColorPalette.WallOutline, 2f);

            Vector2 aiDot = new(centerScore.X + 95f + i * 22f, centerScore.Y);
            if (i < _manager.AiScore)
                batcher.DrawFilledCircle(spriteBatch, aiDot, 7f, ColorPalette.PlayerRed);
            else
                batcher.DrawCircleOutline(spriteBatch, aiDot, 7f, ColorPalette.WallOutline, 2f);
        }

        // --- AI 对手血条 (右) ---
        float aiMaxHp = _ai.MaxHealth;
        float aiHpPct = MathHelper.Clamp(_aiHealthDisplay / aiMaxHp, 0f, 1f);
        Rectangle aiBarBg = new(VirtualViewport.VirtualWidth - 500, 22, 420, 26);
        Rectangle aiBarFill = new(VirtualViewport.VirtualWidth - 500 + (int)(420 * (1f - aiHpPct)), 22, (int)(420 * aiHpPct), 26);
        batcher.DrawRoundedRect(spriteBatch, aiBarBg, 6f, Color.Black * 0.6f);
        batcher.DrawRoundedRect(spriteBatch, aiBarFill, 6f, ColorPalette.PlayerRed);
        batcher.DrawRectangleOutline(spriteBatch, aiBarBg, ColorPalette.WallOutline, 1.5f);
        font.DrawText(spriteBatch, $"{(int)_ai.CurrentHealth} / {(int)aiMaxHp}", new Vector2(VirtualViewport.VirtualWidth - 92, 24), Color.White, 0.68f, new Vector2(1f, 0f));

        // AI 副武器 CD
        if (_ai.SubWeapon != null)
        {
            float aiCdPct = 1f - MathHelper.Clamp(_ai.SubWeapon.CurrentCooldown / _ai.SubWeapon.Cooldown, 0f, 1f);
            Rectangle aiCdBar = new(VirtualViewport.VirtualWidth - 260, 54, 180, 12);
            batcher.DrawRectangle(spriteBatch, aiCdBar, Color.Black * 0.5f);
            batcher.DrawRectangle(spriteBatch, new Rectangle(VirtualViewport.VirtualWidth - 260 + (int)(180 * (1f - aiCdPct)), 54, (int)(180 * aiCdPct), 12), ColorPalette.DashTrail);
        }
    }

    public void Exit()
    {
        _map.CleanPhysicsWorld(_world);
    }
}
