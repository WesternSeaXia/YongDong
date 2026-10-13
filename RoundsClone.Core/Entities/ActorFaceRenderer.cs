using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色面部五官与眼神表情渲染器。
/// 职责边界：专门负责根据角色的 <see cref="EyeStyle"/> 与受击状态，程序化绘制横向双眼、瞳孔视线追踪、眉毛、腮红与嘴部表情。
/// </summary>
public static class ActorFaceRenderer
{
    /// <summary>
    /// 绘制角色面部眼神与表情细节。
    /// </summary>
    public static void DrawEyesAndExpression(
        Actor actor,
        ShapeBatcher batcher,
        SpriteBatch spriteBatch,
        Vector2 bodyPos,
        float visualBodyRadius,
        float sizeRatio,
        Color bodyColor,
        Color outlineColor)
    {
        Vector2 currentAim = actor.CurrentAim;
        Vector2 lookShift = new(currentAim.X * visualBodyRadius * 0.30f, currentAim.Y * visualBodyRadius * 0.18f - visualBodyRadius * 0.08f);
        Vector2 faceCenter = bodyPos + lookShift;
        float eyeSpacingX = 6.6f * sizeRatio;
        Vector2 leftEyePos = faceCenter + new Vector2(-eyeSpacingX, 0f);
        Vector2 rightEyePos = faceCenter + new Vector2(eyeSpacingX, 0f);

        // 受击眩晕时统一渲染 X 型眩晕眼
        if (actor.HitFlashTimer > 0f)
        {
            float cross = 3.8f * sizeRatio;
            batcher.DrawLine(spriteBatch, leftEyePos - new Vector2(cross), leftEyePos + new Vector2(cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, leftEyePos + new Vector2(-cross, cross), leftEyePos + new Vector2(cross, -cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, rightEyePos - new Vector2(cross), rightEyePos + new Vector2(cross), outlineColor, 2.4f);
            batcher.DrawLine(spriteBatch, rightEyePos + new Vector2(-cross, cross), rightEyePos + new Vector2(cross, -cross), outlineColor, 2.4f);
            return;
        }

        float leftPersp = currentAim.X < 0f ? 1.08f : 0.92f;
        float rightPersp = currentAim.X >= 0f ? 1.08f : 0.92f;
        float baseEyeR = 4.6f * sizeRatio;

        switch (actor.EyeStyle)
        {
            case EyeStyle.Cute:
                // 水汪萌眼：超大亮瞳 + 双重星光高光 + 粉嫩腮红 + 弯弯小嘴
                DrawCuteEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.24f * leftPersp, currentAim, outlineColor, isLeft: true);
                DrawCuteEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.24f * rightPersp, currentAim, outlineColor, isLeft: false);
                Color blushColor = new Color(255, 115, 160) * 0.62f;
                batcher.DrawDeformedCircle(spriteBatch, leftEyePos + new Vector2(-2.2f * sizeRatio, 5.2f * sizeRatio), 3.4f * sizeRatio, new Vector2(1.4f, 0.75f), 0f, blushColor);
                batcher.DrawDeformedCircle(spriteBatch, rightEyePos + new Vector2(2.2f * sizeRatio, 5.2f * sizeRatio), 3.4f * sizeRatio, new Vector2(1.4f, 0.75f), 0f, blushColor);
                Vector2 cuteMouth = faceCenter + new Vector2(0f, 5.5f * sizeRatio);
                batcher.DrawArc(spriteBatch, cuteMouth + new Vector2(-2.0f * sizeRatio, 0f), 2.0f * sizeRatio, 0.2f, MathHelper.Pi - 0.4f, outlineColor, 1.8f);
                batcher.DrawArc(spriteBatch, cuteMouth + new Vector2(2.0f * sizeRatio, 0f), 2.0f * sizeRatio, 0.2f, MathHelper.Pi - 0.4f, outlineColor, 1.8f);
                break;

            case EyeStyle.Sneaky:
                // 猥琐眯眯眼：半阖眼皮 + 贼溜溜斜视小眼珠 + 挑眉坏笑
                DrawSneakyEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.08f * leftPersp, currentAim, bodyColor, outlineColor, isLeft: true);
                DrawSneakyEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.08f * rightPersp, currentAim, bodyColor, outlineColor, isLeft: false);
                Vector2 smirkCenter = faceCenter + new Vector2(1.5f * sizeRatio, 5.0f * sizeRatio);
                batcher.DrawArc(spriteBatch, smirkCenter, 4.5f * sizeRatio, 0.15f, MathHelper.Pi * 0.65f, outlineColor, 2.2f);
                break;

