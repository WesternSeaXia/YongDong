using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Input;
using RoundsClone.Core.Maps;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 游戏场景生命周期接口。
/// </summary>
public interface IScene
{
    /// <summary>
    /// 进入场景时调用。
    /// </summary>
    void Enter();

    /// <summary>
    /// 帧更新场景逻辑。
    /// </summary>
    void Update(float dt);

    /// <summary>
    /// 渲染场景画面。
    /// </summary>
    void Draw(SpriteBatch spriteBatch);

    /// <summary>
    /// 离开场景时调用。
    /// </summary>
    void Exit();
}

/// <summary>
/// 全局场景状态机与战局上下文管理器。
/// 协调场景流转（主菜单 -> 角色定制 -> 关卡三选一 -> 战斗对决 -> 技能三选一 -> 结算）。
/// </summary>
public class SceneManager
{
    public VirtualViewport Viewport { get; }
    public ShapeBatcher ShapeBatcher { get; }
    public ProceduralFont Font { get; }
    public InputManager Input { get; }
    public ParticleSystem Particles { get; } = new();
    public ScreenShake Shake { get; } = new();

    private IScene? _currentScene;

    // --- 全局持久对决上下文数据 ---
    public Color PlayerColor { get; set; } = ColorPalette.PlayerBlue;
    public Color AiColor { get; set; } = ColorPalette.PlayerRed;

    public EyeStyle PlayerEyeStyle { get; set; } = EyeStyle.Classic;
    public EyeStyle AiEyeStyle { get; set; } = EyeStyle.Sneaky;

    public Func<ISubWeapon> PlayerSubWeaponFactory { get; set; } = () => new BlockSubWeapon();
    public Func<ISubWeapon> AiSubWeaponFactory { get; set; } = () => new DashSubWeapon();

    public List<ICardPlugin> PlayerCards { get; } = new();
    public List<ICardPlugin> AiCards { get; } = new();

    public int PlayerScore { get; set; } = 0;
    public int AiScore { get; set; } = 0;
    public const int TargetWins = 5;

    public MapData? CurrentMap { get; set; }
    public bool LastRoundPlayerWon { get; set; } = false;

    public SceneManager(VirtualViewport viewport, ShapeBatcher shapeBatcher, ProceduralFont font, InputManager input)
    {
        Viewport = viewport;
        ShapeBatcher = shapeBatcher;
        Font = font;
        Input = input;
    }

    /// <summary>
    /// 切换到新场景并执行退出与进入生命周期。
    /// </summary>
    public void ChangeScene(IScene newScene)
    {
        _currentScene?.Exit();
        _currentScene = newScene;
        _currentScene.Enter();
    }

    /// <summary>
    /// 重置整场比赛的比分与卡牌积累（重新开始整场对抗）。
    /// </summary>
    public void ResetMatch()
    {
        PlayerScore = 0;
        AiScore = 0;
        PlayerCards.Clear();
        AiCards.Clear();
        CurrentMap = null;
    }

    /// <summary>
    /// 帧更新当前活跃场景。
    /// </summary>
    public void Update(float dt)
    {
        Particles.Update(dt);
        Shake.Update(dt);
        _currentScene?.Update(dt);
    }

    /// <summary>
    /// 渲染当前活跃场景。
    /// </summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        _currentScene?.Draw(spriteBatch);
    }
}
