using System;
using System.Collections.Generic;
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

        // 换弹日冕 (Radiance)：换弹期间角色身后投射宏伟太阳虚影与 150px 范围光环
        if (actor.IsReloading && actor.HasRadiance)
        {
            DrawRadianceSunPhantom(actor, batcher, spriteBatch);
        }

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

        // 极寒冰冻 (Frost)：减速期间在身体表面凝结晶莹多棱双锥体蓝色冰晶
        if (actor.SlowTimer > 0f && actor.FrostCrystals.Count > 0)
        {
            DrawFrostIceCrystals(batcher, spriteBatch, bodyPos, visualBodyRadius, actor.FrostCrystals);
        }

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

    /// <summary>
    /// 绘制换弹日冕 (Radiance) 持续期间的太阳虚影与攻击杀伤范围 (150px 判定范围)。
    /// 外观：中央圆形主体 + 16 个向外突出的三角形齿芒（经典太阳/日珥徽标形态）。
    /// 动效：换弹初期 0.35s 由小到大平滑展开至 150px 半径；展开完毕后才开始产生攻击判定；全程无闪烁。
    /// </summary>
    private static void DrawRadianceSunPhantom(Actor actor, ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        Vector2 pos = actor.Body.Position;
        const float attackRadius = 150f;      // 攻击判定最大半径（对应三角形外顶点）
        const float circleRadius = 118f;      // 中心主圆形半径
        const float expandDuration = 0.35f;   // 刚开始由小到大展开时长

        // 计算换弹已过去时间与由小到大的展开缩放值（Ease-Out 平滑曲线）
        float elapsedReload = MathF.Max(0f, actor.ReloadDuration - actor.ReloadTimer);
        float progress = MathHelper.Clamp(elapsedReload / expandDuration, 0f, 1f);
        float scale = 1.0f - MathF.Pow(1.0f - progress, 3f);

        if (scale <= 0.001f) return;

        float currentAttackRadius = attackRadius * scale;
        float currentCircleRadius = circleRadius * scale;
        float currentCoreRadius = 50f * scale;

        // 平稳匀速慢速旋转，无闪烁
        float rotAngle = elapsedReload * 0.8f;

        // 纯正高对比度暖金与金黄色彩，无任何周期性闪烁或透明度脉动
        Color circleFillColor = new Color(255, 185, 45) * 0.22f;
        Color circleOutlineColor = new Color(255, 215, 65) * 0.85f;
        Color coreFillColor = new Color(255, 230, 110) * 0.28f;
        Color coreOutlineColor = new Color(255, 240, 130) * 0.60f;
        Color triangleFillColor = new Color(255, 195, 45) * 0.38f;
        Color triangleOutlineColor = new Color(255, 220, 60) * 0.88f;

        // 1. 绘制 16 个向外突出的太阳光芒三角形 (Triangles)
        const int triangleCount = 16;
        float step = MathHelper.TwoPi / triangleCount;
        float halfBaseAngle = step * 0.38f;
        Span<Vector2> triVerts = stackalloc Vector2[3];

        for (int i = 0; i < triangleCount; i++)
        {
            float centerAngle = i * step + rotAngle;
            float cosC = MathF.Cos(centerAngle);
            float sinC = MathF.Sin(centerAngle);

            // 外顶点（精确延伸至 150px 判定范围极限）
            Vector2 tipPos = pos + new Vector2(cosC, sinC) * currentAttackRadius;

            // 内基底两个端点（坐落在中心圆周上）
            float leftAngle = centerAngle - halfBaseAngle;
            float rightAngle = centerAngle + halfBaseAngle;
            Vector2 leftBasePos = pos + new Vector2(MathF.Cos(leftAngle), MathF.Sin(leftAngle)) * currentCircleRadius;
            Vector2 rightBasePos = pos + new Vector2(MathF.Cos(rightAngle), MathF.Sin(rightAngle)) * currentCircleRadius;

            triVerts[0] = tipPos;
            triVerts[1] = rightBasePos;
            triVerts[2] = leftBasePos;

            batcher.DrawFilledConvexPolygon(spriteBatch, triVerts, triangleFillColor);
            batcher.DrawPolygonOutline(spriteBatch, triVerts, triangleOutlineColor, 2.0f);
        }

        // 2. 绘制中心大圆形主体
        batcher.DrawFilledCircle(spriteBatch, pos, currentCircleRadius, circleFillColor);
        batcher.DrawCircleOutline(spriteBatch, pos, currentCircleRadius, circleOutlineColor, 2.5f);

        // 3. 绘制内层高能太阳核心圆
        batcher.DrawFilledCircle(spriteBatch, pos, currentCoreRadius, coreFillColor);
        batcher.DrawCircleOutline(spriteBatch, pos, currentCoreRadius, coreOutlineColor, 1.5f);

        // 4. 展开完成后，随脉冲向外辐射的冲击波环圈 (0.28s 周期)
        if (progress >= 1.0f && actor.RadianceTimer > 0f)
        {
            float pulseWave = MathHelper.Clamp(actor.RadianceTimer / 0.28f, 0f, 1f);
            float waveRadius = currentCircleRadius + (currentAttackRadius - currentCircleRadius) * pulseWave;
            Color waveColor = new Color(255, 235, 120) * (0.65f * (1f - pulseWave));
            batcher.DrawCircleOutline(spriteBatch, pos, waveRadius, waveColor, 1.8f);
        }
    }

    /// <summary>
    /// 绘制减速状态下的极寒晶莹蓝色冰晶（三维多棱柱体，顶面与底面均为多棱锥，随机附着在敌人身体上）。
    /// </summary>
    private static void DrawFrostIceCrystals(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 bodyPos, float bodyRadius, IReadOnlyList<FrostCrystalData> crystals)
    {
        if (crystals == null || crystals.Count == 0) return;

        Color leftFacetColor = new Color(40, 130, 225, 240);  // 受光面深邃蔚蓝
        Color rightFacetColor = new Color(15, 65, 155, 245);  // 背光面冷暗深蓝
        Color rimOutlineColor = new Color(8, 28, 75, 255);    // 外轮廓深邃午夜蓝晶界线
        Color ringFacetColor = new Color(75, 155, 245, 210);  // 棱柱顶底截面环线
        Color crestRidgeColor = new Color(185, 225, 255, 250); // 纵贯中轴折射冰晶浅蓝高光棱脊

        Span<Vector2> triVerts = stackalloc Vector2[3];
        Span<Vector2> quadVerts = stackalloc Vector2[4];
        Span<Vector2> contourVerts = stackalloc Vector2[6];

        for (int i = 0; i < crystals.Count; i++)
        {
            var crystal = crystals[i];
            Vector2 c = bodyPos + crystal.RelativeOffset;
            float cos = MathF.Cos(crystal.Angle);
            float sin = MathF.Sin(crystal.Angle);
            Vector2 u = new(cos, sin);     // 棱柱主轴方向
            Vector2 v = new(-sin, cos);    // 棱柱法向（横向）

            float halfLen = crystal.PrismLength * 0.5f;
            float halfW = crystal.PrismWidth * 0.5f;
            float pyrH = crystal.PyramidHeight;

            // 顶面与底面尖顶
            Vector2 topApex = c + u * (halfLen + pyrH);
            Vector2 bottomApex = c - u * (halfLen + pyrH);

            // 棱柱体顶面多边形特征点
            Vector2 topMid = c + u * halfLen;
            Vector2 topLeft = c + u * (halfLen * 0.88f) - v * halfW;
            Vector2 topRight = c + u * (halfLen * 0.88f) + v * halfW;

            // 棱柱体底面多边形特征点
            Vector2 bottomMid = c - u * halfLen;
            Vector2 bottomLeft = c - u * (halfLen * 0.88f) - v * halfW;
            Vector2 bottomRight = c - u * (halfLen * 0.88f) + v * halfW;

            // 1. 绘制左侧受光面 (Light Facets)
            // 顶锥左侧三角形: topApex -> topLeft -> topMid
            triVerts[0] = topApex;
            triVerts[1] = topLeft;
            triVerts[2] = topMid;
            batcher.DrawFilledConvexPolygon(spriteBatch, triVerts, leftFacetColor);

            // 棱柱左侧四边形: topMid -> topLeft -> bottomLeft -> bottomMid
            quadVerts[0] = topMid;
            quadVerts[1] = topLeft;
            quadVerts[2] = bottomLeft;
            quadVerts[3] = bottomMid;
            batcher.DrawFilledConvexPolygon(spriteBatch, quadVerts, leftFacetColor);

            // 底锥左侧三角形: bottomApex -> bottomMid -> bottomLeft
            triVerts[0] = bottomApex;
            triVerts[1] = bottomMid;
            triVerts[2] = bottomLeft;
            batcher.DrawFilledConvexPolygon(spriteBatch, triVerts, leftFacetColor);

            // 2. 绘制右侧背光面 (Shadow Facets)
            // 顶锥右侧三角形: topApex -> topMid -> topRight
            triVerts[0] = topApex;
            triVerts[1] = topMid;
            triVerts[2] = topRight;
            batcher.DrawFilledConvexPolygon(spriteBatch, triVerts, rightFacetColor);

            // 棱柱右侧四边形: topMid -> bottomMid -> bottomRight -> topRight
            quadVerts[0] = topMid;
            quadVerts[1] = bottomMid;
            quadVerts[2] = bottomRight;
            quadVerts[3] = topRight;
            batcher.DrawFilledConvexPolygon(spriteBatch, quadVerts, rightFacetColor);

            // 底锥右侧三角形: bottomApex -> bottomRight -> bottomMid
            triVerts[0] = bottomApex;
            triVerts[1] = bottomRight;
            triVerts[2] = bottomMid;
            batcher.DrawFilledConvexPolygon(spriteBatch, triVerts, rightFacetColor);

            // 3. 绘制外轮廓深蓝晶界线
            contourVerts[0] = topApex;
            contourVerts[1] = topRight;
            contourVerts[2] = bottomRight;
            contourVerts[3] = bottomApex;
            contourVerts[4] = bottomLeft;
            contourVerts[5] = topLeft;
            batcher.DrawPolygonOutline(spriteBatch, contourVerts, rimOutlineColor, 1.8f);

            // 4. 绘制棱柱体与顶底双锥的截面交界线
            batcher.DrawLine(spriteBatch, topLeft, topMid, ringFacetColor, 1.4f);
            batcher.DrawLine(spriteBatch, topMid, topRight, ringFacetColor, 1.4f);
            batcher.DrawLine(spriteBatch, bottomLeft, bottomMid, ringFacetColor, 1.4f);
            batcher.DrawLine(spriteBatch, bottomMid, bottomRight, ringFacetColor, 1.4f);

            // 5. 绘制贯穿顶底尖端的折射棱脊线 (Crest Ridge)
            batcher.DrawLine(spriteBatch, topApex, topMid, crestRidgeColor, 1.8f);
            batcher.DrawLine(spriteBatch, topMid, bottomMid, crestRidgeColor, 1.8f);
            batcher.DrawLine(spriteBatch, bottomMid, bottomApex, crestRidgeColor, 1.8f);
        }

        // 身体表面微弱极寒冰霜光晕轮廓
        batcher.DrawCircleOutline(spriteBatch, bodyPos, bodyRadius + 2.5f, new Color(30, 95, 205) * 0.55f, 2.0f);
    }
}
