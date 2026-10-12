using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 技能卡牌三选一草拟强化场景。
/// 回合结算后落后方（或每轮对战前）在此界面挑选一张强力卡牌，系统实时高亮显示与已装备卡牌的连锁协同关系。
/// </summary>
public class CardSelectScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;
    private readonly bool _isForPlayer;
    private List<ICardPlugin> _draftedCards = new();
    private int _hoveredCardIndex = -1;

    public CardSelectScene(SceneManager manager, Action exitAction, bool isForPlayer = true)
    {
        _manager = manager;
        _exitAction = exitAction;
        _isForPlayer = isForPlayer;
    }

    public void Enter()
    {
        _draftedCards = CardRegistry.DraftCards(3);
        _hoveredCardIndex = -1;
    }

    public void Update(float dt)
    {
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();
        bool mobile = VirtualViewport.IsMobile;

        int cardW = mobile ? 540 : 400;
        int cardH = mobile ? 720 : 540;
        int gap = mobile ? 45 : 60;
        int totalW = cardW * 3 + gap * 2;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;
        int startY = mobile ? 195 : 260;

        _hoveredCardIndex = -1;

        for (int i = 0; i < _draftedCards.Count; i++)
        {
            Rectangle cardRect = new(startX + i * (cardW + gap), startY, cardW, cardH);
            if (cardRect.Contains(mousePos))
            {
                _hoveredCardIndex = i;
                if (click)
                {
                    // 装备所选卡牌
                    var chosen = _draftedCards[i];
                    if (_isForPlayer)
                    {
                        _manager.PlayerCards.Add(chosen);
                    }
                    else
                    {
                        _manager.AiCards.Add(chosen);
                    }

                    // 挑选完技能后，进入下一关战场挑选！
                    _manager.ChangeScene(new StageSelectScene(_manager, _exitAction));
                    break;
                }
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        bool mobile = VirtualViewport.IsMobile;

        // 顶部简洁标题
        font.DrawText(spriteBatch, "选择技能", new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 95f : 120f), Color.White, mobile ? 2.5f : 2.0f, new Vector2(0.5f, 0.5f));

        int cardW = mobile ? 540 : 400;
        int cardH = mobile ? 720 : 540;
        int gap = mobile ? 45 : 60;
        int totalW = cardW * 3 + gap * 2;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;
        int startY = mobile ? 195 : 260;

        for (int i = 0; i < _draftedCards.Count; i++)
        {
            var card = _draftedCards[i];
            bool hover = i == _hoveredCardIndex;
            int lift = hover ? 16 : 0;
            Rectangle cardRect = new(startX + i * (cardW + gap), startY - lift, cardW, cardH);

            Color tierColor = GetTierColor(card.Tier);
            Color bg = hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard;

            // 1. 卡牌底板与边框
            batcher.DrawRoundedRect(spriteBatch, cardRect, 16f, bg);
            batcher.DrawRectangleOutline(spriteBatch, cardRect, hover ? tierColor : ColorPalette.WallOutline, hover ? 4.5f : 2.5f);

            // 2. 卡牌名称横幅
            int bannerH = mobile ? 96 : 68;
            Rectangle bannerRect = new(cardRect.X, cardRect.Y, cardRect.Width, bannerH);
            batcher.DrawRoundedRect(spriteBatch, bannerRect, 14f, tierColor * 0.85f);
            font.DrawText(spriteBatch, card.Name, new Vector2(bannerRect.Center.X, bannerRect.Center.Y), Color.White, mobile ? 1.55f : 1.15f, new Vector2(0.5f, 0.5f));

            // 3. 矢量图标徽章圆环
            Vector2 iconCenter = new(cardRect.Center.X, cardRect.Y + (mobile ? 265 : 190));
            float ringR = mobile ? 88f : 62f;
            batcher.DrawFilledCircle(spriteBatch, iconCenter, ringR, Color.Black * 0.45f);
            batcher.DrawCircleOutline(spriteBatch, iconCenter, ringR, tierColor, 3.5f);
            ProceduralIcons.DrawIcon(batcher, spriteBatch, card.Icon, iconCenter, mobile ? 105f : 75f, tierColor);

            // 4. 简洁数值属性
            font.DrawText(spriteBatch, card.Description, new Vector2(cardRect.Center.X, cardRect.Y + (mobile ? 460 : 350)), ColorPalette.TextWhite, mobile ? 1.42f : 0.95f, new Vector2(0.5f, 0f));
        }
    }

    private Color GetTierColor(CardTier tier) => tier switch
    {
        CardTier.Common => ColorPalette.TierCommon,
        CardTier.Rare => ColorPalette.TierRare,
        CardTier.Epic => ColorPalette.TierEpic,
        CardTier.Legendary => ColorPalette.TierLegendary,
        _ => Color.White
    };

    private string GetTierName(CardTier tier) => tier switch
    {
        CardTier.Common => "普通",
        CardTier.Rare => "稀有",
        CardTier.Epic => "史诗",
        CardTier.Legendary => "传说",
        _ => ""
    };

    public void Exit() { }
}
