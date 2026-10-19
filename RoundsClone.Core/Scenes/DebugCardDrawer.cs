using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Input;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 局内自选卡牌调试抽屉与训练靶桩控制器 (Debug Card Drawer & Training Dummy)。
/// 职责边界：允许开发者/玩家在任何战斗场景随时按 ~ 或 F1 呼出侧边抽屉，
/// 自由动态插拔 22 张卡牌、切换无限生命木桩模式、一键重置与清空，并实时统计输出 DPS 与单发伤害。
/// </summary>
public class DebugCardDrawer
{
    public bool IsOpen { get; set; } = false;
    public bool IsDummyMode { get; set; } = false;
    public bool InfiniteAmmo { get; set; } = false;
    public bool IsTargetingPlayer { get; set; } = true;

    // 靶桩伤害统计数据
    public float DummyTotalDamage { get; private set; } = 0f;
    public float DummyLastHitDamage { get; private set; } = 0f;
    public float DummyDps { get; private set; } = 0f;
    private float _damageInWindow = 0f;
    private float _dpsTimer = 0f;

    // 全部可用卡牌实例缓存
    private readonly List<ICardPlugin> _allCards;
    private int _hoveredCardIndex = -1;

    public DebugCardDrawer()
    {
        _allCards = CardRegistry.GetAllCards();
    }

    /// <summary>
    /// 记录木桩所承受的有效伤害以解算秒伤。
    /// </summary>
    public void RecordDummyDamage(float amount)
    {
        DummyTotalDamage += amount;
        DummyLastHitDamage = amount;
        _damageInWindow += amount;
    }

    private int _lastHoveredSlot = -1;

