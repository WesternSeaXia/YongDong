using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 主菜单场景。
/// 包含几何动效背景、标题徽记、开始比赛、触屏摇杆开关及按键操作指南。
/// </summary>
public class MainMenuScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitGameAction;
    private float _time = 0f;

    // 菜单按钮交互区域
    private Rectangle _btnStart;
    private Rectangle _btnToggleControls;
    private Rectangle _btnExit;

    public MainMenuScene(SceneManager manager, Action exitGameAction)
    {
        _manager = manager;
        _exitGameAction = exitGameAction;

        int cx = VirtualViewport.VirtualWidth / 2;
        int btnW = 360;
        int btnH = 65;

        _btnStart = new Rectangle(cx - btnW / 2, 520, btnW, btnH);
        _btnToggleControls = new Rectangle(cx - btnW / 2, 615, btnW, btnH);
        _btnExit = new Rectangle(cx - btnW / 2, 710, btnW, btnH);
    }

    public void Enter()
    {
    }

    public void Update(float dt)
    {
        _time += dt;

        // 生成背景微粒
        if (Random.Shared.NextDouble() < 0.35)
        {
            Vector2 p = new(
                (float)(Random.Shared.NextDouble() * VirtualViewport.VirtualWidth),
                VirtualViewport.VirtualHeight + 10f
            );
            Vector2 v = new(
                (float)(Random.Shared.NextDouble() - 0.5) * 40f,
                -(float)(Random.Shared.NextDouble() * 80f + 60f)
            );
            _manager.Particles.EmitDashTrail(p, v, ColorPalette.PlayerBlue * 0.4f);
        }

        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();

        // 按钮点击响应
        if (_btnStart.Contains(mousePos) && click)
        {
            _manager.ChangeScene(new CharacterSelectScene(_manager, _exitGameAction));
        }
        else if (_btnToggleControls.Contains(mousePos) && click)
        {
            _manager.Input.ShowVirtualControls = !_manager.Input.ShowVirtualControls;
        }
        else if (_btnExit.Contains(mousePos) && click)
        {
            _exitGameAction();
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;

        // 1. 渲染网格与背景光环
        for (int x = 0; x < VirtualViewport.VirtualWidth; x += 100)
        {
            batcher.DrawLine(spriteBatch, new Vector2(x, 0), new Vector2(x, VirtualViewport.VirtualHeight), ColorPalette.GridLine, 1f);
        }
        for (int y = 0; y < VirtualViewport.VirtualHeight; y += 100)
        {
            batcher.DrawLine(spriteBatch, new Vector2(0, y), new Vector2(VirtualViewport.VirtualWidth, y), ColorPalette.GridLine, 1f);
        }

        Vector2 centerRing = new(VirtualViewport.VirtualWidth * 0.5f, 300f);
        float pulse = MathF.Sin(_time * 1.5f) * 15f;
        batcher.DrawCircleOutline(spriteBatch, centerRing, 170f + pulse, ColorPalette.PlayerBlue * 0.25f, 3f);
        batcher.DrawCircleOutline(spriteBatch, centerRing, 220f - pulse, ColorPalette.PlayerRed * 0.2f, 2f);

        _manager.Particles.Draw(batcher, spriteBatch);

        // 2. 简洁主标题
        font.DrawText(spriteBatch, "ROUNDS", new Vector2(VirtualViewport.VirtualWidth * 0.5f, 300f), Color.White, 2.6f, new Vector2(0.5f, 0.5f));

        // 3. 按钮列表
        DrawButton(batcher, spriteBatch, font, _btnStart, "开始游戏", mousePos, ColorPalette.PlayerBlue);
        string toggleText = _manager.Input.ShowVirtualControls ? "虚拟摇杆: 开" : "虚拟摇杆: 关";
        DrawButton(batcher, spriteBatch, font, _btnToggleControls, toggleText, mousePos, ColorPalette.TextGray);
        DrawButton(batcher, spriteBatch, font, _btnExit, "退出游戏", mousePos, ColorPalette.PlayerRed * 0.8f);
    }

    private void DrawButton(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Rectangle rect, string text, Vector2 mousePos, Color accent)
    {
        bool hover = rect.Contains(mousePos);
        Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
        Color border = hover ? accent : ColorPalette.WallOutline;

        batcher.DrawRoundedRect(spriteBatch, rect, 8f, bg);
        batcher.DrawRectangleOutline(spriteBatch, rect, border, hover ? 3f : 2f);

        if (hover)
        {
            batcher.DrawRectangle(spriteBatch, new Rectangle(rect.X, rect.Y, 6, rect.Height), accent);
        }

        font.DrawText(spriteBatch, text, new Vector2(rect.Center.X, rect.Center.Y), hover ? Color.White : ColorPalette.TextWhite, 0.85f, new Vector2(0.5f, 0.5f));
    }

    public void Exit() { }
}
