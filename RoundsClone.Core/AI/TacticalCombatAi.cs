using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.AI;

/// <summary>
/// 战术对抗型综合 AI 实现。
/// 职责边界：实现 <see cref="IAiBrain"/> 接口，负责弹道线性超前预判、重力抛物线抬枪补偿、障碍寻路跳跃、空间距离拉扯以及威胁子弹反应式防御（格挡/冲刺）。
/// </summary>
public sealed class TacticalCombatAi : IAiBrain
{
    /// <inheritdoc />
    public string Name => "战术专家 AI";

    private float _reactionTimer = 0f;
    private float _jumpCooldown = 0f;

    /// <inheritdoc />
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

        // 2. 距离战术拉扯 (维持 400 ~ 560 像素的最佳交火距离)
        float preferredDistance = 480f;
        float moveDirX;
        if (distToTarget > preferredDistance + 80f)
        {
            moveDirX = deltaToTarget.X > 0 ? 1.0f : -1.0f; // 贴近目标
        }
        else if (distToTarget < preferredDistance - 80f)
        {
            moveDirX = deltaToTarget.X > 0 ? -1.0f : 1.0f; // 拉开距离
        }
        else
        {
            // 处于最佳距离区间，做轻微钟摆步法移动
            moveDirX = MathF.Sin(_reactionTimer * 2f);
        }
        input.MoveVector = new Vector2(moveDirX, 0f);

        // 3. 障碍感知与智能跳跃 / 贴墙跳
        Vector2 forwardRay = new(moveDirX > 0 ? 1f : -1f, 0f);
        var hit = world.Raycast(selfPos + forwardRay * (self.Radius + 4f), forwardRay, 60f, self.Collider);
        if (hit.HasHit && self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            input.IsJumpDown = true;
            _jumpCooldown = 0.5f;
        }

        // 若目标在高台上方且水平距离较近，主动跳跃抢占高点
        if (targetPos.Y < selfPos.Y - 120f && MathF.Abs(deltaToTarget.X) < 320f && self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            input.IsJumpDown = true;
            _jumpCooldown = 0.6f;
        }

        // 贴墙跳机制：身处空中且贴在墙上时，若目标在高处或需要脱离则触发蹬墙跳
        if (self.Body.IsTouchingWall && !self.Body.IsGrounded && _jumpCooldown <= 0f)
        {
            if (targetPos.Y < selfPos.Y || MathF.Abs(deltaToTarget.X) > 200f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.45f;
            }
        }

        // 4. 反射式防御系统 (检测迎面威胁子弹并激活格挡或冲刺)
        bool needDefense = false;
        foreach (var proj in projectiles)
        {
            if (proj.Owner != self && !proj.IsDestroyed)
            {
                Vector2 toSelf = selfPos - proj.Body.Position;
                float distToProj = toSelf.Length();

                if (distToProj < 200f && Vector2.Dot(proj.Body.Velocity, toSelf) > 0f)
                {
                    needDefense = true;
                    break;
                }
            }
        }

        if (needDefense && self.SubWeapon != null && self.SubWeapon.IsReady)
        {
            input.IsSecondaryDown = true;
        }

        // 5. 武器开火判定 (视线无遮挡且处于有效射程内即扣动扳机)
        var losCheck = world.Raycast(selfPos + aimDirection * (self.Radius + 4f), aimDirection, distToTarget, self.Collider);
        bool hasClearShot = !losCheck.HasHit || losCheck.Distance >= distToTarget - 50f;

        if (hasClearShot && distToTarget < 1200f)
        {
            input.IsFireDown = true;
        }

        return input;
    }
}