    /// <summary>
    /// 帧更新抽屉交互、快捷键监听与 DPS 解算。
    /// </summary>
    public void Update(float dt, InputManager input, Actor player, Actor ai, RoundsClone.Core.Audio.AudioManager? audio = null)
    {
        // 1. DPS 统计滑动窗口更新
        _dpsTimer += dt;
        if (_dpsTimer >= 1.0f)
        {
            DummyDps = _damageInWindow / _dpsTimer;
            _damageInWindow = 0f;
            _dpsTimer = 0f;
        }

        // 2. 调试快捷键监听
        if (input.IsKeyJustPressed(Keys.OemTilde) || input.IsKeyJustPressed(Keys.F1))
        {
            IsOpen = !IsOpen;
            audio?.PlayUiClick();
        }

        if (input.IsKeyJustPressed(Keys.F3))
        {
            IsDummyMode = !IsDummyMode;
            audio?.PlayUiClick();
            if (IsDummyMode)
            {
                DummyTotalDamage = 0f;
                DummyLastHitDamage = 0f;
                _damageInWindow = 0f;
                ai.ResetState(ai.Body.Position);
            }
        }

        if (input.IsKeyJustPressed(Keys.F4))
        {
            InfiniteAmmo = !InfiniteAmmo;
            audio?.PlayUiClick();
        }

        if (input.IsKeyJustPressed(Keys.H))
        {
            player.CurrentHealth = player.MaxHealth;
            audio?.PlayUiClick();
        }

        if (input.IsKeyJustPressed(Keys.F5))
        {
            Actor activeTarget = IsTargetingPlayer ? player : ai;
            ToggleSubWeapon(activeTarget, audio);
        }

        // 右上角浮动 [DEBUG] 按钮点击检测
        Rectangle btnQuickToggle = new(1810, 16, 95, 36);
        if (btnQuickToggle.Contains(input.VirtualMousePosition) && input.IsLeftClickJustPressed())
        {
            IsOpen = !IsOpen;
            audio?.PlayUiClick();
        }

        if (!IsOpen) return;

        // 3. 抽屉面板内鼠标交互
        Vector2 mousePos = input.VirtualMousePosition;
        bool leftClick = input.IsLeftClickJustPressed();
        bool rightClick = input.IsRightClickJustPressed();

        Actor currentTarget = IsTargetingPlayer ? player : ai;

        // 功能按键区域
        Rectangle btnDummy = new(1335, 68, 170, 34);
        Rectangle btnAmmo = new(1515, 68, 170, 34);
        Rectangle btnHeal = new(1695, 68, 170, 34);

        Rectangle btnTargetP1 = new(1335, 108, 170, 32);
        Rectangle btnTargetAi = new(1515, 108, 170, 32);
        Rectangle btnClearAll = new(1695, 108, 170, 32);

        Rectangle btnSubWeapon = new(1335, 146, 530, 34);

        if (leftClick)
        {
            if (btnDummy.Contains(mousePos))
            {
                audio?.PlayUiClick();
                IsDummyMode = !IsDummyMode;
                if (IsDummyMode)
                {
                    DummyTotalDamage = 0f;
                    DummyLastHitDamage = 0f;
                    _damageInWindow = 0f;
                    ai.ResetState(ai.Body.Position);
                }
            }
            else if (btnAmmo.Contains(mousePos))
            {
                audio?.PlayUiClick();
                InfiniteAmmo = !InfiniteAmmo;
            }
            else if (btnHeal.Contains(mousePos))
            {
                audio?.PlayUiClick();
                player.CurrentHealth = player.MaxHealth;
            }
            else if (btnTargetP1.Contains(mousePos))
            {
                audio?.PlayUiClick();
                IsTargetingPlayer = true;
            }
            else if (btnTargetAi.Contains(mousePos))
            {
                audio?.PlayUiClick();
                IsTargetingPlayer = false;
            }
            else if (btnClearAll.Contains(mousePos))
            {
                audio?.PlayUiClick();
                currentTarget.RebuildFromCards(Array.Empty<ICardPlugin>());
            }
            else if (btnSubWeapon.Contains(mousePos))
            {
                ToggleSubWeapon(currentTarget, audio);
            }
        }

        // 4. 卡牌网格点击交互（紧凑 3 列布局，彻底解决与底部已装卡牌重叠问题）
        _hoveredCardIndex = -1;
        int gridStartX = 1335;
        int gridStartY = 188;
        int itemW = 176;
        int itemH = 34;
        int gapX = 8;
        int gapY = 6;

        for (int i = 0; i < _allCards.Count; i++)
        {
            int col = i % 3;
            int row = i / 3;
            Rectangle itemRect = new(gridStartX + col * (itemW + gapX), gridStartY + row * (itemH + gapY), itemW, itemH);

            if (itemRect.Contains(mousePos))
            {
                _hoveredCardIndex = i;
                if (_lastHoveredSlot != i)
                {
                    audio?.PlayUiHover();
                    _lastHoveredSlot = i;
                }

                if (leftClick)
                {
                    audio?.PlayUiClick();
                    // 左键：给当前目标添加 1 张该卡牌
                    var newCards = new List<ICardPlugin>(currentTarget.AppliedCards) { _allCards[i] };
                    currentTarget.RebuildFromCards(newCards);
                }
                else if (rightClick)
                {
                    audio?.PlayUiClick();
                    // 右键：从当前目标卸载 1 张该卡牌
                    int removeIdx = currentTarget.AppliedCards.FindLastIndex(c => c.Id.Equals(_allCards[i].Id, StringComparison.OrdinalIgnoreCase));
                    if (removeIdx >= 0)
                    {
                        var remaining = new List<ICardPlugin>(currentTarget.AppliedCards);
                        remaining.RemoveAt(removeIdx);
                        currentTarget.RebuildFromCards(remaining);
                    }
                }
            }
        }

        if (_hoveredCardIndex == -1) _lastHoveredSlot = -1;

        // 5. 底部已装卡牌列表点击移除（下移至 Y=548 之后，与上方 25 张卡牌货架完全分离）
        int equippedStartY = 548;
        int pillX = 1335;
        int pillY = equippedStartY + 34;

        for (int i = 0; i < currentTarget.AppliedCards.Count && i < 36; i++)
        {
            Rectangle pillRect = new(pillX, pillY, 176, 30);
            if (pillRect.Contains(mousePos) && leftClick)
            {
                audio?.PlayUiClick();
                var remaining = new List<ICardPlugin>(currentTarget.AppliedCards);
                remaining.RemoveAt(i);
                currentTarget.RebuildFromCards(remaining);
                break;
            }

            pillX += 184;
            if (pillX + 176 > 1890)
            {
                pillX = 1335;
                pillY += 34;
            }
        }
    }

