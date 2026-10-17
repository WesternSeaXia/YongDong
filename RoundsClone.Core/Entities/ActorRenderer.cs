using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色躯干、定长竹节手脚、枪械与头顶弹匣指示器渲染器。
/// 职责边界：专门负责将角色物理状态与 <see cref="ActorLimbAnimator"/> 解算出的角位移及躯干后坐力偏移动画转换为屏幕上的程序化矢量图形。
/// </summary>
public static class ActorRenderer
{
    /// <summary>
    /// 渲染完整角色外观（双腿 -> 副手 -> 弹性球体躯干 -> 面部表情 -> 主手与枪械 -> 弹匣指示器 -> 副武器特效）。
    /// </summary>
    public static void Draw(Actor actor, ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        if (actor.IsDead) return;

        var body = actor.Body;
        var anim = actor.LimbAnimator;
        Vector2 pos = body.Position;
        Vector2 currentAim = actor.CurrentAim;

        Color renderColor = actor.HitFlashTimer > 0f ? ColorPalette.WhiteFlash : actor.ThemeColor;
        if (actor.InvulnerabilityTimer > 0f)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(actor.InvulnerabilityTimer * 24f);
            renderColor = Color.Lerp(renderColor, Color.Gold, 0.6f + pulse * 0.4f);
            batcher.DrawCircleOutline(spriteBatch, pos, actor.Radius + 7f, Color.Gold * (0.4f + pulse * 0.6f), 2.5f);
        }
        Color limbBlack = new(10, 12, 16);
        Color limbRim = new(44, 50, 66, 150);
        Color outlineColor = new(14, 18, 28, 235);
        float sizeRatio = actor.Radius / Actor.BaseRadius;
        float aimSign = currentAim.X >= 0f ? 1f : -1f;
        float moveDir = anim.VisualSpeedFactor > 0.08f ? anim.VisualMoveDir : aimSign;

        float walkBobY = (body.IsGrounded || MathF.Abs(body.Velocity.Y) < 10f)
            ? -MathF.Abs(MathF.Sin(anim.WalkCyclePhase)) * 7.0f * sizeRatio * anim.VisualSpeedFactor
            : 0f;
        float visualBodyRadius = actor.Radius * 0.78f;

        // 躯干物理中心：叠加上半身独立后坐位移（射击时后退并自动回正）
        Vector2 bodyPos = pos + anim.TorsoRecoilOffset + new Vector2(0f, -actor.Radius * 0.22f + walkBobY);

        // 固定手脚竹节长度
        float legFixedLen = 14.5f * sizeRatio;
        float armFixedLen = 11.5f * sizeRatio;
        float limbStartW = 3.0f * sizeRatio;
        float limbEndW = 2.2f * sizeRatio;

        // 1. 绘制身体下边缘锚定的黑色定长竹节双腿（高处落地压弯与静摩擦步态）
        Vector2 leftLegRoot = GetDeformedPerimeterPoint(actor, bodyPos, visualBodyRadius * 0.96f, MathHelper.PiOver2 + 0.34f);
        Vector2 rightLegRoot = GetDeformedPerimeterPoint(actor, bodyPos, visualBodyRadius * 0.96f, MathHelper.PiOver2 - 0.34f);

        float strideSwing0 = MathF.Sin(anim.WalkCyclePhase) * 0.45f * anim.VisualSpeedFactor * moveDir;
        float strideSwing1 = MathF.Sin(anim.WalkCyclePhase + MathHelper.Pi) * 0.45f * anim.VisualSpeedFactor * moveDir;
        float airTilt = (!body.IsGrounded && MathF.Abs(body.Velocity.Y) > 10f)
            ? MathHelper.Clamp(body.Velocity.X * 0.0007f, -0.35f, 0.35f)
            : 0f;

        float leftLegBaseAngle = MathHelper.PiOver2 + 0.12f * (1f - anim.VisualSpeedFactor) + strideSwing0 + airTilt;
        float rightLegBaseAngle = MathHelper.PiOver2 - 0.12f * (1f - anim.VisualSpeedFactor) + strideSwing1 + airTilt;