            case EyeStyle.Derp:
                // 智慧斗鸡眼：一大一小不对称眼眶 + 对眼乱转瞳孔 + 呆萌圆嘴
                float wobble = MathF.Sin(actor.LimbAnimator.IdlePhase * 1.5f + actor.LimbAnimator.WalkCyclePhase) * 0.25f;
                Vector2 derpAimL = MathHelper2D.SafeNormalize(new Vector2(0.75f, -0.35f + wobble));
                Vector2 derpAimR = MathHelper2D.SafeNormalize(new Vector2(-0.80f, 0.35f - wobble));
                DrawClassicEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.32f, derpAimL, outlineColor, isLeft: true, browTiltOverride: -1.5f);
                DrawClassicEye(batcher, spriteBatch, rightEyePos, baseEyeR * 0.78f, derpAimR, outlineColor, isLeft: false, browTiltOverride: -1.8f);
                Vector2 derpMouth = faceCenter + new Vector2(0.5f * sizeRatio, 6.2f * sizeRatio);
                batcher.DrawFilledCircle(spriteBatch, derpMouth, 2.6f * sizeRatio, outlineColor);
                batcher.DrawFilledCircle(spriteBatch, derpMouth, 1.3f * sizeRatio, new Color(220, 80, 95));
                break;

            case EyeStyle.Happy:
                // 腹黑笑眼：弯弯月牙笑眼 (^ ^) + 腮红 + 微笑
                DrawHappyArcEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.05f * leftPersp, outlineColor, sizeRatio);
                DrawHappyArcEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.05f * rightPersp, outlineColor, sizeRatio);
                Color happyBlush = new Color(255, 120, 150) * 0.55f;
                batcher.DrawDeformedCircle(spriteBatch, leftEyePos + new Vector2(-2.0f * sizeRatio, 4.2f * sizeRatio), 3.0f * sizeRatio, new Vector2(1.35f, 0.7f), 0f, happyBlush);
                batcher.DrawDeformedCircle(spriteBatch, rightEyePos + new Vector2(2.0f * sizeRatio, 4.2f * sizeRatio), 3.0f * sizeRatio, new Vector2(1.35f, 0.7f), 0f, happyBlush);
                batcher.DrawArc(spriteBatch, faceCenter + new Vector2(0f, 3.8f * sizeRatio), 3.6f * sizeRatio, 0.25f, MathHelper.Pi - 0.5f, outlineColor, 2.0f);
                break;

            case EyeStyle.Crazy:
                // 狂暴死鱼眼：夸张瞪大眼白 + 针尖小瞳孔 + 浓重黑眼圈与暴躁立眉
                DrawCrazyEye(batcher, spriteBatch, leftEyePos, baseEyeR * 1.18f * leftPersp, currentAim, outlineColor, isLeft: true);
                DrawCrazyEye(batcher, spriteBatch, rightEyePos, baseEyeR * 1.18f * rightPersp, currentAim, outlineColor, isLeft: false);
                break;

            case EyeStyle.Classic:
            default:
                DrawClassicEye(batcher, spriteBatch, leftEyePos, baseEyeR * leftPersp, currentAim, outlineColor, isLeft: true);
                DrawClassicEye(batcher, spriteBatch, rightEyePos, baseEyeR * rightPersp, currentAim, outlineColor, isLeft: false);
                break;
        }
    }

    private static void DrawClassicEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft, float? browTiltOverride = null)
    {
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.4f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, Color.White);

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.36f);
        float pupilR = eyeR * 0.58f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(18, 20, 30));

        Vector2 highlightPos = pupilPos + new Vector2(-pupilR * 0.35f, -pupilR * 0.35f);
        batcher.DrawFilledCircle(spriteBatch, highlightPos, pupilR * 0.36f, Color.White);

        float browTilt = browTiltOverride ?? (isLeft ? 1.6f : -1.6f);
        Vector2 browLeft = eyePos + new Vector2(-eyeR * 0.85f, -eyeR * 1.15f - browTilt);
        Vector2 browRight = eyePos + new Vector2(eyeR * 0.85f, -eyeR * 1.15f + browTilt);
        batcher.DrawLine(spriteBatch, browLeft, browRight, outlineColor, 2.2f);
    }

    private static void DrawCuteEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft)
    {
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.5f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, Color.White);

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.25f);
        float pupilR = eyeR * 0.74f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(22, 24, 42));

        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(-pupilR * 0.36f, -pupilR * 0.36f), pupilR * 0.42f, Color.White);
        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(pupilR * 0.32f, pupilR * 0.32f), pupilR * 0.20f, Color.White);

        float lashDir = isLeft ? -1f : 1f;
        Vector2 lashStart = eyePos + new Vector2(lashDir * eyeR * 0.85f, -eyeR * 0.45f);
        batcher.DrawLine(spriteBatch, lashStart, lashStart + new Vector2(lashDir * 2.8f, -2.2f), outlineColor, 2.0f);
    }

    private static void DrawSneakyEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color bodyColor, Color outlineColor, bool isLeft)
    {
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.2f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, new Color(245, 245, 230));

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.48f);
        float pupilR = eyeR * 0.46f;
        batcher.DrawFilledCircle(spriteBatch, pupilPos, pupilR, new Color(18, 20, 30));
        batcher.DrawFilledCircle(spriteBatch, pupilPos + new Vector2(-pupilR * 0.3f, -pupilR * 0.3f), pupilR * 0.32f, Color.White);

        Rectangle topLid = new((int)(eyePos.X - eyeR - 2f), (int)(eyePos.Y - eyeR - 2f), (int)((eyeR + 2f) * 2f), (int)(eyeR * 0.95f));
        Rectangle botLid = new((int)(eyePos.X - eyeR - 2f), (int)(eyePos.Y + eyeR * 0.32f), (int)((eyeR + 2f) * 2f), (int)(eyeR * 0.90f));
        batcher.DrawRectangle(spriteBatch, topLid, bodyColor);
        batcher.DrawRectangle(spriteBatch, botLid, bodyColor);

        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 1.15f, -eyeR * 0.12f), eyePos + new Vector2(eyeR * 1.15f, -eyeR * 0.12f), outlineColor, 2.4f);
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 1.05f, eyeR * 0.35f), eyePos + new Vector2(eyeR * 1.05f, eyeR * 0.35f), outlineColor, 2.0f);

        float browY = isLeft ? -eyeR * 1.25f : -eyeR * 0.95f;
        float tilt = isLeft ? -1.5f : 1.8f;
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 0.9f, browY - tilt), eyePos + new Vector2(eyeR * 0.9f, browY + tilt), outlineColor, 2.2f);
    }

    private static void DrawHappyArcEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Color outlineColor, float sizeRatio)
    {
        Vector2 arcCenter = eyePos + new Vector2(0f, eyeR * 0.35f);
        batcher.DrawArc(spriteBatch, arcCenter, eyeR * 0.90f, MathHelper.Pi + 0.35f, MathHelper.Pi - 0.70f, outlineColor, 3.0f * sizeRatio);
    }

    private static void DrawCrazyEye(ShapeBatcher batcher, SpriteBatch spriteBatch, Vector2 eyePos, float eyeR, Vector2 aim, Color outlineColor, bool isLeft)
    {
        batcher.DrawArc(spriteBatch, eyePos + new Vector2(0f, 1.2f), eyeR + 2.2f, 0.25f, MathHelper.Pi - 0.5f, outlineColor * 0.65f, 2.2f);

        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR + 1.5f, outlineColor);
        batcher.DrawFilledCircle(spriteBatch, eyePos, eyeR, new Color(255, 248, 245));

        Vector2 pupilPos = eyePos + aim * (eyeR * 0.32f);
        batcher.DrawFilledCircle(spriteBatch, pupilPos, eyeR * 0.36f, new Color(220, 50, 50));
        batcher.DrawFilledCircle(spriteBatch, pupilPos, eyeR * 0.20f, new Color(15, 15, 22));

        float browTilt = isLeft ? 2.8f : -2.8f;
        batcher.DrawLine(spriteBatch, eyePos + new Vector2(-eyeR * 0.95f, -eyeR * 1.05f - browTilt), eyePos + new Vector2(eyeR * 0.95f, -eyeR * 1.05f + browTilt), outlineColor, 2.6f);
    }
}
