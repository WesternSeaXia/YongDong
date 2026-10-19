using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.Audio;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Input;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 跨场景共享的全量 17 首背景音乐选取与试听弹窗 (Music Selector Modal)。
/// 提供双列曲目直观排版、正在播放曲目高亮、即点即播试听、切歌及循环模式（顺序/随机/单曲）切换。
/// </summary>
public class MusicSelectorModal
{
    public bool IsOpen { get; private set; } = false;

    private Rectangle _cardRect;
    private Rectangle[] _trackRects = Array.Empty<Rectangle>();
    private Rectangle _btnPrev;
    private Rectangle _btnNext;
    private Rectangle _btnMode;
    private Rectangle _btnClose;
    private int _lastHoveredIndex = -1;

    public void Open()
    {
        IsOpen = true;
        _lastHoveredIndex = -1;
        RecalculateLayout();
    }

    public void Close()
    {
        IsOpen = false;
        _lastHoveredIndex = -1;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void RecalculateLayout()
    {
        bool mobile = VirtualViewport.IsMobile;
        int cx = VirtualViewport.VirtualWidth / 2;
        int cy = VirtualViewport.VirtualHeight / 2;

        int cardW = mobile ? 1480 : 1240;
        int cardH = mobile ? 890 : 750;
        _cardRect = new Rectangle(cx - cardW / 2, cy - cardH / 2, cardW, cardH);

        int colPad = mobile ? 46 : 36;
        int colGap = mobile ? 24 : 18;
        int colW = (_cardRect.Width - colPad * 2 - colGap) / 2;

        int itemH = mobile ? 58 : 46;
        int itemGap = mobile ? 12 : 9;
        int listStartY = _cardRect.Y + (mobile ? 128 : 110);

        const int totalTracks = 17;
        _trackRects = new Rectangle[totalTracks];

        // 左列 9 首 (0 ~ 8)
        int col0X = _cardRect.X + colPad;
        for (int i = 0; i < 9; i++)
        {
            int iy = listStartY + i * (itemH + itemGap);
            _trackRects[i] = new Rectangle(col0X, iy, colW, itemH);
        }

        // 右列 8 首 (9 ~ 16)
        int col1X = col0X + colW + colGap;
        for (int i = 9; i < totalTracks; i++)
        {
            int iy = listStartY + (i - 9) * (itemH + itemGap);
            _trackRects[i] = new Rectangle(col1X, iy, colW, itemH);
        }

        // 底部工具栏按键
        int btnH = mobile ? 58 : 44;
        int btnY = _cardRect.Bottom - (mobile ? 82 : 68);

        int prevW = mobile ? 180 : 140;
        int nextW = mobile ? 180 : 140;
        int modeW = mobile ? 300 : 240;
        int closeW = mobile ? 220 : 180;
        int spacing = mobile ? 20 : 16;

        int totalBtnW = prevW + nextW + modeW + closeW + spacing * 3;
        int startBtnX = cx - totalBtnW / 2;

        _btnPrev = new Rectangle(startBtnX, btnY, prevW, btnH);
        _btnNext = new Rectangle(_btnPrev.Right + spacing, btnY, nextW, btnH);
        _btnMode = new Rectangle(_btnNext.Right + spacing, btnY, modeW, btnH);
        _btnClose = new Rectangle(_btnMode.Right + spacing, btnY, closeW, btnH);
    }

    /// <summary>
    /// 处理音乐选取弹窗输入交互。
    /// </summary>
    /// <returns>若输入被弹窗消费（处于开启状态或刚被点击），返回 true</returns>
    public bool Update(InputManager input, AudioManager audio)
    {
        if (!IsOpen) return false;

        if (input.IsKeyJustPressed(Keys.Escape))
        {
            Close();
            return true;
        }

        Vector2 mousePos = input.VirtualMousePosition;
        bool click = input.IsLeftClickJustPressed();

        int hovered = -1;
        for (int i = 0; i < _trackRects.Length; i++)
        {
            if (_trackRects[i].Contains(mousePos))
            {
                hovered = i;
                break;
            }
        }
        if (hovered == -1)
        {
            if (_btnPrev.Contains(mousePos)) hovered = 1001;
            else if (_btnNext.Contains(mousePos)) hovered = 1002;
            else if (_btnMode.Contains(mousePos)) hovered = 1003;
            else if (_btnClose.Contains(mousePos)) hovered = 1004;
        }

        if (hovered != -1 && hovered != _lastHoveredIndex)
        {
            audio.PlayUiHover();
        }
        _lastHoveredIndex = hovered;

        // 检查曲目列表项点击
        for (int i = 0; i < _trackRects.Length; i++)
        {
            if (_trackRects[i].Contains(mousePos) && click)
            {
                audio.PlayUiClick();
                audio.PlayTrack(i);
                return true;
            }
        }

        // 底部按键点击
        if (_btnPrev.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.PlayPrevBgm();
            return true;
        }
        if (_btnNext.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.PlayNextBgm(userTriggered: true);
            return true;
        }
        if (_btnMode.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            audio.CyclePlayMode();
            return true;
        }
        if (_btnClose.Contains(mousePos) && click)
        {
            audio.PlayUiClick();
            Close();
            return true;
        }

        // 点击弹窗卡片外侧任意深色区域关闭
        if (click && !_cardRect.Contains(mousePos))
        {
            audio.PlayUiClick();
            Close();
            return true;
        }

        return true;
    }

    /// <summary>
    /// 渲染音乐选取弹窗。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, InputManager input, AudioManager audio)
    {
        if (!IsOpen) return;

        bool mobile = VirtualViewport.IsMobile;
        int cx = VirtualViewport.VirtualWidth / 2;
        Vector2 mousePos = input.VirtualMousePosition;

        // 1. 全屏半透明遮罩
        batcher.DrawRectangle(spriteBatch, new Rectangle(0, 0, VirtualViewport.VirtualWidth, VirtualViewport.VirtualHeight), Color.Black * 0.82f);

        // 2. 弹窗主背景
        batcher.DrawRoundedRect(spriteBatch, _cardRect, 16f, ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, _cardRect, ColorPalette.PlayerBlue, 3.5f);

        // 3. 标题与副标题
        float titleScale = mobile ? 1.55f : 1.25f;
        float subScale = mobile ? 0.85f : 0.68f;
        font.DrawText(spriteBatch, "背景音乐曲库选取 (17首)", new Vector2(cx, _cardRect.Y + (mobile ? 42 : 34)), Color.White, titleScale, new Vector2(0.5f, 0.5f));

        string subText = $"正在播放: {audio.CurrentBgmTitle}  |  模式: {audio.GetPlayModeName()}";
        font.DrawText(spriteBatch, subText, new Vector2(cx, _cardRect.Y + (mobile ? 82 : 68)), ColorPalette.PlayerYellow, subScale, new Vector2(0.5f, 0.5f));

        // 分割线
        int linePad = mobile ? 46 : 36;
        int lineY = _cardRect.Y + (mobile ? 112 : 94);
        batcher.DrawLine(spriteBatch, new Vector2(_cardRect.X + linePad, lineY), new Vector2(_cardRect.Right - linePad, lineY), ColorPalette.WallOutline, 1.5f);

        // 4. 双列渲染 17 首曲目按钮
        float itemTextScale = mobile ? 0.82f : 0.68f;
        for (int i = 0; i < _trackRects.Length; i++)
        {
            Rectangle rect = _trackRects[i];
            bool isCurrent = (i == audio.CurrentBgmIndex);
            bool hover = rect.Contains(mousePos);

            Color bg;
            Color border;
            if (isCurrent)
            {
                bg = hover ? ColorPalette.PlayerBlue * 0.45f : ColorPalette.PlayerBlue * 0.25f;
                border = ColorPalette.PlayerYellow;
            }
            else
            {
                bg = hover ? ColorPalette.ButtonHover : Color.Black * 0.40f;
                border = hover ? Color.White : ColorPalette.WallOutline;
            }

            batcher.DrawRoundedRect(spriteBatch, rect, mobile ? 8f : 6f, bg);
            batcher.DrawRectangleOutline(spriteBatch, rect, border, isCurrent ? 2.8f : (hover ? 2f : 1.2f));

            // 左侧状态指示条
            if (isCurrent)
            {
                batcher.DrawRectangle(spriteBatch, new Rectangle(rect.X, rect.Y, 6, rect.Height), ColorPalette.PlayerYellow);
            }

            // 曲目名称文本
            string trackName = audio.GetTrackName(i);
            string fullItemLabel = $"{i + 1:D2}. {trackName}";
            Color textColor = isCurrent ? ColorPalette.PlayerYellow : (hover ? Color.White : ColorPalette.TextWhite);
            font.DrawText(spriteBatch, fullItemLabel, new Vector2(rect.X + (mobile ? 20 : 16), rect.Center.Y), textColor, itemTextScale, new Vector2(0f, 0.5f));

            // 右侧正在播放徽标
            if (isCurrent)
            {
                font.DrawText(spriteBatch, "[播放中]", new Vector2(rect.Right - (mobile ? 20 : 16), rect.Center.Y), ColorPalette.PlayerGreen, itemTextScale * 0.95f, new Vector2(1f, 0.5f));
            }
        }

        // 5. 底部工具栏按键
        DrawButton(batcher, spriteBatch, font, _btnPrev, "上一首", mousePos, ColorPalette.PlayerYellow);
        DrawButton(batcher, spriteBatch, font, _btnNext, "下一首", mousePos, ColorPalette.PlayerYellow);
        DrawButton(batcher, spriteBatch, font, _btnMode, $"模式: {audio.GetPlayModeName()}", mousePos, ColorPalette.PlayerGreen);
        DrawButton(batcher, spriteBatch, font, _btnClose, "关闭 (ESC)", mousePos, ColorPalette.PlayerRed * 0.85f);
    }

    private static void DrawButton(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Rectangle rect, string text, Vector2 mousePos, Color accent)
    {
        bool mobile = VirtualViewport.IsMobile;
        bool hover = rect.Contains(mousePos);
        Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
        Color border = hover ? Color.White : accent;

        batcher.DrawRoundedRect(spriteBatch, rect, mobile ? 10f : 7f, bg);
        batcher.DrawRectangleOutline(spriteBatch, rect, border, hover ? 3f : 1.8f);

        if (hover)
        {
            batcher.DrawRectangle(spriteBatch, new Rectangle(rect.X, rect.Y, 6, rect.Height), accent);
        }

        float textScale = mobile ? 0.95f : 0.76f;
        font.DrawText(spriteBatch, text, new Vector2(rect.Center.X, rect.Center.Y), hover ? Color.White : ColorPalette.TextWhite, textScale, new Vector2(0.5f, 0.5f));
    }
}