        batcher.DrawFixedLengthBambooLimb(spriteBatch, leftLegRoot, legFixedLen, leftLegBaseAngle, anim.GetLegBendAngle(0), limbStartW, limbEndW, limbBlack, limbRim, 0.9f);
        batcher.DrawFixedLengthBambooLimb(spriteBatch, rightLegRoot, legFixedLen, rightLegBaseAngle, anim.GetLegBendAngle(1), limbStartW, limbEndW, limbBlack, limbRim, 0.9f);

        // 2. 绘制身体后侧边缘锚定的黑色定长竹节副手
        float offEdgeAngle = aimSign >= 0f ? (MathHelper.Pi - 0.20f) : 0.20f;
        Vector2 offArmRoot = GetDeformedPerimeterPoint(actor, bodyPos, visualBodyRadius * 0.96f, offEdgeAngle);
        float offSwing = MathF.Sin(anim.WalkCyclePhase + MathHelper.Pi) * 0.38f * anim.VisualSpeedFactor;
        float offBaseAngle = (aimSign >= 0f ? (MathHelper.Pi - 0.55f) : 0.55f) + offSwing;

        batcher.DrawFixedLengthBambooLimb(spriteBatch, offArmRoot, armFixedLen, offBaseAngle, anim.OffArmBendAngle, 2.8f * sizeRatio, 2.0f * sizeRatio, limbBlack, limbRim, 0.9f);

        // 3. 绘制角色弹性球形身体（含 Squash & Stretch、高光与轮廓线）
        batcher.DrawDeformedCircle(
            spriteBatch,
            bodyPos,
            visualBodyRadius,
            actor.Squash.CurrentScale,
            actor.Squash.DeformationAngle,
            renderColor
        );

        Vector2 highlightPos = bodyPos + new Vector2(-visualBodyRadius * 0.22f, -visualBodyRadius * 0.28f);
        batcher.DrawDeformedCircle(
            spriteBatch,
            highlightPos,
            visualBodyRadius * 0.40f,
            actor.Squash.CurrentScale * new Vector2(0.95f, 0.65f),
            actor.Squash.DeformationAngle - 0.3f,
            Color.White * 0.22f
        );

        batcher.DrawDeformedCircleOutline(
            spriteBatch,
            bodyPos,
            visualBodyRadius,
            actor.Squash.CurrentScale,
            actor.Squash.DeformationAngle,
            outlineColor,
            2.8f
        );

        // 4. 委托面部渲染器绘制五官与眼神
        ActorFaceRenderer.DrawEyesAndExpression(actor, batcher, spriteBatch, bodyPos, visualBodyRadius, sizeRatio, renderColor, outlineColor);

        // 5. 绘制身体前侧边缘锚定的黑色定长竹节主手与矢量枪械
        float aimAngle = MathF.Atan2(currentAim.Y, currentAim.X);
        float mainEdgeAngle = aimAngle + aimSign * 0.18f;
        Vector2 mainArmRoot = GetDeformedPerimeterPoint(actor, bodyPos, visualBodyRadius * 0.96f, mainEdgeAngle);

        float recoilAngleOffset = -aimSign * MathHelper.Clamp(anim.HandRecoilSpring * 0.045f, -0.45f, 0.45f);
        Vector2 handTipPos = batcher.DrawFixedLengthBambooLimb(
            spriteBatch,
            mainArmRoot,
            armFixedLen,
            aimAngle + recoilAngleOffset,
            anim.MainArmBendAngle,
            2.8f * sizeRatio,
            2.0f * sizeRatio,
            limbBlack,
            limbRim,
            0.9f
        );

        DrawGun(actor, batcher, spriteBatch, handTipPos, currentAim, aimSign, sizeRatio, outlineColor, limbBlack);

