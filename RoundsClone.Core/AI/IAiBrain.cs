using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.AI;

/// <summary>
/// AI 行为大脑决策接口。
/// 解耦 AI 算法与具体的 Actor 身体，支持未来插拔不同性格风格的敌人智能。
/// </summary>
public interface IAiBrain
{
    /// <summary>
    /// AI 风格标识名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 思考并生成当前帧的控制意图输入。
    /// </summary>
    /// <param name="self">AI 自身实体</param>
    /// <param name="target">对抗目标实体 (玩家)</param>
    /// <param name="projectiles">全场活跃子弹列表</param>
    /// <param name="world">物理碰撞世界</param>
    /// <param name="dt">时间步长</param>
    /// <returns>计算出的控制操作意图</returns>
    ControlInputState Think(Actor self, Actor target, IReadOnlyList<Projectile> projectiles, CollisionWorld world, float dt);
}

/// <summary>
/// 战术对抗型综合 AI 实现。
/// 具备弹道线性超前预判、障碍寻路跳跃、空间距离拉扯控制以及子弹来袭反应式防御（精准格挡/冲刺闪避）。
/// </summary>
public class TacticalCombatAi : IAiBrain
{
    public string Name => "战术专家 AI";

    private float _reactionTimer = 0f;
    private float _jumpCooldown = 0f;
    private readonly Random _rand = new();

    public ControlInputState Think(Actor self, Actor target, IReadOnlyList<Projectile> projectiles, CollisionWorld world, float dt)
    {
        ControlInputState input = default;
        if (self.IsDead || target.IsDead) return input;

        _reactionTimer += dt;
        if (_jumpCooldown > 0f) _jumpCooldown -= dt;

        Vector2 selfPos = self.Body.Position;
        Vector2 targetPos = target.Body.Position;
        Vector2 deltaToTarget = targetPos - selfPos;
        float distToTarget = deltaToTarget.Length();

        // 1. 预判瞄准与重力抛物线抬枪补偿 (Lead & Ballistic Drop Compensation)
        float bulletSpeed = MathF.Max(self.BulletSpeed, 600f);
        float flightTime = distToTarget / bulletSpeed;
        float bulletGravity = world.Gravity.Y * 0.56f;
        Vector2 dropCompensation = new(0f, -0.5f * bulletGravity * flightTime * flightTime * 0.8f);
        Vector2 predictedTargetPos = targetPos + target.Body.Velocity * (flightTime * 0.70f) + dropCompensation;
        Vector2 aimDirection = MathHelper2D.SafeNormalize(predictedTargetPos - selfPos);
        input.AimVector = aimDirection;

        // 2. 距离战术拉扯 (维持 400 ~ 650 像素的最佳交火距离)
        float preferredDistance = 480f;
        float moveDirX = 0f;
        if (distToTarget > preferredDistance + 80f)
        {
            moveDirX = deltaToTarget.X > 0 ? 1.0f : -1.0f; // 贴近
        }
        else if (distToTarget < preferredDistance - 80f)
        {
            moveDirX = deltaToTarget.X > 0 ? -1.0f : 1.0f; // 远离拉开
        }
        else
        {
            // 处于最佳距离，做轻微钟摆随机步法
            moveDirX = MathF.Sin((float)_reactionTimer * 2f);
        }
        input.MoveVector = new Vector2(moveDirX, 0f);

        // 3. 障碍感知与智能跳跃 / 贴墙跳
        // 前方发射射线探测是否有高墙或障碍物
        Vector2 forwardRay = new Vector2(moveDirX > 0 ? 1f : -1f, 0f);
        var hit = world.Raycast(selfPos, forwardRay, 60f);
        if (hit.HasHit && self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            input.IsJumpDown = true;
            _jumpCooldown = 0.5f;
        }
        // 若目标在高台上方且水平距离近，也尝试跳跃攻击
        if (targetPos.Y < selfPos.Y - 120f && MathF.Abs(deltaToTarget.X) < 320f && self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            input.IsJumpDown = true;
            _jumpCooldown = 0.6f;
        }
        // 贴墙跳机制：若身处空中且贴在墙上，当目标在高处或需要脱离时机敏蹬墙跳
        if (self.Body.IsTouchingWall && !self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            if (targetPos.Y < selfPos.Y || MathF.Abs(deltaToTarget.X) > 200f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.45f;
            }
        }

        // 4. 反射式防御系统 (检测威胁子弹并激活格挡或冲刺)
        bool needDefense = false;
        foreach (var proj in projectiles)
        {
            if (proj.Owner != self && !proj.IsDestroyed)
            {
                Vector2 toSelf = selfPos - proj.Body.Position;
                float distToProj = toSelf.Length();

                // 子弹在近身 180 像素内且速度方向朝向自己
                if (distToProj < 200f)
                {
                    float dot = Vector2.Dot(proj.Body.Velocity, toSelf);
                    if (dot > 0f) // 正在迎面靠近
                    {
                        needDefense = true;
                        break;
                    }
                }
            }
        }

        if (needDefense && self.SubWeapon != null && self.SubWeapon.IsReady)
        {
            input.IsSecondaryDown = true;
        }

        // 5. 武器开火判定
        // 当准星与目标夹角小于阈值，且视线无不可穿透障碍物时开火
        var losCheck = world.Raycast(selfPos, aimDirection, distToTarget);
        bool hasClearShot = !losCheck.HasHit || losCheck.Distance >= distToTarget - 40f;

        if (hasClearShot && distToTarget < 1200f)
        {
            // 准星朝向良好即扣动扳机
            input.IsFireDown = true;
        }

        return input;
    }
}
