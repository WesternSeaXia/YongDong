using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 游戏设置场景。
/// 提供虚拟触控显示开关、左摇杆上推跳跃开关、左摇杆控制瞄准（取消右摇杆改为开火/技能/跳跃键）及摇杆中心死区比例调节。
/// </summary>
public class SettingsScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitGameAction;

    private readonly float[] _deadzonePresets = { 0.12f, 0.18f, 0.25f, 0.32f };

    private Rectangle _rowVirtualControls;
    private Rectangle _rowLeftStickJump;
    private Rectangle _rowLeftStickAim;
    private Rectangle _rowDeadzone;
    private Rectangle _btnBack;

    public SettingsScene(SceneManager manager, Action exitGameAction)
    {
        _manager = manager;
        _exitGameAction = exitGameAction;

        bool mobile = VirtualViewport.IsMobile;
        int cx = VirtualViewport.VirtualWidth / 2;

        int rowW = mobile ? 960 : 680;
        int rowH = mobile ? 105 : 74;
        int gap = mobile ? 28 : 22;
        int startY = mobile ? 230 : 270;

        _rowVirtualControls = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 0, rowW, rowH);
        _rowLeftStickJump = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 1, rowW, rowH);
        _rowLeftStickAim = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 2, rowW, rowH);
        _rowDeadzone = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 3, rowW, rowH);

        int backW = mobile ? 460 : 320;
        int backH = mobile ? 100 : 68;
        int backY = mobile ? 840 : 790;
        _btnBack = new Rectangle(cx - backW / 2, backY, backW, backH);
    }

    public void Enter()
    {
    }

    public void Update(float dt)
    {
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();
        var input = _manager.Input;

        if (_rowVirtualControls.Contains(mousePos) && click)
        {
            input.ShowVirtualControls = !input.ShowVirtualControls;
        }
        else if (_rowLeftStickJump.Contains(mousePos) && click)
        {
            input.LeftStickJumpEnabled = !input.LeftStickJumpEnabled;
        }
        else if (_rowLeftStickAim.Contains(mousePos) && click)
        {
            input.LeftStickControlsAim = !input.LeftStickControlsAim;
        }
        else if (_rowDeadzone.Contains(mousePos) && click)
        {
            int nextIndex = 0;
            for (int i = 0; i < _deadzonePresets.Length; i++)
            {
                if (MathF.Abs(input.StickDeadzoneRatio - _deadzonePresets[i]) < 0.02f)
                {
                    nextIndex = (i + 1) % _deadzonePresets.Length;
                    break;
                }
            }
            input.StickDeadzoneRatio = _deadzonePresets[nextIndex];
        }
        else if ((_btnBack.Contains(mousePos) && click) || input.IsKeyJustPressed(Keys.Escape))
        {
            _manager.ChangeScene(new MainMenuScene(_manager, _exitGameAction));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        var input = _manager.Input;
        Vector2 mousePos = input.VirtualMousePosition;
        bool mobile = VirtualViewport.IsMobile;

        // 1. 背景网格
        for (int x = 0; x < VirtualViewport.VirtualWidth; x += 100)
        {
            batcher.DrawLine(spriteBatch, new Vector2(x, 0), new Vector2(x, VirtualViewport.VirtualHeight), ColorPalette.GridLine, 1f);
        }
        for (int y = 0; y < VirtualViewport.VirtualHeight; y += 100)
        {
            batcher.DrawLine(spriteBatch, new Vector2(0, y), new Vector2(VirtualViewport.VirtualWidth, y), ColorPalette.GridLine, 1f);
        }

        // 2. 顶部标题
        font.DrawText(spriteBatch, "游戏设置", new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 115f : 145f), Color.White, mobile ? 2.5f : 2.0f, new Vector2(0.5f, 0.5f));

        // 3. 设置项列表
        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowVirtualControls,
            "虚拟触控按键",
            input.ShowVirtualControls ? "开启" : "关闭",
            input.ShowVirtualControls,
            mousePos,
            ColorPalette.PlayerBlue
        );

        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowLeftStickJump,
            "左摇杆上推跳跃",
            input.LeftStickJumpEnabled ? "开启" : "关闭",
            input.LeftStickJumpEnabled,
            mousePos,
            ColorPalette.PlayerGreen
        );

        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowLeftStickAim,
            "左摇杆控制瞄准",
            input.LeftStickControlsAim ? "开启 (右侧按键)" : "关闭 (双摇杆)",
            input.LeftStickControlsAim,
            mousePos,
            ColorPalette.PlayerRed
        );

        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowDeadzone,
            "摇杆中心死区",
            $"{(int)MathF.Round(input.StickDeadzoneRatio * 100f)}%",
            true,
            mousePos,
            ColorPalette.PlayerYellow
        );

        // 4. 返回主菜单按钮
        bool backHover = _btnBack.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnBack, mobile ? 12f : 8f, backHover ? ColorPalette.PlayerBlue : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnBack, Color.White, backHover ? 3.5f : 2.5f);
        font.DrawText(spriteBatch, "返回", new Vector2(_btnBack.Center.X, _btnBack.Center.Y), Color.White, mobile ? 1.4f : 1.0f, new Vector2(0.5f, 0.5f));
    }

    private void DrawSettingRow(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        ProceduralFont font,
        Rectangle rect,
        string label,
        string valueText,
        bool isActive,
        Vector2 mousePos,
        Color accentColor)
    {
        bool mobile = VirtualViewport.IsMobile;
        bool hover = rect.Contains(mousePos);

        Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
        Color border = isActive ? accentColor : (hover ? Color.White : ColorPalette.WallOutline);

        batcher.DrawRoundedRect(spriteBatch, rect, mobile ? 12f : 8f, bg);
        batcher.DrawRectangleOutline(spriteBatch, rect, border, isActive || hover ? 3.5f : 2f);

        // 左侧色标条
        batcher.DrawRectangle(spriteBatch, new Rectangle(rect.X, rect.Y, 10, rect.Height), isActive ? accentColor : ColorPalette.WallOutline);

        float textScale = mobile ? 1.32f : 0.92f;
        font.DrawText(spriteBatch, label, new Vector2(rect.X + (mobile ? 42 : 32), rect.Center.Y), ColorPalette.TextWhite, textScale, new Vector2(0f, 0.5f));

        Color valColor = isActive ? accentColor : ColorPalette.TextGray;
        font.DrawText(spriteBatch, valueText, new Vector2(rect.Right - (mobile ? 42 : 32), rect.Center.Y), valColor, textScale, new Vector2(1f, 0.5f));
    }

    public void Exit()
    {
    }
}
