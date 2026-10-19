using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 游戏设置场景。
/// 职责边界：提供虚拟触控显示开关、左摇杆上推跳跃开关、左摇杆控制瞄准、摇杆中心死区比例调节以及动态平滑跟踪摄像机开关；
/// 所有配置项通过 <see cref="GameSettings"/> 实现跨端磁盘持久化保存。
/// </summary>
public class SettingsScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitGameAction;

    private readonly float[] _deadzonePresets = { 0.12f, 0.18f, 0.25f, 0.32f };
    private readonly MusicSelectorModal _musicModal = new();

    private Rectangle _rowKeybindPreset;
    private Rectangle _rowAiDifficulty;
    private Rectangle _rowVirtualControls;
    private Rectangle _rowLeftStickJump;
    private Rectangle _rowLeftStickAim;
    private Rectangle _rowDeadzone;
    private Rectangle _rowDynamicCamera;
    private Rectangle _rowMasterVolume, _btnMasterMinus, _barMasterVolume, _btnMasterPlus;
    private Rectangle _rowBgmVolume, _btnBgmMinus, _barBgmVolume, _btnBgmPlus;
    private Rectangle _rowSfxVolume, _btnSfxMinus, _barSfxVolume, _btnSfxPlus;
    private Rectangle _rowBgmSelect;
    private Rectangle _btnBgmPrev;
    private Rectangle _btnBgmNext;
    private Rectangle _rowPlayMode;
    private Rectangle _btnBack;
    private int _lastHoveredRow = -1;

    public SettingsScene(SceneManager manager, Action exitGameAction)
    {
        _manager = manager;
        _exitGameAction = exitGameAction;

        bool mobile = VirtualViewport.IsMobile;
        int cx = VirtualViewport.VirtualWidth / 2;

        int rowW = mobile ? 1040 : 840;
        int rowH = mobile ? 56 : 42;
        int gap = mobile ? 8 : 6;
        int startY = mobile ? 105 : 105;

        _rowKeybindPreset = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 0, rowW, rowH);
        _rowAiDifficulty = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 1, rowW, rowH);
        _rowVirtualControls = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 2, rowW, rowH);
        _rowLeftStickJump = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 3, rowW, rowH);
        _rowLeftStickAim = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 4, rowW, rowH);
        _rowDeadzone = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 5, rowW, rowH);
        _rowDynamicCamera = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 6, rowW, rowH);

        _rowMasterVolume = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 7, rowW, rowH);
        _rowBgmVolume = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 8, rowW, rowH);
        _rowSfxVolume = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 9, rowW, rowH);

        _rowBgmSelect = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 10, rowW, rowH);
        _rowPlayMode = new Rectangle(cx - rowW / 2, startY + (rowH + gap) * 11, rowW, rowH);

        int volBtnW = mobile ? 48 : 34;
        int volBtnH = mobile ? 40 : 28;
        int barW = mobile ? 260 : 200;
        int barH = mobile ? 26 : 20;
        int volMargin = mobile ? 36 : 24;

        void SetupVolumeControls(Rectangle row, out Rectangle minus, out Rectangle bar, out Rectangle plus)
        {
            int pX = row.Right - volMargin - volBtnW;
            int bX = pX - 10 - barW;
            int mX = bX - 10 - volBtnW;
            int bY = row.Center.Y - barH / 2;
            int btnY = row.Center.Y - volBtnH / 2;

            minus = new Rectangle(mX, btnY, volBtnW, volBtnH);
            bar = new Rectangle(bX, bY, barW, barH);
            plus = new Rectangle(pX, btnY, volBtnW, volBtnH);
        }

        SetupVolumeControls(_rowMasterVolume, out _btnMasterMinus, out _barMasterVolume, out _btnMasterPlus);
        SetupVolumeControls(_rowBgmVolume, out _btnBgmMinus, out _barBgmVolume, out _btnBgmPlus);
        SetupVolumeControls(_rowSfxVolume, out _btnSfxMinus, out _barSfxVolume, out _btnSfxPlus);

        int btnStepW = mobile ? 52 : 34;
        int btnStepH = mobile ? 52 : 34;
        int btnStepY = _rowBgmSelect.Center.Y - btnStepH / 2;
        int stepRightMargin = mobile ? 36 : 24;
        _btnBgmNext = new Rectangle(_rowBgmSelect.Right - stepRightMargin - btnStepW, btnStepY, btnStepW, btnStepH);
        int middleTitleW = mobile ? 460 : 360;
        _btnBgmPrev = new Rectangle(_btnBgmNext.X - middleTitleW - btnStepW, btnStepY, btnStepW, btnStepH);

        int backW = mobile ? 460 : 320;
        int backH = mobile ? 72 : 50;
        int backY = mobile ? 875 : 695;
        _btnBack = new Rectangle(cx - backW / 2, backY, backW, backH);
    }

    /// <inheritdoc />
    public void Enter()
    {
        // 同步当前持久化配置到输入管理器
        var settings = GameSettings.Current;
        var input = _manager.Input;
        input.ShowVirtualControls = settings.ShowVirtualControls;
        input.LeftStickJumpEnabled = settings.LeftStickJumpEnabled;
        input.LeftStickControlsAim = settings.LeftStickControlsAim;
        input.StickDeadzoneRatio = settings.StickDeadzoneRatio;
    }

    /// <inheritdoc />
    public void Update(float dt)
    {
        var input = _manager.Input;
        var audio = _manager.Audio;

        // 若曲库弹窗开启，优先拦截弹窗输入
        if (_musicModal.IsOpen)
        {
            _musicModal.Update(input, audio);
            return;
        }

        Vector2 mousePos = input.VirtualMousePosition;
        bool click = input.IsLeftClickJustPressed();
        var settings = GameSettings.Current;

        int hoveredRow = -1;
        if (_rowKeybindPreset.Contains(mousePos)) hoveredRow = 0;
        else if (_rowAiDifficulty.Contains(mousePos)) hoveredRow = 1;
        else if (_rowVirtualControls.Contains(mousePos)) hoveredRow = 2;
        else if (_rowLeftStickJump.Contains(mousePos)) hoveredRow = 3;
        else if (_rowLeftStickAim.Contains(mousePos)) hoveredRow = 4;
        else if (_rowDeadzone.Contains(mousePos)) hoveredRow = 5;
        else if (_rowDynamicCamera.Contains(mousePos)) hoveredRow = 6;
        else if (_btnMasterMinus.Contains(mousePos) || _btnMasterPlus.Contains(mousePos) || _barMasterVolume.Contains(mousePos) || _rowMasterVolume.Contains(mousePos)) hoveredRow = 7;
        else if (_btnBgmMinus.Contains(mousePos) || _btnBgmPlus.Contains(mousePos) || _barBgmVolume.Contains(mousePos) || _rowBgmVolume.Contains(mousePos)) hoveredRow = 8;
        else if (_btnSfxMinus.Contains(mousePos) || _btnSfxPlus.Contains(mousePos) || _barSfxVolume.Contains(mousePos) || _rowSfxVolume.Contains(mousePos)) hoveredRow = 9;
        else if (_btnBgmPrev.Contains(mousePos)) hoveredRow = 10;
        else if (_btnBgmNext.Contains(mousePos)) hoveredRow = 11;
        else if (_rowBgmSelect.Contains(mousePos)) hoveredRow = 12;
        else if (_rowPlayMode.Contains(mousePos)) hoveredRow = 13;
        else if (_btnBack.Contains(mousePos)) hoveredRow = 14;

        if (hoveredRow != -1 && hoveredRow != _lastHoveredRow)
        {
            audio.PlayUiHover();
        }
        _lastHoveredRow = hoveredRow;

        if (_rowKeybindPreset.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            settings.KeybindPreset = (settings.KeybindPreset + 1) % 3;
            GameSettings.Save();
        }
        else if (_rowAiDifficulty.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            settings.AiDifficulty = (settings.AiDifficulty + 1) % 3;
            GameSettings.Save();
        }
        else if (_rowVirtualControls.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            input.ShowVirtualControls = !input.ShowVirtualControls;
            settings.ShowVirtualControls = input.ShowVirtualControls;
            GameSettings.Save();
        }
        else if (_rowLeftStickJump.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            input.LeftStickJumpEnabled = !input.LeftStickJumpEnabled;
            settings.LeftStickJumpEnabled = input.LeftStickJumpEnabled;
            GameSettings.Save();
        }
        else if (_rowLeftStickAim.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            input.LeftStickControlsAim = !input.LeftStickControlsAim;
            settings.LeftStickControlsAim = input.LeftStickControlsAim;
            GameSettings.Save();
        }
        else if (_rowDeadzone.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
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
            settings.StickDeadzoneRatio = input.StickDeadzoneRatio;
            GameSettings.Save();
        }
        else if (_rowDynamicCamera.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            settings.DynamicCameraEnabled = !settings.DynamicCameraEnabled;
            GameSettings.Save();
        }
        // 主音量调节
        else if (_btnMasterMinus.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.MasterVolume = MathF.Round(MathF.Max(0f, audio.MasterVolume - 0.1f), 2);
            GameSettings.Save();
        }
        else if (_btnMasterPlus.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.MasterVolume = MathF.Round(MathF.Min(1f, audio.MasterVolume + 0.1f), 2);
            GameSettings.Save();
        }
        else if (_barMasterVolume.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            float t = MathHelper.Clamp((mousePos.X - _barMasterVolume.X) / _barMasterVolume.Width, 0f, 1f);
            audio.MasterVolume = MathF.Round(t, 2);
            GameSettings.Save();
        }
        // 背景音乐音量调节
        else if (_btnBgmMinus.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.BgmVolume = MathF.Round(MathF.Max(0f, audio.BgmVolume - 0.1f), 2);
            GameSettings.Save();
        }
        else if (_btnBgmPlus.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.BgmVolume = MathF.Round(MathF.Min(1f, audio.BgmVolume + 0.1f), 2);
            GameSettings.Save();
        }
        else if (_barBgmVolume.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            float t = MathHelper.Clamp((mousePos.X - _barBgmVolume.X) / _barBgmVolume.Width, 0f, 1f);
            audio.BgmVolume = MathF.Round(t, 2);
            GameSettings.Save();
        }
        // 战斗音效音量调节
        else if (_btnSfxMinus.Contains(mousePos) && click)
        {
            audio.SfxVolume = MathF.Round(MathF.Max(0f, audio.SfxVolume - 0.1f), 2);
            audio.PlayShoot();
            GameSettings.Save();
        }
        else if (_btnSfxPlus.Contains(mousePos) && click)
        {
            audio.SfxVolume = MathF.Round(MathF.Min(1f, audio.SfxVolume + 0.1f), 2);
            audio.PlayShoot();
            GameSettings.Save();
        }
        else if (_barSfxVolume.Contains(mousePos) && click)
        {
            float t = MathHelper.Clamp((mousePos.X - _barSfxVolume.X) / _barSfxVolume.Width, 0f, 1f);
            audio.SfxVolume = MathF.Round(t, 2);
            audio.PlayShoot();
            GameSettings.Save();
        }
        // 背景音乐选曲与播放模式
        else if (_btnBgmPrev.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.PlayPrevBgm();
        }
        else if (_btnBgmNext.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.PlayNextBgm(userTriggered: true);
        }
        else if (_rowBgmSelect.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            _musicModal.Open();
        }
        else if (_rowPlayMode.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.CyclePlayMode();
        }
        else if ((_btnBack.Contains(mousePos) && click) || input.IsKeyJustPressed(Keys.Escape))
        {
            audio.PlayUiClick();
            GameSettings.Save();
            _manager.ChangeScene(new MainMenuScene(_manager, _exitGameAction));
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        var input = _manager.Input;
        var settings = GameSettings.Current;
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
        font.DrawText(spriteBatch, "游戏设置", new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 65f : 65f), Color.White, mobile ? 2.3f : 1.8f, new Vector2(0.5f, 0.5f));

        // 3. 设置项列表
        // 键盘改键预设方案
        string keybindText = settings.KeybindPreset switch
        {
            0 => "WASD 移动 + 空格跳跃",
            1 => "方向键移动 + 上键跳跃",
            2 => "WASD 移动 + W键跳跃",
            _ => "WASD 移动 + 空格跳跃"
        };
        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowKeybindPreset,
            "键盘控制方案",
            keybindText,
            true,
            mousePos,
            ColorPalette.PlayerYellow
        );

        // AI 难度分层
        string aiDiffText = settings.AiDifficulty switch
        {
            0 => "简单 (新手友好 / 射击迟疑)",
            1 => "普通 (战术特工 / 均衡预判)",
            2 => "地狱 (绝壁弹反 / 极限超前预判)",
            _ => "普通 (战术特工)"
        };
        Color aiDiffColor = settings.AiDifficulty switch
        {
            0 => ColorPalette.PlayerGreen,
            1 => ColorPalette.PlayerBlue,
            _ => ColorPalette.PlayerRed
        };
        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowAiDifficulty,
            "AI 对战难度",
            aiDiffText,
            true,
            mousePos,
            aiDiffColor
        );

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

        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowDynamicCamera,
            "动态平滑摄像机",
            settings.DynamicCameraEnabled ? "开启" : "关闭",
            settings.DynamicCameraEnabled,
            mousePos,
            ColorPalette.DashTrail
        );

        // 音量调节项
        DrawVolumeRow(
            batcher, spriteBatch, font,
            _rowMasterVolume,
            _btnMasterMinus, _barMasterVolume, _btnMasterPlus,
            "全局主音量",
            _manager.Audio.MasterVolume,
            mousePos,
            ColorPalette.PlayerBlue
        );

        DrawVolumeRow(
            batcher, spriteBatch, font,
            _rowBgmVolume,
            _btnBgmMinus, _barBgmVolume, _btnBgmPlus,
            "背景音乐音量 (BGM)",
            _manager.Audio.BgmVolume,
            mousePos,
            ColorPalette.PlayerYellow
        );

        DrawVolumeRow(
            batcher, spriteBatch, font,
            _rowSfxVolume,
            _btnSfxMinus, _barSfxVolume, _btnSfxPlus,
            "战斗音效音量 (SFX)",
            _manager.Audio.SfxVolume,
            mousePos,
            ColorPalette.PlayerGreen
        );

        // 背景音乐选曲行
        DrawBgmSelectRow(batcher, spriteBatch, font, _rowBgmSelect, _btnBgmPrev, _btnBgmNext, _manager.Audio, mousePos);

        // 播放模式切换行
        DrawSettingRow(
            batcher, spriteBatch, font,
            _rowPlayMode,
            "音乐播放模式",
            _manager.Audio.GetPlayModeName(),
            true,
            mousePos,
            ColorPalette.PlayerGreen
        );

        // 4. 返回主菜单按钮
        bool backHover = _btnBack.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnBack, mobile ? 12f : 8f, backHover ? ColorPalette.PlayerBlue : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnBack, Color.White, backHover ? 3.5f : 2.5f);
        font.DrawText(spriteBatch, "返回", new Vector2(_btnBack.Center.X, _btnBack.Center.Y), Color.White, mobile ? 1.4f : 1.0f, new Vector2(0.5f, 0.5f));

        // 5. 若曲库弹窗开启，在最顶层绘制
        if (_musicModal.IsOpen)
        {
            _musicModal.Draw(batcher, spriteBatch, font, input, _manager.Audio);
        }
    }

    private static void DrawBgmSelectRow(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        ProceduralFont font,
        Rectangle rowRect,
        Rectangle btnPrev,
        Rectangle btnNext,
        Audio.AudioManager audio,
        Vector2 mousePos)
    {
        bool mobile = VirtualViewport.IsMobile;
        bool hoverRow = rowRect.Contains(mousePos);

        batcher.DrawRoundedRect(spriteBatch, rowRect, mobile ? 12f : 8f, hoverRow ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, rowRect, hoverRow ? Color.White : ColorPalette.PlayerYellow, hoverRow ? 3.5f : 2f);

        // 左侧色标条
        batcher.DrawRectangle(spriteBatch, new Rectangle(rowRect.X, rowRect.Y, 10, rowRect.Height), ColorPalette.PlayerYellow);

        float textScale = mobile ? 1.25f : 0.90f;
        font.DrawText(spriteBatch, "背景音乐选曲", new Vector2(rowRect.X + (mobile ? 42 : 32), rowRect.Center.Y), ColorPalette.TextWhite, textScale, new Vector2(0f, 0.5f));

        // 上一首按键
        bool prevHover = btnPrev.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, btnPrev, 6f, prevHover ? ColorPalette.ButtonHover : Color.Black * 0.45f);
        batcher.DrawRectangleOutline(spriteBatch, btnPrev, prevHover ? Color.White : ColorPalette.WallOutline, prevHover ? 2.5f : 1.5f);
        font.DrawText(spriteBatch, "<", new Vector2(btnPrev.Center.X, btnPrev.Center.Y), Color.White, mobile ? 1.15f : 0.90f, new Vector2(0.5f, 0.5f));

        // 中间曲目名称（点击可展开全部曲库）
        int midX = (btnPrev.Right + btnNext.X) / 2;
        string titleStr = $"{audio.CurrentBgmTitle} (点此选歌)";
        font.DrawText(spriteBatch, titleStr, new Vector2(midX, rowRect.Center.Y), ColorPalette.PlayerYellow, mobile ? 0.78f : 0.65f, new Vector2(0.5f, 0.5f));

        // 下一首按键
        bool nextHover = btnNext.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, btnNext, 6f, nextHover ? ColorPalette.ButtonHover : Color.Black * 0.45f);
        batcher.DrawRectangleOutline(spriteBatch, btnNext, nextHover ? Color.White : ColorPalette.WallOutline, nextHover ? 2.5f : 1.5f);
        font.DrawText(spriteBatch, ">", new Vector2(btnNext.Center.X, btnNext.Center.Y), Color.White, mobile ? 1.15f : 0.90f, new Vector2(0.5f, 0.5f));
    }

    private static void DrawVolumeRow(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        ProceduralFont font,
        Rectangle rowRect,
        Rectangle btnMinus,
        Rectangle barRect,
        Rectangle btnPlus,
        string label,
        float volume,
        Vector2 mousePos,
        Color accentColor)
    {
        bool mobile = VirtualViewport.IsMobile;
        bool hoverRow = rowRect.Contains(mousePos);

        batcher.DrawRoundedRect(spriteBatch, rowRect, mobile ? 12f : 8f, hoverRow ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, rowRect, hoverRow ? Color.White : accentColor, hoverRow ? 3.5f : 2f);

        // 左侧色标条
        batcher.DrawRectangle(spriteBatch, new Rectangle(rowRect.X, rowRect.Y, 10, rowRect.Height), accentColor);

        float textScale = mobile ? 1.25f : 0.90f;
        font.DrawText(spriteBatch, label, new Vector2(rowRect.X + (mobile ? 42 : 32), rowRect.Center.Y), ColorPalette.TextWhite, textScale, new Vector2(0f, 0.5f));

        // 减号按键 [-]
        bool minusHover = btnMinus.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, btnMinus, 6f, minusHover ? ColorPalette.ButtonHover : Color.Black * 0.45f);
        batcher.DrawRectangleOutline(spriteBatch, btnMinus, minusHover ? Color.White : ColorPalette.WallOutline, minusHover ? 2.5f : 1.5f);
        font.DrawText(spriteBatch, "-", new Vector2(btnMinus.Center.X, btnMinus.Center.Y), Color.White, mobile ? 1.2f : 0.95f, new Vector2(0.5f, 0.5f));

        // 音量填充槽
        bool barHover = barRect.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, barRect, 4f, Color.Black * 0.55f);
        int fillW = (int)(barRect.Width * volume);
        if (fillW > 0)
        {
            Rectangle fillRect = new(barRect.X, barRect.Y, fillW, barRect.Height);
            batcher.DrawRoundedRect(spriteBatch, fillRect, 4f, accentColor * 0.85f);
        }
        batcher.DrawRectangleOutline(spriteBatch, barRect, barHover ? Color.White : accentColor, barHover ? 2.5f : 1.5f);

        string pctText = $"{(int)MathF.Round(volume * 100f)}%";
        font.DrawText(spriteBatch, pctText, new Vector2(barRect.Center.X, barRect.Center.Y), Color.White, mobile ? 0.78f : 0.65f, new Vector2(0.5f, 0.5f));

        // 加号按键 [+]
        bool plusHover = btnPlus.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, btnPlus, 6f, plusHover ? ColorPalette.ButtonHover : Color.Black * 0.45f);
        batcher.DrawRectangleOutline(spriteBatch, btnPlus, plusHover ? Color.White : ColorPalette.WallOutline, plusHover ? 2.5f : 1.5f);
        font.DrawText(spriteBatch, "+", new Vector2(btnPlus.Center.X, btnPlus.Center.Y), Color.White, mobile ? 1.2f : 0.95f, new Vector2(0.5f, 0.5f));
    }

    private static void DrawSettingRow(
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

        float textScale = mobile ? 1.25f : 0.90f;
        font.DrawText(spriteBatch, label, new Vector2(rect.X + (mobile ? 42 : 32), rect.Center.Y), ColorPalette.TextWhite, textScale, new Vector2(0f, 0.5f));

        Color valColor = isActive ? accentColor : ColorPalette.TextGray;
        font.DrawText(spriteBatch, valueText, new Vector2(rect.Right - (mobile ? 42 : 32), rect.Center.Y), valColor, textScale, new Vector2(1f, 0.5f));
    }

    /// <inheritdoc />
    public void Exit()
    {
        GameSettings.Save();
    }
}
