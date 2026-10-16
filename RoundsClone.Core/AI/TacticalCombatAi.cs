using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Maps;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.AI;

/// <summary>
/// 战术对抗型综合智能 AI（升级版）。
/// 具备高智商环境感知能力：
/// 1. 尖刺深渊探针与绝对避险：绝不肉身撞刺，接近刺坑前主动急刹、跳跃越障或退守高地；
/// 2. 滞空遇险极限自救：被击飞至尖刺上方时，紧急向安全平台滑行并触发贴墙蹬跳与副武器脱困；
/// 3. 移动平台搭乘与通勤：感知横向穿梭平台，等待平台接驳跨越深渊；
/// 4. 动态吊箱掩体与反弹利用：借助悬挂集装箱阻挡敌方弹道；
/// 5. 极寒冰面战术滑行：低摩擦下的间歇脉冲身法射击；
/// 6. 弹道精准预判与高抛抬枪补偿：结合引力抛物线和移动提前量实现精准压制。
/// </summary>
public sealed class TacticalCombatAi : IAiBrain
{
    /// <inheritdoc />
    public string Name => "高智商战术特工 AI";

    private float _reactionTimer = 0f;
    private float _jumpCooldown = 0f;
    private float _strafeTimer = 0f;
    private float _strafeDir = 1f;
    private Vector2 _lastPos = Vector2.Zero;
    private float _stuckTimer = 0f;