    /// <summary>
    /// 在屏幕空间矩阵上绘制调试抽屉完整界面与提示框。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Actor player, Actor ai)
    {
        // 1. 始终渲染右上角调试小徽章
        Rectangle btnQuick = new(1810, 16, 95, 36);
        batcher.DrawRoundedRect(spriteBatch, btnQuick, 6f, IsOpen ? Color.Cyan * 0.85f : new Color(20, 24, 36) * 0.85f);
        batcher.DrawRectangleOutline(spriteBatch, btnQuick, Color.Cyan, 1.5f);
        font.DrawText(spriteBatch, IsOpen ? "收起 [~]" : "沙盒 [~]", new Vector2(btnQuick.Center.X, btnQuick.Center.Y), Color.White, 0.78f, new Vector2(0.5f, 0.5f));

        if (!IsOpen) return;

        // 2. 抽屉主背板
        Rectangle panel = new(1315, 12, 590, 1056);
        batcher.DrawRoundedRect(spriteBatch, panel, 14f, new Color(12, 16, 24) * 0.95f);
        batcher.DrawRectangleOutline(spriteBatch, panel, Color.Cyan * 0.8f, 2.0f);

        // 标题与快捷键提示
        font.DrawText(spriteBatch, "沙盒卡牌实验室", new Vector2(panel.X + 20, 32), Color.Cyan, 1.15f);
        font.DrawText(spriteBatch, "按 [~] 或 [F1] 隐藏", new Vector2(panel.Right - 20, 36), Color.Gray, 0.72f, new Vector2(1f, 0f));

        // 3. 快捷控制按钮组
        Rectangle btnDummy = new(1335, 68, 170, 34);
        Rectangle btnAmmo = new(1515, 68, 170, 34);
        Rectangle btnHeal = new(1695, 68, 170, 34);

        DrawButton(batcher, spriteBatch, font, btnDummy, IsDummyMode ? "靶桩模式: 开启" : "靶桩模式: 关闭", IsDummyMode ? Color.LightGreen : Color.Gray);
        DrawButton(batcher, spriteBatch, font, btnAmmo, InfiniteAmmo ? "无限备弹: 开启" : "无限备弹: 关闭", InfiniteAmmo ? Color.LightGreen : Color.Gray);
        DrawButton(batcher, spriteBatch, font, btnHeal, "生命回满 (H)", Color.Coral);

        // 目标切换
        Rectangle btnTargetP1 = new(1335, 108, 170, 32);
        Rectangle btnTargetAi = new(1515, 108, 170, 32);
        Rectangle btnClearAll = new(1695, 108, 170, 32);

        DrawButton(batcher, spriteBatch, font, btnTargetP1, "目标: 玩家 P1", IsTargetingPlayer ? Color.Cyan : Color.DimGray);
        DrawButton(batcher, spriteBatch, font, btnTargetAi, "目标: AI对手", !IsTargetingPlayer ? Color.Coral : Color.DimGray);
        DrawButton(batcher, spriteBatch, font, btnClearAll, "清空卡牌", Color.IndianRed);

        // 副武器切换
        Actor currentTarget = IsTargetingPlayer ? player : ai;
        Rectangle btnSubWeapon = new(1335, 146, 530, 34);
        string weaponName = currentTarget.SubWeapon?.Name ?? "无副武器";
        string nextWeapon = currentTarget.SubWeapon is BlockSubWeapon ? "疾风冲刺" : "力场格挡";
        Color weaponColor = currentTarget.SubWeapon is BlockSubWeapon ? ColorPalette.PlayerBlue : ColorPalette.DashTrail;
        DrawButton(batcher, spriteBatch, font, btnSubWeapon, $"当前副武器: {weaponName}  [点此或按 F5 切换为 {nextWeapon}]", weaponColor);

        // 4. 卡牌超市货架网格（3 列紧凑布局，9行共 354px，高度截止于 Y=522）
        int gridStartX = 1335;
        int gridStartY = 188;
        int itemW = 176;
        int itemH = 34;
        int gapX = 8;
        int gapY = 6;

        for (int i = 0; i < _allCards.Count; i++)
        {
            var card = _allCards[i];
            int col = i % 3;
            int row = i / 3;
            Rectangle itemRect = new(gridStartX + col * (itemW + gapX), gridStartY + row * (itemH + gapY), itemW, itemH);

            bool hovered = i == _hoveredCardIndex;
            Color tierCol = GetTierColor(card.Tier);

            batcher.DrawRoundedRect(spriteBatch, itemRect, 5f, hovered ? ColorPalette.ButtonHover : new Color(24, 30, 44));
            batcher.DrawRectangleOutline(spriteBatch, itemRect, hovered ? tierCol : Color.DarkSlateGray, hovered ? 1.8f : 1.0f);

            // 品质色点
            batcher.DrawFilledCircle(spriteBatch, new Vector2(itemRect.X + 13, itemRect.Center.Y), 5f, tierCol);

            // 卡牌名称
            font.DrawText(spriteBatch, card.Name, new Vector2(itemRect.X + 24, itemRect.Center.Y), Color.White, 0.78f, new Vector2(0f, 0.5f));

            // 已装数量徽标
            int count = currentTarget.AppliedCards.Count(c => c.Id.Equals(card.Id, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                font.DrawText(spriteBatch, $"x{count}", new Vector2(itemRect.Right - 8, itemRect.Center.Y), Color.Yellow, 0.75f, new Vector2(1f, 0.5f));
            }
        }

        // 5. 底部已装卡牌详情与单卡卸载栏（起始 Y=548，与上方卡牌完全分离无重叠）
        int equippedStartY = 548;
        batcher.DrawLine(spriteBatch, new Vector2(panel.X + 16, equippedStartY), new Vector2(panel.Right - 16, equippedStartY), Color.DarkSlateGray, 1.5f);

        string targetName = IsTargetingPlayer ? "玩家已生效卡牌" : "AI/靶桩已生效卡牌";
        font.DrawText(spriteBatch, $"{targetName} ({currentTarget.AppliedCards.Count} 张) - 点击 [X] 卸载:", new Vector2(panel.X + 20, equippedStartY + 8), Color.LightGray, 0.78f);

        int pillX = 1335;
        int pillY = equippedStartY + 34;

        for (int i = 0; i < currentTarget.AppliedCards.Count && i < 36; i++)
        {
            var c = currentTarget.AppliedCards[i];
            Rectangle pillRect = new(pillX, pillY, 176, 30);
            Color tCol = GetTierColor(c.Tier);

            batcher.DrawRoundedRect(spriteBatch, pillRect, 4f, new Color(30, 36, 52));
            batcher.DrawRectangleOutline(spriteBatch, pillRect, tCol * 0.7f, 1.0f);

            font.DrawText(spriteBatch, c.Name, new Vector2(pillRect.X + 8, pillRect.Center.Y), Color.White, 0.72f, new Vector2(0f, 0.5f));
            font.DrawText(spriteBatch, "[X]", new Vector2(pillRect.Right - 8, pillRect.Center.Y), Color.IndianRed, 0.75f, new Vector2(0.5f, 0.5f));

            pillX += 184;
            if (pillX + 176 > 1890)
            {
                pillX = 1335;
                pillY += 34;
            }
        }

        // 6. 悬停卡牌浮动属性面板 (Tooltip)
        if (_hoveredCardIndex >= 0 && _hoveredCardIndex < _allCards.Count)
        {
            var card = _allCards[_hoveredCardIndex];
            int tipW = 390;
            int tipH = 220;
            int tipX = panel.X - tipW - 14;
            int tipY = Math.Clamp(_allCards.Count > 0 ? (gridStartY + (_hoveredCardIndex / 3) * 40) : 300, 80, 800);

            Rectangle tipRect = new(tipX, tipY, tipW, tipH);
            Color tierCol = GetTierColor(card.Tier);

            batcher.DrawRoundedRect(spriteBatch, tipRect, 10f, new Color(14, 18, 28) * 0.98f);
            batcher.DrawRectangleOutline(spriteBatch, tipRect, tierCol, 2.0f);

            // 标题
            font.DrawText(spriteBatch, card.Name, new Vector2(tipRect.X + 16, tipRect.Y + 16), Color.White, 1.05f);
            font.DrawText(spriteBatch, $"左键: 装配  |  右键: 卸载", new Vector2(tipRect.Right - 16, tipRect.Y + 20), Color.DarkGray, 0.70f, new Vector2(1f, 0f));

            // 双刃剑数值词条
            string[] lines = card.Description.Split('\n');
            float curY = tipRect.Y + 58;

            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                Color lineColor = ColorPalette.TextWhite;

                // 增益与负面区分：换弹时间减少、冷却减少属于缩短等待时间的正面增益（绿字）
                bool isWaitTimeReduction = line.Contains("冷却 -") || line.Contains("换弹时间 -") || line.Contains("换弹 -") || line.Contains("前摇 -");
                bool isPenalty = (line.Contains('-') && !isWaitTimeReduction) || line.Contains("换弹时间 +") || line.Contains("后坐力 +") || line.Contains("散布 +") || line.Contains("间隔 +") || line.Contains("冷却 +");
                bool isBuff = line.Contains('+') || isWaitTimeReduction || line.Contains("成长") || line.Contains("免疫") || line.Contains("折射") || line.Contains("补满") || line.Contains("无敌") || line.Contains("复活") || line.Contains("滑翔") || line.Contains("返还") || line.Contains("吸血");

                if (isBuff)
                {
                    lineColor = new Color(130, 255, 150);
                }
                if (isPenalty)
                {
                    lineColor = new Color(255, 115, 115);
                }

                font.DrawText(spriteBatch, line, new Vector2(tipRect.X + 16, curY), lineColor, 0.82f);
                curY += 28;
            }
        }
    }

