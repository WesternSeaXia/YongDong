using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 比赛终局结算场景。
/// 展示最终决斗获胜者、双方积累的技能卡牌流派一览、再战一场或返回主菜单。
/// </summary>
public class GameOverScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;

    private Rectangle _btnRematch;
    private Rectangle _btnMenu;

    public GameOverScene(SceneManager manager, Action exitAction)
    {
        _manager = manager;
        _exitAction = exitAction;

        int cx = VirtualViewport.VirtualWidth / 2;
        if (VirtualViewport.IsMobile)
        {
            _btnRematch = new Rectangle(cx - 500, 865, 450, 102);
            _btnMenu = new Rectangle(cx + 50, 865, 450, 102);
        }
        else
        {
            _btnRematch = new Rectangle(cx - 360, 860, 320, 65);
            _btnMenu = new Rectangle(cx + 40, 860, 320, 65);
        }
    }

    public void Enter() { }

    public void Update(float dt)
    {
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();

        if (_btnRematch.Contains(mousePos) && click)
        {
            _manager.ResetMatch();
            _manager.ChangeScene(new StageSelectScene(_manager, _exitAction));
        }
        else if (_btnMenu.Contains(mousePos) && click)
        {
            _manager.ResetMatch();
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool mobile = VirtualViewport.IsMobile;

        bool playerWon = _manager.PlayerScore >= SceneManager.TargetWins;

        // 1. 胜利/失败标题
        string title = playerWon ? "胜利" : "失败";
        Color titleColor = playerWon ? ColorPalette.PlayerGreen : ColorPalette.PlayerRed;
        font.DrawText(spriteBatch, title, new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 130f : 160f), titleColor, mobile ? 3.2f : 2.6f, new Vector2(0.5f, 0.5f));

        // 2. 比分板
        int sbW = mobile ? 660 : 500;
        int sbH = mobile ? 150 : 130;
        Rectangle scoreBoard = new((VirtualViewport.VirtualWidth - sbW) / 2, mobile ? 235 : 270, sbW, sbH);
        batcher.DrawRoundedRect(spriteBatch, scoreBoard, 12f, ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, scoreBoard, titleColor, 3.5f);
        font.DrawText(spriteBatch, $"{_manager.PlayerScore}   :   {_manager.AiScore}", new Vector2(scoreBoard.Center.X, scoreBoard.Center.Y), Color.White, mobile ? 2.4f : 2.0f, new Vector2(0.5f, 0.5f));

        // 3. 双方已选卡牌展示
        int colW = mobile ? 720 : 550;
        int colH = mobile ? 395 : 340;
        int colY = mobile ? 425 : 450;

        Rectangle pCol = new(mobile ? 200 : 350, colY, colW, colH);
        batcher.DrawRoundedRect(spriteBatch, pCol, 12f, ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, pCol, ColorPalette.PlayerBlue, 2.5f);
        font.DrawText(spriteBatch, "玩家卡牌", new Vector2(pCol.Center.X, pCol.Y + (mobile ? 42 : 30)), ColorPalette.PlayerBlue, mobile ? 1.35f : 1.0f, new Vector2(0.5f, 0.5f));

        float pCardY = pCol.Y + (mobile ? 95 : 70);
        foreach (var card in _manager.PlayerCards)
        {
            font.DrawText(spriteBatch, $"• {card.Name}", new Vector2(pCol.X + 45, pCardY), Color.White, mobile ? 1.15f : 0.78f);
            pCardY += mobile ? 48 : 34;
        }

        Rectangle aiCol = new(mobile ? 1000 : 1020, colY, colW, colH);
        batcher.DrawRoundedRect(spriteBatch, aiCol, 12f, ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, aiCol, ColorPalette.PlayerRed, 2.5f);
        font.DrawText(spriteBatch, "AI 卡牌", new Vector2(aiCol.Center.X, aiCol.Y + (mobile ? 42 : 30)), ColorPalette.PlayerRed, mobile ? 1.35f : 1.0f, new Vector2(0.5f, 0.5f));

        float aiCardY = aiCol.Y + (mobile ? 95 : 70);
        foreach (var card in _manager.AiCards)
        {
            font.DrawText(spriteBatch, $"• {card.Name}", new Vector2(aiCol.X + 45, aiCardY), Color.White, mobile ? 1.15f : 0.78f);
            aiCardY += mobile ? 48 : 34;
        }

        // 4. 底部按钮
        bool rematchHover = _btnRematch.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnRematch, 12f, rematchHover ? ColorPalette.PlayerGreen : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnRematch, Color.White, rematchHover ? 4f : 2.5f);
        font.DrawText(spriteBatch, "再来一局", new Vector2(_btnRematch.Center.X, _btnRematch.Center.Y), Color.White, mobile ? 1.45f : 1.0f, new Vector2(0.5f, 0.5f));

        bool menuHover = _btnMenu.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnMenu, 12f, menuHover ? ColorPalette.PlayerBlue : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnMenu, Color.White, menuHover ? 4f : 2.5f);
        font.DrawText(spriteBatch, "主菜单", new Vector2(_btnMenu.Center.X, _btnMenu.Center.Y), Color.White, mobile ? 1.45f : 1.0f, new Vector2(0.5f, 0.5f));
    }

    public void Exit() { }
}
