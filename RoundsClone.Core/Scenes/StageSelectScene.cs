using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Maps;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 关卡三选一草拟选择场景。
/// 每轮回合对决开启前，玩家从随机三座具备不同拓扑结构与环境物理特性的竞技场中挑选一座作为本次对战战场。
/// </summary>
public class StageSelectScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;
    private List<MapData> _draftedMaps = new();
    private int _hoveredCardIndex = -1;
    private int _lastHoveredCardIndex = -1;

    public StageSelectScene(SceneManager manager, Action exitAction)
    {
        _manager = manager;
        _exitAction = exitAction;
    }

    public void Enter()
    {
        // 随机草拟 3 张不同竞技场卡牌
        _draftedMaps = MapRegistry.DraftMaps(3);
        _hoveredCardIndex = -1;
        _lastHoveredCardIndex = -1;
    }

    public void Update(float dt)
    {
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();
        bool mobile = VirtualViewport.IsMobile;

        int cardW = mobile ? 540 : 420;
        int cardH = mobile ? 680 : 440;
        int gap = mobile ? 45 : 55;
        int count = Math.Max(1, _draftedMaps.Count);
        int totalW = cardW * count + gap * (count - 1);
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;
        int startY = mobile ? 210 : 300;

        int prevHover = _lastHoveredCardIndex;
        _hoveredCardIndex = -1;

        for (int i = 0; i < _draftedMaps.Count; i++)
        {
            Rectangle cardRect = new(startX + i * (cardW + gap), startY, cardW, cardH);
            if (cardRect.Contains(mousePos))
            {
                _hoveredCardIndex = i;
                if (click)
                {
                    _manager.Audio.PlayUiClick();
                    _manager.CurrentMap = _draftedMaps[i];
                    _manager.ChangeScene(new BattleScene(_manager, _exitAction));
                    break;
                }
            }
        }

        if (_hoveredCardIndex != -1 && _hoveredCardIndex != prevHover)
        {
            _manager.Audio.PlayUiHover();
        }
        _lastHoveredCardIndex = _hoveredCardIndex;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        bool mobile = VirtualViewport.IsMobile;

        // 顶部简洁标题
        font.DrawText(spriteBatch, "选择地图", new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 105f : 140f), Color.White, mobile ? 2.5f : 2.0f, new Vector2(0.5f, 0.5f));

        int cardW = mobile ? 540 : 420;
        int cardH = mobile ? 680 : 440;
        int gap = mobile ? 45 : 55;
        int count = Math.Max(1, _draftedMaps.Count);
        int totalW = cardW * count + gap * (count - 1);
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;
        int startY = mobile ? 210 : 300;

        for (int i = 0; i < _draftedMaps.Count; i++)
        {
            var map = _draftedMaps[i];
            bool hover = i == _hoveredCardIndex;

            int lift = hover ? 16 : 0;
            Rectangle cardRect = new(startX + i * (cardW + gap), startY - lift, cardW, cardH);

            Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;
            Color border = hover ? ColorPalette.PlayerBlue : ColorPalette.WallOutline;

            // 绘制卡牌主体
            batcher.DrawRoundedRect(spriteBatch, cardRect, 14f, bg);
            batcher.DrawRectangleOutline(spriteBatch, cardRect, border, hover ? 4.5f : 2.5f);

            // 顶部地图名条带
            int headerH = mobile ? 92 : 64;
            Rectangle headerRect = new(cardRect.X, cardRect.Y, cardRect.Width, headerH);
            batcher.DrawRoundedRect(spriteBatch, headerRect, 12f, hover ? ColorPalette.PlayerBlue * 0.85f : ColorPalette.WallSolid);
            font.DrawText(spriteBatch, map.Name, new Vector2(headerRect.Center.X, headerRect.Center.Y), Color.White, mobile ? 1.48f : 1.05f, new Vector2(0.5f, 0.5f));

            // 地图缩略图预览
            int thumbY = cardRect.Y + headerH + 28;
            int thumbH = cardRect.Height - headerH - 56;
            Rectangle thumbRect = new(cardRect.X + 28, thumbY, cardRect.Width - 56, thumbH);
            map.DrawThumbnail(batcher, spriteBatch, thumbRect);
        }
    }

    public void Exit() { }
}
