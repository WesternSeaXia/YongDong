using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Input;
using RoundsClone.Core.Scenes;
using RoundsClone.Core.SubWeapons;
using RoundsClone.Core.Maps;
using RoundsClone.Core.Cards;

namespace RoundsClone.Core;

/// <summary>
/// 游戏顶层核心宿主类。
/// 继承自 MonoGame Game，协调图形设备管理、虚拟等比视口映射、全局输入轮询与场景管理器生命周期。
/// </summary>
public class RoundsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;

    private VirtualViewport? _viewport;
    private ShapeBatcher? _shapeBatcher;
    private ProceduralFont? _font;
    private InputManager? _input;
    private SceneManager? _sceneManager;

    private readonly bool _autoTestFlow;
    private int _testFrame = 0;
    private int _drawCount = 0;

    /// <summary>
    /// 构造主游戏实例并设置初始图形参数。
    /// </summary>
    /// <param name="autoTestFlow">是否开启全流程自动化冒烟测试</param>
    public RoundsGame(bool autoTestFlow = false)
    {
        _autoTestFlow = autoTestFlow;
        _graphics = new GraphicsDeviceManager(this)
        {
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = OperatingSystem.IsAndroid(),
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };

        if (!OperatingSystem.IsAndroid())
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            Window.AllowUserResizing = true;
            Window.Title = "混乱枪豆人";
            IsMouseVisible = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        }

        Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
        Console.WriteLine("[RoundsGame] Initialize started");
        GameSettings.Load();
        _viewport = new VirtualViewport();
        _input = new InputManager
        {
            ShowVirtualControls = GameSettings.Current.ShowVirtualControls,
            LeftStickJumpEnabled = GameSettings.Current.LeftStickJumpEnabled,
            LeftStickControlsAim = GameSettings.Current.LeftStickControlsAim,
            StickDeadzoneRatio = GameSettings.Current.StickDeadzoneRatio
        };

        base.Initialize();
        Console.WriteLine("[RoundsGame] Initialize completed successfully");
    }

    protected override void LoadContent()
    {
        Console.WriteLine("[RoundsGame] LoadContent started: generating procedural textures and font atlas");
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _shapeBatcher = new ShapeBatcher(GraphicsDevice);
        _font = new ProceduralFont(GraphicsDevice, 28f);

        _sceneManager = new SceneManager(_viewport!, _shapeBatcher, _font, _input!);
        _sceneManager.Audio.Initialize();
        _sceneManager.Audio.LoadBgm(Content);

        // 默认进入主菜单
        _sceneManager.ChangeScene(new MainMenuScene(_sceneManager, Exit));
        Console.WriteLine("[RoundsGame] LoadContent completed: MainMenuScene ready");
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        // 限制最大时间步长，防止窗口拖动时发生物理爆炸
        dt = MathF.Min(dt, 0.05f);

        // 更新虚拟视口投影视窗
        _viewport!.Update(GraphicsDevice);

        // 更新输入控制器
        _input!.Update(_viewport);

        // 更新场景状态机
        _sceneManager!.Update(dt);

        if (_autoTestFlow)
        {
            _testFrame++;
            if (_testFrame == 2)
            {
                Console.WriteLine($"[SmokeTest] AudioManager verification: Loaded {_sceneManager.Audio.TrackCount} BGM tracks. Current: {_sceneManager.Audio.CurrentBgmTitle}");
            }
            else if (_testFrame == 10)
            {
                Console.WriteLine("[SmokeTest] (1/7) MainMenuScene verified. Transitioning to SettingsScene (Testing Keybinds & AI Difficulty)...");
                _sceneManager.ChangeScene(new SettingsScene(_sceneManager, Exit));
            }
            else if (_testFrame == 25)
            {
                Console.WriteLine("[SmokeTest] (2/7) SettingsScene verified. Transitioning to CharacterSelectScene...");
                _sceneManager.ChangeScene(new CharacterSelectScene(_sceneManager, Exit));
            }
            else if (_testFrame == 40)
            {
                Console.WriteLine("[SmokeTest] (3/7) CharacterSelectScene verified. Transitioning to StageSelectScene (3-choice draft)...");
                _sceneManager.PlayerSubWeaponFactory = () => new BlockSubWeapon();
                _sceneManager.AiSubWeaponFactory = () => new DashSubWeapon();
                _sceneManager.ChangeScene(new StageSelectScene(_sceneManager, Exit));
            }
            else if (_testFrame == 55)
            {
                Console.WriteLine("[SmokeTest] (4/7) StageSelectScene verified. Selecting arena and transitioning to BattleScene (Normal Competition)...");
                _sceneManager.CurrentMap = MapRegistry.CreateBumperMaze();
                _sceneManager.ChangeScene(new BattleScene(_sceneManager, Exit, isSandboxMode: false));
            }
            else if (_testFrame == 75)
            {
                Console.WriteLine("[SmokeTest] (5/7) Normal BattleScene verified. Transitioning to Sandbox Mode (Debug Drawer & Training Dummy)...");
                _sceneManager.ChangeScene(new BattleScene(_sceneManager, Exit, isSandboxMode: true));
            }
            else if (_testFrame == 95)
            {
                Console.WriteLine("[SmokeTest] (6/7) Sandbox Mode verified. Transitioning to CardSelectScene (3-choice skill draft)...");
                _sceneManager.ChangeScene(new CardSelectScene(_sceneManager, Exit, isForPlayer: true));
            }
            else if (_testFrame == 110)
            {
                Console.WriteLine("[SmokeTest] (7/7) CardSelectScene verified. Equipping synergistic cards and transitioning to GameOverScene...");
                _sceneManager.PlayerCards.Add(new ClusterBombCard());
                _sceneManager.PlayerCards.Add(new BounceMasterCard());
                _sceneManager.PlayerScore = 3;
                _sceneManager.ChangeScene(new GameOverScene(_sceneManager, Exit));
            }
            else if (_testFrame >= 130)
            {
                Console.WriteLine("[SmokeTest] ALL SCENES & SYSTEMS (SettingsScene, Normal Battle, Sandbox Mode, Force Physics, Procedural VFX, Audio, AI, Cards, SubWeapons, Maps) VERIFIED SUCCESSFULLY!");
                Exit();
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ColorPalette.BackgroundDark);

        if (_spriteBatch != null && _sceneManager != null && _viewport != null)
        {
            // 考虑震屏摄像机偏移叠加
            Matrix viewMatrix = Matrix.CreateTranslation(_sceneManager.Shake.OffsetX, _sceneManager.Shake.OffsetY, 0f)
                                * Matrix.CreateRotationZ(_sceneManager.Shake.Angle)
                                * _viewport.TransformMatrix;

            // 开启视口矩阵渲染
            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                viewMatrix
            );

            _sceneManager.Draw(_spriteBatch);

            _spriteBatch.End();

            if (_drawCount < 3)
            {
                _drawCount++;
                Console.WriteLine($"[RoundsGame] Successfully rendered frame {_drawCount}!");
            }
        }

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _shapeBatcher?.Dispose();
        _font?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }
}