        // 6. 绘制头顶弹匣余量与换弹进度条 + 副武器特效
        DrawAmmoAndReloadIndicator(actor, batcher, spriteBatch, pos, sizeRatio);
        actor.SubWeapon?.Draw(batcher, spriteBatch, actor);
    }

    /// <summary>
    /// 绘制握持在主手末端的矢量枪械（换弹期间保持瞄准朝向不旋转）。
    /// </summary>
    private static void DrawGun(
        Actor actor,
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        Vector2 handTipPos,
        Vector2 gunDir,
        float aimSign,
        float sizeRatio,
        Color outlineColor,
        Color limbBlack)
    {
        Vector2 gunPerp = new(-gunDir.Y, gunDir.X);
        if (aimSign < 0f) gunPerp = -gunPerp;

        // 枪柄 (Grip)
        batcher.DrawLine(spriteBatch, handTipPos, handTipPos - gunPerp * (7.2f * sizeRatio) - gunDir * (2.0f * sizeRatio), new Color(32, 36, 46), 4.4f * sizeRatio);
        // 枪管外框与护木 (Receiver & Barrel)
        Vector2 gunStart = handTipPos - gunDir * (3.8f * sizeRatio);
        Vector2 gunEnd = handTipPos + gunDir * (16.5f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd, outlineColor, 8.0f * sizeRatio);
        batcher.DrawLine(spriteBatch, gunStart, gunEnd - gunDir * (1.8f * sizeRatio), new Color(55, 62, 78), 4.8f * sizeRatio);
        // 枪身主题色能量槽
        Color energyColor = actor.IsReloading ? Color.Gray * 0.5f : actor.ThemeColor;
        batcher.DrawLine(spriteBatch, handTipPos, handTipPos + gunDir * (9.0f * sizeRatio), energyColor, 2.3f * sizeRatio);

        // 黑色手部末端握持圆点
        batcher.DrawFilledCircle(spriteBatch, handTipPos, 2.6f * sizeRatio, limbBlack);
    }

    /// <summary>
    /// 绘制角色头顶的弹匣剩余子弹颗粒或换弹读条。
    /// </summary>
    private static void DrawAmmoAndReloadIndicator(
        Actor actor,
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        Vector2 pos,
        float sizeRatio)
    {
        Vector2 indicatorCenter = pos + new Vector2(0f, -actor.Radius * 1.42f - 10f);

        if (actor.IsReloading)
        {
            float barW = 42f * sizeRatio;
            float barH = 6f;
            float progress = MathHelper.Clamp(1f - (actor.ReloadTimer / actor.ReloadDuration), 0f, 1f);
            Rectangle bgRect = new((int)(indicatorCenter.X - barW * 0.5f), (int)(indicatorCenter.Y - barH * 0.5f), (int)barW, (int)barH);
            Rectangle fillRect = new(bgRect.X, bgRect.Y, (int)(barW * progress), (int)barH);

            batcher.DrawRectangle(spriteBatch, bgRect, Color.Black * 0.7f);
            batcher.DrawRectangle(spriteBatch, fillRect, ColorPalette.PlayerYellow);
            batcher.DrawRectangleOutline(spriteBatch, bgRect, Color.White * 0.8f, 1.2f);
        }
        else
        {
            int displayMax = Math.Min(actor.MaxAmmo, 10);
            float spacing = 9.5f;
            float totalW = (displayMax - 1) * spacing;
            float startX = indicatorCenter.X - totalW * 0.5f;

            for (int i = 0; i < displayMax; i++)
            {
                Vector2 pipPos = new(startX + i * spacing, indicatorCenter.Y);
                bool hasBullet = i < actor.CurrentAmmo;
                if (hasBullet)
                {
                    batcher.DrawFilledCircle(spriteBatch, pipPos, 3.6f, actor.ThemeColor);
                    batcher.DrawFilledCircle(spriteBatch, pipPos, 1.8f, Color.White);
                }
                else
                {
                    batcher.DrawCircleOutline(spriteBatch, pipPos, 3.2f, Color.Gray * 0.45f, 1.2f);
                }
            }
        }
    }

    /// <summary>
    /// 计算果冻形变椭圆身体边缘指定世界角度方向上的精确锚点坐标。
    /// </summary>
    private static Vector2 GetDeformedPerimeterPoint(Actor actor, Vector2 center, float baseRadius, float worldAngle)
    {
        Vector2 scale = actor.Squash.CurrentScale;
        float rot = actor.Squash.DeformationAngle;
        float localAngle = worldAngle - rot;

        float localX = MathF.Cos(localAngle) * baseRadius * scale.X;
        float localY = MathF.Sin(localAngle) * baseRadius * scale.Y;

        float cosR = MathF.Cos(rot);
        float sinR = MathF.Sin(rot);

        return new Vector2(
            center.X + (localX * cosR - localY * sinR),
            center.Y + (localX * sinR + localY * cosR)
        );
    }
}
