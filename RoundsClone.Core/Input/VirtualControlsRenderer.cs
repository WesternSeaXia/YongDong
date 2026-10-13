using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Input;

/// <summary>
/// 虚拟触控界面渲染器。
/// 职责边界：专门负责绘制屏幕上的左右虚拟摇杆、中心死区指示环、独立开火大按键以及跳跃/副武器技能冷却按钮，将 UI 绘制与 <see cref="InputManager"/> 的输入状态解算解耦。
/// </summary>
public static class VirtualControlsRenderer
{
    /// <summary>
    /// 绘制符合双手人体工学的半透明霓虹虚拟触控摇杆与动作按键。
    /// </summary>
    public static void Draw(
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        ProceduralFont font,
        InputManager input,
        Actor? player = null)
    {
        if (!input.ShowVirtualControls) return;

        float deadzoneRadius = input.StickRadius * input.StickDeadzoneRatio;
        Color baseRing = Color.White * 0.26f;
        Color leftActiveRing = input.IsLeftStickActive ? ColorPalette.PlayerBlue * 0.80f : baseRing;

        // 1. 左侧跟随式移动摇杆（含中心死区圆环指示）
        float leftAlpha = input.IsLeftStickActive ? 1.0f : 0.65f;
        batcher.DrawFilledCircle(spriteBatch, input.LeftStickCenter, input.StickRadius, Color.Black * (0.32f * leftAlpha));
        batcher.DrawCircleOutline(spriteBatch, input.LeftStickCenter, input.StickRadius, leftActiveRing * leftAlpha, input.IsLeftStickActive ? 4f : 2.5f);
        batcher.DrawCircleOutline(spriteBatch, input.LeftStickCenter, deadzoneRadius, Color.White * (0.22f * leftAlpha), 1.8f);

        if (input.IsLeftStickActive && input.LeftKnobOffset.Length() > deadzoneRadius)
        {
            batcher.DrawLine(spriteBatch, input.LeftStickCenter, input.LeftStickCenter + input.LeftKnobOffset, ColorPalette.PlayerBlue * 0.55f, 4f);
        }
        Vector2 leftKnobPos = input.LeftStickCenter + input.LeftKnobOffset;
        batcher.DrawFilledCircle(spriteBatch, leftKnobPos, input.StickRadius * 0.38f, ColorPalette.PlayerBlue * (input.IsLeftStickActive ? 0.78f : 0.38f));
        batcher.DrawCircleOutline(spriteBatch, leftKnobPos, input.StickRadius * 0.38f, Color.White * (0.65f * leftAlpha), 2f);

        // 2. 右侧区域：根据 LeftStickControlsAim 切换“右发射摇杆”或“独立开火大按键”
        if (!input.LeftStickControlsAim)
        {
            Color rightActiveRing = input.IsRightStickActive ? ColorPalette.PlayerRed * 0.80f : baseRing;
            batcher.DrawFilledCircle(spriteBatch, input.RightStickCenter, input.StickRadius, Color.Black * 0.32f);
            batcher.DrawCircleOutline(spriteBatch, input.RightStickCenter, input.StickRadius, rightActiveRing, input.IsRightStickActive ? 4f : 2.5f);
            batcher.DrawCircleOutline(spriteBatch, input.RightStickCenter, deadzoneRadius, Color.White * 0.20f, 1.8f);
            if (input.IsRightStickActive && input.RightKnobOffset.Length() > deadzoneRadius)
            {
                batcher.DrawLine(spriteBatch, input.RightStickCenter, input.RightStickCenter + input.RightKnobOffset, ColorPalette.PlayerRed * 0.55f, 4f);
            }
            Vector2 rightKnobPos = input.RightStickCenter + input.RightKnobOffset;
            batcher.DrawFilledCircle(spriteBatch, rightKnobPos, input.StickRadius * 0.38f, ColorPalette.PlayerRed * (input.IsRightStickActive ? 0.78f : 0.45f));
            batcher.DrawCircleOutline(spriteBatch, rightKnobPos, input.StickRadius * 0.38f, Color.White * 0.65f, 2f);
        }
        else
        {
            // 右侧独立开火大按键
            Color fireBg = input.IsFireButtonPressed ? ColorPalette.PlayerRed * 0.85f : ColorPalette.PlayerRed * 0.38f;
            batcher.DrawFilledCircle(spriteBatch, input.FireButtonCenter, input.FireButtonRadius, fireBg);
            batcher.DrawCircleOutline(spriteBatch, input.FireButtonCenter, input.FireButtonRadius, ColorPalette.PlayerRed, input.IsFireButtonPressed ? 5f : 3.5f);
            batcher.DrawCircleOutline(spriteBatch, input.FireButtonCenter, input.FireButtonRadius * 0.78f, Color.White * 0.3f, 2f);
            font.DrawText(spriteBatch, "开火", input.FireButtonCenter, Color.White, 1.25f, new Vector2(0.5f, 0.5f));
        }

        // 3. 右手内弧形跳跃按钮
        Color rightJumpColor = input.IsJumpButtonPressed ? ColorPalette.PlayerGreen * 0.78f : Color.Black * 0.38f;
        batcher.DrawFilledCircle(spriteBatch, input.JumpButtonCenter, input.JumpButtonRadius, rightJumpColor);
        batcher.DrawCircleOutline(spriteBatch, input.JumpButtonCenter, input.JumpButtonRadius, ColorPalette.PlayerGreen * 0.8f, 3.5f);
        font.DrawText(spriteBatch, "跳跃", input.JumpButtonCenter, Color.White, 0.98f, new Vector2(0.5f, 0.5f));

        // 4. 右手内弧形技能(副武器)按钮（支持实时冷却显示）
        bool subReady = player?.SubWeapon == null || player.SubWeapon.IsReady;
        string subName = player?.SubWeapon?.Name ?? "技能";
        Color secBgColor = input.IsSecondaryButtonPressed
            ? ColorPalette.BlockShield * 0.85f
            : (subReady ? ColorPalette.BlockShield * 0.38f : Color.Black * 0.45f);
        Color secBorderColor = subReady ? ColorPalette.BlockShield : ColorPalette.TextGray * 0.5f;

        batcher.DrawFilledCircle(spriteBatch, input.SecondaryButtonCenter, input.ButtonRadius, secBgColor);
        batcher.DrawCircleOutline(spriteBatch, input.SecondaryButtonCenter, input.ButtonRadius, secBorderColor, subReady ? 4f : 2.5f);
        if (!subReady && player?.SubWeapon != null)
        {
            string cdText = $"{player.SubWeapon.CurrentCooldown:0.0}s";
            font.DrawText(spriteBatch, cdText, input.SecondaryButtonCenter, ColorPalette.PlayerYellow, 0.95f, new Vector2(0.5f, 0.5f));
        }
        else
        {
            font.DrawText(spriteBatch, subName, input.SecondaryButtonCenter, Color.White, 0.92f, new Vector2(0.5f, 0.5f));
        }
    }
}
