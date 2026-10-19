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
    private Rectangle _btnSandbox;
    private Rectangle _btnToggleControls;
    private Rectangle _btnCredits;
    private Rectangle _btnExit;

    // 感谢名单弹窗状态与分页交互
    private bool _showCredits = false;
    private int _creditsPage = 0;
    private Rectangle _creditsCardRect;
    private Rectangle _creditsPrevPageBtn;
    private Rectangle _creditsNextPageBtn;
    private Rectangle _creditsCloseBtn;
    private int _lastHoveredButton = -1;

    public MainMenuScene(SceneManager manager, Action exitGameAction)
    {
        _manager = manager;
        _exitGameAction = exitGameAction;

        int cx = VirtualViewport.VirtualWidth / 2;
        int cy = VirtualViewport.VirtualHeight / 2;
        if (VirtualViewport.IsMobile)
        {
            int btnW = 580;
            int btnH = 82;
            int gap = 16;
            int startY = 410;
            _btnStart = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 0, btnW, btnH);
            _btnSandbox = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 1, btnW, btnH);
            _btnToggleControls = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH);
            _btnCredits = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 3, btnW, btnH);
            _btnExit = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 4, btnW, btnH);

            int cardW = 1360;
            int cardH = 840;
            _creditsCardRect = new Rectangle(cx - cardW / 2, cy - cardH / 2, cardW, cardH);

            int bH = 64;
            int bY = _creditsCardRect.Bottom - 82;
            _creditsPrevPageBtn = new Rectangle(cx - 480, bY, 190, bH);
            _creditsNextPageBtn = new Rectangle(cx + 90, bY, 190, bH);
            _creditsCloseBtn = new Rectangle(cx + 300, bY, 230, bH);
        }
        else
        {
            int btnW = 360;
            int btnH = 56;
            int gap = 14;
            int startY = 455;
            _btnStart = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 0, btnW, btnH);
            _btnSandbox = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 1, btnW, btnH);
            _btnToggleControls = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH);
            _btnCredits = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 3, btnW, btnH);
            _btnExit = new Rectangle(cx - btnW / 2, startY + (btnH + gap) * 4, btnW, btnH);

            int cardW = 1160;
            int cardH = 720;
            _creditsCardRect = new Rectangle(cx - cardW / 2, cy - cardH / 2, cardW, cardH);

            int bH = 48;
            int bY = _creditsCardRect.Bottom - 68;
            _creditsPrevPageBtn = new Rectangle(cx - 380, bY, 140, bH);
            _creditsNextPageBtn = new Rectangle(cx + 80, bY, 140, bH);
            _creditsCloseBtn = new Rectangle(cx + 240, bY, 180, bH);
        }
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

        // 感谢名单模态弹窗拦截输入
        if (_showCredits)
        {
            int hoveredCreditsBtn = -1;
            if (_creditsPrevPageBtn.Contains(mousePos)) hoveredCreditsBtn = 1;
            else if (_creditsNextPageBtn.Contains(mousePos)) hoveredCreditsBtn = 2;
            else if (_creditsCloseBtn.Contains(mousePos)) hoveredCreditsBtn = 3;

            if (hoveredCreditsBtn != -1 && hoveredCreditsBtn != _lastHoveredButton)
            {
                _manager.Audio.PlayUiHover();
            }
            _lastHoveredButton = hoveredCreditsBtn;

            int totalPages = PageRanges.Length;
            if (_creditsPrevPageBtn.Contains(mousePos) && click)
            {
                _manager.Audio.PlayUiClick();
                _creditsPage = (_creditsPage - 1 + totalPages) % totalPages;
            }
            else if (_creditsNextPageBtn.Contains(mousePos) && click)
            {
                _manager.Audio.PlayUiClick();
                _creditsPage = (_creditsPage + 1) % totalPages;
            }
            else if (_manager.Input.IsKeyJustPressed(Keys.Escape) || (_creditsCloseBtn.Contains(mousePos) && click))
            {
                _manager.Audio.PlayUiClick();
                _showCredits = false;
            }
            return;
        }

        int hoveredMainBtn = -1;
        if (_btnStart.Contains(mousePos)) hoveredMainBtn = 10;
        else if (_btnSandbox.Contains(mousePos)) hoveredMainBtn = 11;
        else if (_btnToggleControls.Contains(mousePos)) hoveredMainBtn = 12;
        else if (_btnCredits.Contains(mousePos)) hoveredMainBtn = 13;
        else if (_btnExit.Contains(mousePos)) hoveredMainBtn = 14;

        if (hoveredMainBtn != -1 && hoveredMainBtn != _lastHoveredButton)
        {
            _manager.Audio.PlayUiHover();
        }
        _lastHoveredButton = hoveredMainBtn;

        // 按钮点击响应
        if (_btnStart.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _manager.ChangeScene(new CharacterSelectScene(_manager, _exitGameAction));
        }
        else if (_btnSandbox.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _manager.ResetMatch();
            _manager.ChangeScene(new BattleScene(_manager, _exitGameAction, isSandboxMode: true));
        }
        else if (_btnToggleControls.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _manager.ChangeScene(new SettingsScene(_manager, _exitGameAction));
        }
        else if (_btnCredits.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _showCredits = true;
        }
        else if (_btnExit.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
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

        float ringY = VirtualViewport.IsMobile ? 240f : 280f;
        Vector2 centerRing = new(VirtualViewport.VirtualWidth * 0.5f, ringY);
        float pulse = MathF.Sin(_time * 1.5f) * 15f;
        batcher.DrawCircleOutline(spriteBatch, centerRing, (VirtualViewport.IsMobile ? 180f : 160f) + pulse, ColorPalette.PlayerBlue * 0.25f, 3f);
        batcher.DrawCircleOutline(spriteBatch, centerRing, (VirtualViewport.IsMobile ? 230f : 210f) - pulse, ColorPalette.PlayerRed * 0.2f, 2f);

        _manager.Particles.Draw(batcher, spriteBatch);

        // 2. 简洁主标题与英文副标题
        font.DrawText(spriteBatch, "混乱枪豆人", centerRing - new Vector2(0, VirtualViewport.IsMobile ? 16f : 14f), Color.White, VirtualViewport.IsMobile ? 2.6f : 2.2f, new Vector2(0.5f, 0.5f));
        font.DrawText(spriteBatch, "CHAOS GUNNER", centerRing + new Vector2(0, VirtualViewport.IsMobile ? 42f : 36f), ColorPalette.TextGray * 0.85f, VirtualViewport.IsMobile ? 0.85f : 0.70f, new Vector2(0.5f, 0.5f));

        // 3. 按钮列表
        DrawButton(batcher, spriteBatch, font, _btnStart, "开始游戏", mousePos, ColorPalette.PlayerBlue);
        DrawButton(batcher, spriteBatch, font, _btnSandbox, "沙盒模式", mousePos, ColorPalette.ElectricPurple);
        DrawButton(batcher, spriteBatch, font, _btnToggleControls, "游戏设置 (含AI难度)", mousePos, ColorPalette.PlayerGreen);
        DrawButton(batcher, spriteBatch, font, _btnCredits, "感谢名单", mousePos, ColorPalette.PlayerYellow);
        DrawButton(batcher, spriteBatch, font, _btnExit, "退出游戏", mousePos, ColorPalette.PlayerRed * 0.8f);

        // 4. 感谢名单弹窗
        if (_showCredits)
        {
            DrawCreditsModal(batcher, spriteBatch, font, mousePos);
        }
    }

    private static readonly (string title, string code1, string code2)[] MusicCredits = new (string title, string code1, string code2)[]
    {
        // Page 1: 01 ~ 04 (截图确认署名)
        (
            "01. 俱乐部跳水 (Club Diver)",
            "\"Club Diver\" Kevin MacLeod (incompetech.com) 采用知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "02. 视频地牢Boss (Video Dungeon Boss)",
            "\"视频地牢老板\" Kevin MacLeod (incompetech.com) 根据知识使用 CC 授权：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "03. 闪亮科技II (Shiny Tech II)",
            "\"闪亮技术II\" Kevin MacLeod (incompetech.com) 根据知识使用许可协议：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "04. 银河说唱 (Galactic Rap)",
            "\"银河说唱\" Kevin MacLeod (incompetech.com) 采用知识共享许可：署名 4.0 许可 协议",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),

        // Page 2: 05 ~ 08 (截图确认署名)
        (
            "05. 云舞者 (Cloud Dancer)",
            "\"云舞者\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可 协议",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "06. 以太夜俱乐部 (Ethernight Club)",
            "\"Ethernight Club\" Kevin MacLeod (incompetech.com) 根据知识使用许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "07. 体素革命 (Voxel Revolution)",
            "\"体素革命\" Kevin MacLeod (incompetech.com) 根据知识使用 CC 授权：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "08. 新浪潮 (Newer Wave)",
            "\"新浪潮\" Kevin MacLeod (incompetech.com) 根据知识用 CC 授权：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),

        // Page 3: 09 ~ 12 (包含新确认署名与待补充项)
        (
            "09. 美之流 (Beauty Flow)",
            "\"Beauty Flow\" Kevin MacLeod (incompetech.com) 采用知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "10. 哔哔声演示 (Bleeping Demo)",
            "\"哔哔演示\" Kevin MacLeod (incompetech.com) 根据知识使用许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "11. 坏血病队长 (Captain Scurvy)",
            "\"坏血病队长\" Kevin MacLeod (incompetech.com 年) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "12. 迷失灵魂的沙漠 (Desert of Lost Souls)",
            "\"失落灵魂的沙漠\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),

        // Page 4: 13 ~ 16 (截图确认署名)
        (
            "13. 完成任务 (Getting it Done)",
            "\"Getting it Done\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "14. 激光槽 (Laser Groove)",
            "\"激光凹槽\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "15. 激光包 (Laserpack)",
            "\"Laserpack\" Kevin MacLeod (incompetech.com) 采用知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),
        (
            "16. 普卡托里与朋友们 (Pookatori and Friends)",
            "\"Pookatori and Friends\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        ),

        // Page 5: 17 (截图确认署名)
        (
            "17. 实现器 (Realizer)",
            "\"实现者\" Kevin MacLeod (incompetech.com) 根据知识共享许可：署名 4.0 许可",
            "协议链接: http://creativecommons.org/licenses/by/4.0/"
        )
    };

    private static readonly (int start, int count)[] PageRanges = new[]
    {
        (0, 4),   // 第 1 页: 01 ~ 04 (4 首)
        (4, 4),   // 第 2 页: 05 ~ 08 (4 首)
        (8, 4),   // 第 3 页: 09 ~ 12 (4 首)
        (12, 5)   // 第 4 页: 13 ~ 17 (5 首，全量 17 首完整展现)
    };

    private void DrawCreditsModal(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Vector2 mousePos)
    {
        // 1. 全屏半透明深色遮罩
        batcher.DrawRectangle(spriteBatch, new Rectangle(0, 0, VirtualViewport.VirtualWidth, VirtualViewport.VirtualHeight), Color.Black * 0.80f);

        bool mobile = VirtualViewport.IsMobile;
        int cx = VirtualViewport.VirtualWidth / 2;

        // 2. 弹窗主背景与发光描边
        batcher.DrawRoundedRect(spriteBatch, _creditsCardRect, 16f, ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, _creditsCardRect, ColorPalette.PlayerYellow, 3f);

        // 3. 标题与说明
        float titleScale = mobile ? 1.5f : 1.25f;
        float subScale = mobile ? 0.80f : 0.65f;
        float textScale = mobile ? 0.74f : 0.60f;

        font.DrawText(spriteBatch, "致谢名单与开源音乐署名 (CREDITS)", new Vector2(cx, _creditsCardRect.Y + (mobile ? 42 : 34)), Color.White, titleScale, new Vector2(0.5f, 0.5f));
        font.DrawText(spriteBatch, "本项目背景音乐均采用知识共享署名许可 (CC BY 4.0)，特向原作者致以崇高谢意：", new Vector2(cx, _creditsCardRect.Y + (mobile ? 82 : 68)), ColorPalette.TextGray, subScale, new Vector2(0.5f, 0.5f));

        // 4. 分页显示曲目署名卡片
        int totalPages = PageRanges.Length;
        _creditsPage = Math.Clamp(_creditsPage, 0, totalPages - 1);

        var (startIdx, pageCount) = PageRanges[_creditsPage];

        int itemStartX = _creditsCardRect.X + (mobile ? 45 : 36);
        int itemW = _creditsCardRect.Width - (mobile ? 90 : 72);
        int itemH = (pageCount == 5) ? (mobile ? 104 : 86) : (mobile ? 116 : 94);
        int itemGap = (pageCount == 5) ? (mobile ? 12 : 9) : (mobile ? 14 : 10);
        int listStartY = _creditsCardRect.Y + (mobile ? 116 : 96);

        for (int i = 0; i < pageCount; i++)
        {
            int trackIdx = startIdx + i;
            int iy = listStartY + i * (itemH + itemGap);
            Rectangle itemRect = new(itemStartX, iy, itemW, itemH);

            batcher.DrawRoundedRect(spriteBatch, itemRect, 8f, Color.Black * 0.45f);
            batcher.DrawRectangleOutline(spriteBatch, itemRect, ColorPalette.WallOutline, 1.5f);
            batcher.DrawRectangle(spriteBatch, new Rectangle(itemRect.X, itemRect.Y, 8, itemRect.Height), ColorPalette.PlayerYellow * 0.8f);

            var (title, code1, code2) = MusicCredits[trackIdx];
            font.DrawText(spriteBatch, title, new Vector2(itemRect.X + 22, iy + (mobile ? 16 : 12)), ColorPalette.PlayerYellow, mobile ? 0.92f : 0.76f);
            font.DrawText(spriteBatch, code1, new Vector2(itemRect.X + 22, iy + (mobile ? 48 : 38)), Color.White, textScale);
            font.DrawText(spriteBatch, code2, new Vector2(itemRect.X + 22, iy + (mobile ? 76 : 62)), ColorPalette.TextGray, textScale * 0.95f);
        }

        // 5. 分页导航与页码
        int endTrackNum = startIdx + pageCount;
        int midTextX = (_creditsPrevPageBtn.Right + _creditsNextPageBtn.X) / 2;
        string pageStr = $"第 {_creditsPage + 1} / {totalPages} 页 (曲目 {startIdx + 1:D2} - {endTrackNum:D2} / 共 {MusicCredits.Length} 首)";
        font.DrawText(spriteBatch, pageStr, new Vector2(midTextX, _creditsPrevPageBtn.Center.Y), ColorPalette.PlayerYellow, mobile ? 0.82f : 0.68f, new Vector2(0.5f, 0.5f));

        DrawCreditsNavButton(batcher, spriteBatch, font, _creditsPrevPageBtn, "上一页", mousePos, ColorPalette.PlayerYellow);
        DrawCreditsNavButton(batcher, spriteBatch, font, _creditsNextPageBtn, "下一页", mousePos, ColorPalette.PlayerYellow);
        DrawCreditsNavButton(batcher, spriteBatch, font, _creditsCloseBtn, "关 闭 (ESC)", mousePos, ColorPalette.PlayerRed * 0.85f);
    }

    private static void DrawCreditsNavButton(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Rectangle rect, string text, Vector2 mousePos, Color accent)
    {
        bool mobile = VirtualViewport.IsMobile;
        bool hover = rect.Contains(mousePos);
        Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
        Color border = hover ? Color.White : accent;

        batcher.DrawRoundedRect(spriteBatch, rect, mobile ? 10f : 7f, bg);
        batcher.DrawRectangleOutline(spriteBatch, rect, border, hover ? 3f : 1.8f);
        font.DrawText(spriteBatch, text, new Vector2(rect.Center.X, rect.Center.Y), hover ? Color.White : ColorPalette.TextWhite, mobile ? 0.95f : 0.78f, new Vector2(0.5f, 0.5f));
    }

    private void DrawButton(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Rectangle rect, string text, Vector2 mousePos, Color accent)
    {
        bool hover = rect.Contains(mousePos);
        Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
        Color border = hover ? accent : ColorPalette.WallOutline;

        batcher.DrawRoundedRect(spriteBatch, rect, VirtualViewport.IsMobile ? 12f : 8f, bg);
        batcher.DrawRectangleOutline(spriteBatch, rect, border, hover ? 3.5f : 2.5f);

        if (hover)
        {
            batcher.DrawRectangle(spriteBatch, new Rectangle(rect.X, rect.Y, 8, rect.Height), accent);
        }

        float textScale = VirtualViewport.IsMobile ? 1.38f : 0.85f;
        font.DrawText(spriteBatch, text, new Vector2(rect.Center.X, rect.Center.Y), hover ? Color.White : ColorPalette.TextWhite, textScale, new Vector2(0.5f, 0.5f));
    }

    public void Exit() { }
}