    /// <inheritdoc />
    public ControlInputState Think(Actor self, Actor target, IReadOnlyList<Projectile> projectiles, CollisionWorld world, MapData map, float dt)
    {
        ControlInputState input = default;
        if (self.IsDead || target.IsDead) return input;

        _reactionTimer += dt;
        if (_jumpCooldown > 0f) _jumpCooldown -= dt;

        Vector2 selfPos = self.Body.Position;
        Vector2 targetPos = target.Body.Position;
        Vector2 deltaToTarget = targetPos - selfPos;
        float distToTarget = deltaToTarget.Length();

        // 卡位防呆计时器
        if (Vector2.DistanceSquared(selfPos, _lastPos) < 4f && self.Body.IsGrounded)
        {
            _stuckTimer += dt;
        }
        else
        {
            _stuckTimer = 0f;
            _lastPos = selfPos;
        }

        // 随机身法换向计时器
        _strafeTimer -= dt;
        if (_strafeTimer <= 0f)
        {
            _strafeDir = (Random.Shared.NextDouble() > 0.5) ? 1f : -1f;
            _strafeTimer = 0.8f + (float)Random.Shared.NextDouble() * 1.2f;
        }

        // -----------------------------------------------------------------
        // 1. 地图环境与危险尖刺深渊感知
        // -----------------------------------------------------------------
        var hazards = map.TerrainObjects
            .Where(t => t.Style == TerrainStyle.HazardSpike && !t.IsDestroyed)
            .Select(t => t.Collider.Bounds)
            .ToList();

        // 判定当前是否正处于尖刺坑的垂直危险空域上方
        bool isDirectlyOverHazard = false;
        Rectangle activeHazard = default;
        foreach (var hazard in hazards)
        {
            if (selfPos.X >= hazard.Left - 15f && selfPos.X <= hazard.Right + 15f && selfPos.Y <= hazard.Bottom + 10f)
            {
                isDirectlyOverHazard = true;
                activeHazard = hazard;
                break;
            }
        }

        // -----------------------------------------------------------------
        // 2. 危险空域【极限紧急自救】(Emergency Hazard Recovery)
        // -----------------------------------------------------------------
        bool isExecutingEmergencyEscape = false;
        if (isDirectlyOverHazard && selfPos.Y > activeHazard.Top - 300f)
        {
            isExecutingEmergencyEscape = true;

            // 计算逃向左侧安全边缘还是右侧安全边缘
            float distToLeftSafe = MathF.Abs(selfPos.X - activeHazard.Left);
            float distToRightSafe = MathF.Abs(selfPos.X - activeHazard.Right);
            float escapeDirX = (distToLeftSafe < distToRightSafe) ? -1.0f : 1.0f;

            // 如果身陷深渊贴墙或贴在悬挂箱上，立刻触发蹬墙跳绝壁脱困自救！
            if (self.Body.IsTouchingWall && _jumpCooldown <= 0f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.22f;
            }
            else
            {
                // 全力朝最近的安全边缘施加推力
                input.MoveVector = new Vector2(escapeDirX, 0f);

                if ((self.Body.IsGrounded || (self.RemainingJumps > 0 && self.Body.Velocity.Y > 0f)) && _jumpCooldown <= 0f)
                {
                    // 如果落入刺中或坠向尖刺，空中/地面全力起跳脱离！
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.20f;
                }
            }

            // 如果副武器（冲刺/格挡）就绪，紧急朝安全方向冲刺！
            if (self.SubWeapon != null && self.SubWeapon.IsReady)
            {
                input.IsSecondaryDown = true;
            }
        }

        // -----------------------------------------------------------------
        // 3. 常规移动与战术走位决策 (包含地面防坠落探针、悬挂箱抓附攀爬与移动平台搭乘)
        // -----------------------------------------------------------------
        if (!isExecutingEmergencyEscape)
        {
            // 3.1 寻找最近的移动平台与悬挂物理箱
            MovingTerrain? nearestMoving = null;
            float minDistMoving = float.MaxValue;
            BoxTerrain? nearestHangingBox = null;
            float minDistHanging = float.MaxValue;

            foreach (var t in map.TerrainObjects)
            {
                if (t is MovingTerrain m && !m.IsDestroyed)
                {
                    float d = Vector2.Distance(selfPos, m.Body.Position);
                    if (d < minDistMoving)
                    {
                        minDistMoving = d;
                        nearestMoving = m;
                    }
                }
                else if (t is BoxTerrain b && b.AttachedRope != null && !b.IsDestroyed)
                {
                    float d = Vector2.Distance(selfPos, b.Body.Position);
                    if (d < minDistHanging)
                    {
                        minDistHanging = d;
                        nearestHangingBox = b;
                    }
                }
            }

            // 检查当前是否稳稳站在移动平台上
            bool isRidingPlatform = false;
            if (self.Body.IsGrounded && nearestMoving != null)
            {
                float dx = MathF.Abs(selfPos.X - nearestMoving.Body.Position.X);
                float dy = selfPos.Y - (nearestMoving.Body.Position.Y - nearestMoving.BoxCol.Size.Y * 0.5f);
                if (dx < nearestMoving.BoxCol.Size.X * 0.5f + 12f && dy >= -8f && dy < 35f)
                {
                    isRidingPlatform = true;
                }
            }

            // 检查当前是否稳稳站在悬挂箱顶部
            bool isRidingHangingBox = false;
            if (self.Body.IsGrounded && nearestHangingBox != null)
            {
                float dx = MathF.Abs(selfPos.X - nearestHangingBox.Body.Position.X);
                float dy = selfPos.Y - (nearestHangingBox.Body.Position.Y - nearestHangingBox.BoxCol.Size.Y * 0.5f);
                if (dx < nearestHangingBox.BoxCol.Size.X * 0.5f + 18f && dy >= -8f && dy < 35f)
                {
                    isRidingHangingBox = true;
                }
            }

            // 3.2 意图移动方向
            float desiredMoveX;
            float preferredDistance = 450f;

            if (isRidingHangingBox)
            {
                // 正立于深渊上方的悬挂箱顶：向前飞跃跨越深渊！
                desiredMoveX = deltaToTarget.X > 0 ? 1.0f : -1.0f;
                if (_jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.50f;
                }
            }
            else if (isRidingPlatform)
            {
                // 已经在移动平台上搭乘通勤：
                // 保持平衡平稳瞄准，若平台已经把自身运到了目标侧则从平台走下
                float platformDeltaTargetX = deltaToTarget.X;
                if (MathF.Abs(platformDeltaTargetX) < 180f)
                {
                    desiredMoveX = platformDeltaTargetX > 0 ? 1.0f : -1.0f; // 下船上岸
                }
                else
                {
                    desiredMoveX = 0f; // 稳住身形让平台载客
                }
            }
            else
            {
                // 距离拉扯逻辑
                if (distToTarget > preferredDistance + 70f)
                {
                    desiredMoveX = deltaToTarget.X > 0 ? 1.0f : -1.0f; // 接近目标
                }
                else if (distToTarget < preferredDistance - 70f)
                {
                    desiredMoveX = deltaToTarget.X > 0 ? -1.0f : 1.0f; // 拉开距离
                }
                else
                {
                    desiredMoveX = _strafeDir * 0.7f; // 钟摆身法
                }

                // 3.3 【前瞻步法探针 (Footing Safety Probe)】：检测前方地面是否通向尖刺！
                if (self.Body.IsGrounded && MathF.Abs(desiredMoveX) > 0.1f)
                {
                    float probeSign = desiredMoveX > 0 ? 1.0f : -1.0f;
                    Vector2 probePos = selfPos + new Vector2(probeSign * 50f, 0f);

                    bool hazardAhead = false;
                    foreach (var h in hazards)
                    {
                        // 如果前进方向正前方 50~90 像素会落入尖刺坑 X 范围
                        if (probePos.X >= h.Left - 10f && probePos.X <= h.Right + 10f && selfPos.Y >= h.Top - 180f)
                        {
                            hazardAhead = true;
                            break;
                        }
                    }

                    if (hazardAhead)
                    {
                        // 前方有尖刺深渊！
                        // 判定能不能借力过坑：是否有移动平台或悬挂箱正好在附近可供起跳抓附攀爬？
                        bool canBoardPlatform = false;
                        if (nearestMoving != null)
                        {
                            Vector2 mPos = nearestMoving.Body.Position;
                            float toPlatformX = mPos.X - selfPos.X;
                            if (MathF.Sign(toPlatformX) == MathF.Sign(probeSign) && MathF.Abs(toPlatformX) < 140f && MathF.Abs(mPos.Y - selfPos.Y) < 100f)
                            {
                                canBoardPlatform = true;
                            }
                        }

                        bool canReachHangingBox = false;
                        if (nearestHangingBox != null)
                        {
                            Vector2 bPos = nearestHangingBox.Body.Position;
                            float toBoxX = bPos.X - selfPos.X;
                            // 悬挂箱位于前进方向 30~240 像素内，且目标在深渊对侧
                            if (MathF.Sign(toBoxX) == MathF.Sign(probeSign) && MathF.Abs(toBoxX) < 240f && MathF.Sign(deltaToTarget.X) == MathF.Sign(probeSign))
                            {
                                canReachHangingBox = true;
                            }
                        }

                        if (canBoardPlatform && _jumpCooldown <= 0f)
                        {
                            // 果断跳上移动平台！
                            input.IsJumpDown = true;
                            _jumpCooldown = 0.5f;
                        }
                        else if (canReachHangingBox && _jumpCooldown <= 0f)
                        {
                            // 锁定深渊上方的悬挂箱：主动飞跃扑向悬挂箱，进行蹬墙跳或跃上箱顶！
                            input.IsJumpDown = true;
                            desiredMoveX = probeSign;
                            _jumpCooldown = 0.45f;
                        }
                        else
                        {
                            // 无法安全过坑：【悬崖勒马，退守高地】！
                            desiredMoveX = -probeSign * 0.8f;
                        }
                    }
                }
            }

            // 冰面微调：如果在冰面上，避免猛打方向失控，改为柔和脉冲
            if (self.Body.IsGrounded && map.TerrainObjects.Any(t => t.Style == TerrainStyle.IceCrystal && t.Collider.Bounds.Contains((int)selfPos.X, (int)selfPos.Y + 28)))
            {
                desiredMoveX *= 0.55f;
            }

            input.MoveVector = new Vector2(desiredMoveX, 0f);

            // 3.4 智能跳跃与贴墙/悬挂箱蹬墙跳 (Smart Jump & Wall Jump)
            if (self.Body.IsTouchingWall && !self.Body.IsGrounded && _jumpCooldown <= 0f)
            {
                // 贴在悬挂箱或高墙侧壁：顺畅触发蹬墙跳跃向高处或反弹
                input.IsJumpDown = true;
                _jumpCooldown = 0.36f;
            }
            else
            {
                Vector2 forwardRay = new(desiredMoveX > 0 ? 1f : -1f, 0f);
                var wallHit = world.Raycast(selfPos + forwardRay * (self.Radius + 4f), forwardRay, 50f, self.Collider);

                if (wallHit.HasHit && self.Body.IsGrounded && _jumpCooldown <= 0f)
                {
                    // 遇到低矮台阶障碍，直接起跳翻越
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.42f;
                }
                else if (_stuckTimer > 0.8f && self.Body.IsGrounded && _jumpCooldown <= 0f)
                {
                    // 发生卡位时主动解卡跳跃
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.45f;
                    _stuckTimer = 0f;
                }
                else if (targetPos.Y < selfPos.Y - 100f && MathF.Abs(deltaToTarget.X) < 280f && (self.Body.IsGrounded || (self.RemainingJumps > 0 && self.Body.Velocity.Y > 0f)) && _jumpCooldown <= 0f)
                {
                    // 目标在上方高台，主动跳跃抢点（地面起跳或空中二段跳补足高度）
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.45f;
                }
            }
        }

        // -----------------------------------------------------------------
        // 4. 重力抛物线抬枪补偿与超前预判瞄准 (Ballistic Lead & Arc Compensation)
        // -----------------------------------------------------------------
        float bulletSpeed = MathF.Max(self.BulletSpeed, 650f);
        float flightTime = distToTarget / bulletSpeed;
        float bulletGravity = world.Gravity.Y * 0.55f;

        // 物理弹道下坠补偿公式：h_compensate = 0.5 * g * t^2
        Vector2 dropCompensation = new(0f, -0.5f * bulletGravity * flightTime * flightTime * 0.86f);
        // 目标移动速度超前预判
        Vector2 predictedTargetPos = targetPos + target.Body.Velocity * (flightTime * 0.72f) + dropCompensation;
        Vector2 aimDirection = MathHelper2D.SafeNormalize(predictedTargetPos - selfPos);
        input.AimVector = aimDirection;

        // -----------------------------------------------------------------
        // 5. 动态掩体遮挡感知与智能开火 (Smart Weapon Firing)
        // -----------------------------------------------------------------
        var losCheck = world.Raycast(selfPos + aimDirection * (self.Radius + 6f), aimDirection, distToTarget, self.Collider);
        bool hasClearShot = true;

        if (losCheck.HasHit && losCheck.Distance < distToTarget - 45f)
        {
            // 视线受阻，检查阻挡物体是否为可破坏砖块 [B]
            var hitTerrain = map.TerrainObjects.Find(t => t.Collider == losCheck.HitCollider);
            if (hitTerrain != null && hitTerrain.IsDestructible)
            {
                // 是可破坏掩体！主动射击破拆掩体！
                hasClearShot = true;
            }
            else
            {
                // 是不可穿透的坚固钢墙，且视线不通：省子弹换弹，不开空枪
                hasClearShot = false;
            }
        }

        if (hasClearShot && distToTarget < 1350f)
        {
            input.IsFireDown = true;
        }

        // -----------------------------------------------------------------
        // 6. 反应式防御与掩体规避 (Threat Projectile Defense)
        // -----------------------------------------------------------------
        bool incomingThreat = false;
        foreach (var proj in projectiles)
        {
            if (proj.Owner != self && !proj.IsDestroyed)
            {
                Vector2 toSelf = selfPos - proj.Body.Position;
                float distToProj = toSelf.Length();

                // 距离小于 220 像素且迎面高速飞来
                if (distToProj < 220f && Vector2.Dot(proj.Body.Velocity, toSelf) > 0f)
                {
                    incomingThreat = true;
                    break;
                }
            }
        }

        if (incomingThreat && self.SubWeapon != null && self.SubWeapon.IsReady)
        {
            input.IsSecondaryDown = true;
        }

        return input;
    }
}
