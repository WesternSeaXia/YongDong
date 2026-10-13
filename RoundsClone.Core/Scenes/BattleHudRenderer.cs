using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 战斗场景 HUD 界面渲染器。
/// 职责边界：专门负责渲染战斗中的顶部血条、副武器冷却进度条、回合比分胜场圆点以及回合结束大字横幅，将 UI 渲染与 <see cref="BattleScene"/> 的战斗模拟解耦。
/// </summary>
public static class BattleHudRenderer
{
    /// <summary>
    /// 渲染顶部对战状态栏 HUD（双方血条、副武器冷却进度条、中央比分与胜场点）。
    /// </summary>
    public static void DrawTopBar(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        ProceduralFont font,
        Actor player,
        Actor ai,
        float playerHealthDisplay,
        float aiHealthDisplay,
        int playerScore,
        int aiScore)
    {
        bool mobile = VirtualViewport.IsMobile;
        int topBarH = mobile ? 96 : 82;

        // 顶部 HUD 栏背景
        Rectangle topBar = new(0, 0, VirtualViewport.VirtualWidth, topBarH);
        batcher.DrawRectangle(spriteBatch, topBar, ColorPalette.BackgroundDark * 0.85f);
        batcher.DrawLine(spriteBatch, new Vector2(0, topBarH), new Vector2(VirtualViewport.VirtualWidth, topBarH), ColorPalette.WallOutline, 2f);

        int barW = mobile ? 520 : 420;
        int barH = mobile ? 36 : 26;
        int barY = mobile ? 18 : 22;
        float hpFontScale = mobile ? 0.95f : 0.68f;

        // --- 1. 玩家血条与副武器 CD (左侧) ---
        float pMaxHp = player.MaxHealth;
        float pHpPct = MathHelper.Clamp(playerHealthDisplay / pMaxHp, 0f, 1f);
        Rectangle pBarBg = new(80, barY, barW, barH);
        Rectangle pBarFill = new(80, barY, (int)(barW * pHpPct), barH);
        batcher.DrawRoundedRect(spriteBatch, pBarBg, 6f, Color.Black * 0.6f);
        batcher.DrawRoundedRect(spriteBatch, pBarFill, 6f, ColorPalette.PlayerBlue);
        batcher.DrawRectangleOutline(spriteBatch, pBarBg, ColorPalette.WallOutline, 2f);
        font.DrawText(spriteBatch, $"{(int)player.CurrentHealth} / {(int)pMaxHp}", new Vector2(96, barY + (mobile ? 4 : 2)), Color.White, hpFontScale);

        if (player.SubWeapon != null)
        {
            int cdW = mobile ? 260 : 180;
            int cdH = mobile ? 16 : 12;
            int cdY = barY + barH + 8;
            float cdPct = 1f - MathHelper.Clamp(player.SubWeapon.CurrentCooldown / player.SubWeapon.Cooldown, 0f, 1f);
            Rectangle cdBar = new(80, cdY, cdW, cdH);
            batcher.DrawRectangle(spriteBatch, cdBar, Color.Black * 0.5f);
            batcher.DrawRectangle(spriteBatch, new Rectangle(80, cdY, (int)(cdW * cdPct), cdH), player.SubWeapon.IsReady ? ColorPalette.BlockShield : ColorPalette.TextGray);
        }

        // --- 2. 中央回合比分与胜场圆点 ---
        Vector2 centerScore = new(VirtualViewport.VirtualWidth * 0.5f, topBarH * 0.5f);
        font.DrawText(spriteBatch, $"{playerScore}  :  {aiScore}", centerScore, Color.White, mobile ? 1.85f : 1.5f, new Vector2(0.5f, 0.5f));

        float dotR = mobile ? 10f : 7f;
        float dotStep = mobile ? 28f : 22f;
        float dotStart = mobile ? 115f : 95f;
        for (int i = 0; i < SceneManager.TargetWins; i++)
        {
            Vector2 pDot = new(centerScore.X - dotStart - i * dotStep, centerScore.Y);
            if (i < playerScore)
                batcher.DrawFilledCircle(spriteBatch, pDot, dotR, ColorPalette.PlayerBlue);
            else
                batcher.DrawCircleOutline(spriteBatch, pDot, dotR, ColorPalette.WallOutline, 2.5f);

            Vector2 aiDot = new(centerScore.X + dotStart + i * dotStep, centerScore.Y);
            if (i < aiScore)
                batcher.DrawFilledCircle(spriteBatch, aiDot, dotR, ColorPalette.PlayerRed);
            else
                batcher.DrawCircleOutline(spriteBatch, aiDot, dotR, ColorPalette.WallOutline, 2.5f);
        }

        // --- 3. AI 对手血条与副武器 CD (右侧) ---
        float aiMaxHp = ai.MaxHealth;
        float aiHpPct = MathHelper.Clamp(aiHealthDisplay / aiMaxHp, 0f, 1f);
        int aiBarX = VirtualViewport.VirtualWidth - 80 - barW;
        Rectangle aiBarBg = new(aiBarX, barY, barW, barH);
        Rectangle aiBarFill = new(aiBarX + (int)(barW * (1f - aiHpPct)), barY, (int)(barW * aiHpPct), barH);
        batcher.DrawRoundedRect(spriteBatch, aiBarBg, 6f, Color.Black * 0.6f);
        batcher.DrawRoundedRect(spriteBatch, aiBarFill, 6f, ColorPalette.PlayerRed);
        batcher.DrawRectangleOutline(spriteBatch, aiBarBg, ColorPalette.WallOutline, 2f);
        font.DrawText(spriteBatch, $"{(int)ai.CurrentHealth} / {(int)aiMaxHp}", new Vector2(VirtualViewport.VirtualWidth - 96, barY + (mobile ? 4 : 2)), Color.White, hpFontScale, new Vector2(1f, 0f));

        if (ai.SubWeapon != null)
        {
            int cdW = mobile ? 260 : 180;
            int cdH = mobile ? 16 : 12;
            int cdY = barY + barH + 8;
            int aiCdX = VirtualViewport.VirtualWidth - 80 - cdW;
            float aiCdPct = 1f - MathHelper.Clamp(ai.SubWeapon.CurrentCooldown / ai.SubWeapon.Cooldown, 0f, 1f);
            Rectangle aiCdBar = new(aiCdX, cdY, cdW, cdH);
            batcher.DrawRectangle(spriteBatch, aiCdBar, Color.Black * 0.5f);
            batcher.DrawRectangle(spriteBatch, new Rectangle(aiCdX + (int)(cdW * (1f - aiCdPct)), cdY, (int)(cdW * aiCdPct), cdH), ColorPalette.DashTrail);
        }
    }

    /// <summary>
    /// 渲染回合结束时的中央胜负横幅。
    /// </summary>
    public static void DrawRoundBanner(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, bool playerWonRound)
    {
        string bannerText = playerWonRound ? "回合胜利" : "回合失败";
        Color bannerColor = playerWonRound ? ColorPalette.PlayerGreen : ColorPalette.PlayerRed;
        Vector2 center = new(VirtualViewport.VirtualWidth * 0.5f, 460f);

        batcher.DrawRectangle(spriteBatch, new Rectangle(0, (int)center.Y - 60, VirtualViewport.VirtualWidth, 120), Color.Black * 0.65f);
        font.DrawText(spriteBatch, bannerText, center, bannerColor, 2.6f, new Vector2(0.5f, 0.5f));
    }
}