    /// <summary>
    /// 在世界空间渲染木桩小人头顶的无限生命徽标与实时秒伤 DPS 数据。
    /// </summary>
    public void DrawDummyWorldHud(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Actor dummy)
    {
        if (!IsDummyMode || dummy.IsDead) return;

        Vector2 headPos = dummy.Body.Position + new Vector2(0f, -dummy.Radius - 46f);

        // 靶桩背景小标牌
        Rectangle badgeRect = new((int)headPos.X - 110, (int)headPos.Y - 26, 220, 52);
        batcher.DrawRoundedRect(spriteBatch, badgeRect, 6f, new Color(10, 14, 22) * 0.85f);
        batcher.DrawRectangleOutline(spriteBatch, badgeRect, Color.LightGreen * 0.9f, 1.5f);

        font.DrawText(spriteBatch, "训练靶桩 (锁血不死)", new Vector2(headPos.X, headPos.Y - 10f), Color.LightGreen, 0.72f, new Vector2(0.5f, 0.5f));
        font.DrawText(spriteBatch, $"DPS: {DummyDps:F1} | 累计: {DummyTotalDamage:F0}", new Vector2(headPos.X, headPos.Y + 12f), Color.Yellow, 0.75f, new Vector2(0.5f, 0.5f));
    }

    private static void DrawButton(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Rectangle rect, string text, Color color)
    {
        batcher.DrawRoundedRect(spriteBatch, rect, 6f, new Color(24, 30, 42));
        batcher.DrawRectangleOutline(spriteBatch, rect, color, 1.5f);
        font.DrawText(spriteBatch, text, new Vector2(rect.Center.X, rect.Center.Y), color, 0.75f, new Vector2(0.5f, 0.5f));
    }

    private static Color GetTierColor(CardTier tier) => tier switch
    {
        CardTier.Common => ColorPalette.TierCommon,
        CardTier.Rare => ColorPalette.TierRare,
        CardTier.Epic => ColorPalette.TierEpic,
        CardTier.Legendary => ColorPalette.TierLegendary,
        _ => Color.White
    };

    private static void ToggleSubWeapon(Actor target, RoundsClone.Core.Audio.AudioManager? audio)
    {
        audio?.PlayUiClick();
        if (target.SubWeapon is RoundsClone.Core.SubWeapons.BlockSubWeapon)
        {
            target.SubWeapon = new RoundsClone.Core.SubWeapons.DashSubWeapon();
        }
        else
        {
            target.SubWeapon = new RoundsClone.Core.SubWeapons.BlockSubWeapon();
        }
    }
}
