using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Cards;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Maps;
using RoundsClone.Core.Physics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.AI;

/// <summary>
/// 战术对抗型综合智能 AI（全机制高阶优化版）。
/// 深度适配最新战斗系统与物理手感：
/// 1. 毒云弥漫感知与躲避：感知战场活跃敌方毒云，主动退散或利用疾风冲刺瞬间穿行脱险；
/// 2. 副武器精准差异化决策：
///    - 疾风冲刺 (DashSubWeapon)：紧急越障脱坑/脱离毒雾/侧向横向闪避来袭子弹/敌方换弹时近身压制，彻底修复朝向陷阱冲刺的严重缺陷；
///    - 力场弹反 (BlockSubWeapon)：弹道时差计算，在来袭子弹贴脸 0.16s 瞬间面朝弹道精准弹反；
/// 3. 目标折射与墙面反弹利用 (Target Bounce & Bank Shot)：直接视线受阻时，借助天花板与墙面法线反弹折射，实现隔墙绕柱打击；
/// 4. 范围爆破冲击波溅射 (Cluster Splash)：持有集束爆破时瞄准敌人脚底地面或掩体下缘形成稳定范围溅射；
/// 5. 装弹风筝战术 (Tactical Reload Kiting)：自身换弹期间退守高地拉开间距，敌方换弹期间贴近爆发压制；
/// 6. 尖刺深渊探针、移动平台通勤与悬挂吊箱借力攀登。
/// </summary>
public sealed class TacticalCombatAi : IAiBrain
{
    /// <inheritdoc />
    public string Name => "高智商战术特工 AI";

    private float _reactionTimer = 0f;
    private float _jumpCooldown = 0f;
    private float _dodgeCooldown = 0f;
    private float _strafeTimer = 0f;
    private float _strafeDir = 1f;
    private Vector2 _lastPos = Vector2.Zero;
    private float _stuckTimer = 0f;

    /// <inheritdoc />
    public ControlInputState Think(
        Actor self,
        Actor target,
        IReadOnlyList<Projectile> projectiles,
        CollisionWorld world,
        MapData map,
        float dt,
        IReadOnlyList<ToxicCloud>? toxicClouds = null)
    {
        ControlInputState input = default;
        if (self.IsDead || target.IsDead) return input;

        int difficulty = GameSettings.Current.AiDifficulty;

        _reactionTimer += dt;
        if (_jumpCooldown > 0f) _jumpCooldown -= dt;
        if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;

        Vector2 selfPos = self.Body.Position;
        Vector2 targetPos = target.Body.Position;
        Vector2 deltaToTarget = targetPos - selfPos;
        float distToTarget = deltaToTarget.Length();

        // 识别双方装配的关键卡牌与武器特性
        bool hasTargetBounce = self.HasTargetBounce || self.BulletBounces > 0;
        bool hasClusterBomb = self.AppliedCards.Exists(c => c is ClusterBombCard);
        bool targetHasCluster = target.AppliedCards.Exists(c => c is ClusterBombCard);
        bool targetIsReloading = target.IsReloading;

        // 卡位防呆计时器
        if (Vector2.DistanceSquared(selfPos, _lastPos) < 6f && self.Body.IsGrounded)
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
            _strafeTimer = 0.7f + (float)Random.Shared.NextDouble() * 1.0f;
        }

        // -----------------------------------------------------------------
        // 1. 地图环境与危险尖刺深渊感知 (Spike Hazard Avoidance)
        // -----------------------------------------------------------------
        var hazards = map.TerrainObjects
            .Where(t => t.Style == TerrainStyle.HazardSpike && !t.IsDestroyed)
            .Select(t => t.Collider.Bounds)
            .ToList();

        bool isDirectlyOverHazard = false;
        Rectangle activeHazard = default;
        foreach (var hazard in hazards)
        {
            if (selfPos.X >= hazard.Left - 18f && selfPos.X <= hazard.Right + 18f && selfPos.Y <= hazard.Bottom + 10f)
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
        if (isDirectlyOverHazard && selfPos.Y > activeHazard.Top - 320f)
        {
            isExecutingEmergencyEscape = true;
            float distToLeftSafe = MathF.Abs(selfPos.X - activeHazard.Left);
            float distToRightSafe = MathF.Abs(selfPos.X - activeHazard.Right);
            float escapeDirX = (distToLeftSafe < distToRightSafe) ? -1.0f : 1.0f;

            if (self.Body.IsTouchingWall && _jumpCooldown <= 0f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.22f;
            }
            else
            {
                input.MoveVector = new Vector2(escapeDirX, 0f);
                if ((self.Body.IsGrounded || (self.RemainingJumps > 0 && self.Body.Velocity.Y > 0f)) && _jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.20f;
                }
            }

            // 【疾风冲刺高空自救关键修复】：
            // Dash 依据 AimVector 冲刺 200px。必须朝向斜上方安全陆地冲刺，绝不能朝向陷阱底部的敌人！
            if (self.SubWeapon is DashSubWeapon dash && dash.IsReady && _dodgeCooldown <= 0f)
            {
                input.AimVector = MathHelper2D.SafeNormalize(new Vector2(escapeDirX * 1.2f, -0.45f));
                input.IsSecondaryDown = true;
                _dodgeCooldown = 0.5f;
            }
        }

        // -----------------------------------------------------------------
        // 3. 毒云弥漫感知与躲避 (Toxic Cloud Avoidance)
        // -----------------------------------------------------------------
        bool isInToxicCloud = false;
        Vector2 toxicRepulsion = Vector2.Zero;

        if (toxicClouds != null)
        {
            foreach (var cloud in toxicClouds)
            {
                if (cloud.IsExpired || cloud.Owner == self) continue;

                float distToCloud = Vector2.Distance(selfPos, cloud.Position);
                float dangerDist = cloud.Radius + self.Radius + 32f; // 约 50 + 20 + 32 = 102px
                if (distToCloud < dangerDist)
                {
                    isInToxicCloud = true;
                    Vector2 away = selfPos - cloud.Position;
                    if (away.LengthSquared() < 0.01f) away = new Vector2(_strafeDir, -0.5f);
                    toxicRepulsion += MathHelper2D.SafeNormalize(away) * (dangerDist - distToCloud);
                }
            }
        }

        if (isInToxicCloud && !isExecutingEmergencyEscape)
        {
            // 身陷毒云内部，紧急向毒圈外撤离！
            float escapeToxicX = toxicRepulsion.X != 0 ? MathF.Sign(toxicRepulsion.X) : (deltaToTarget.X > 0 ? -1f : 1f);
            input.MoveVector = new Vector2(escapeToxicX, 0f);

            // 若在地面或下落，起跳越过贴地毒云
            if ((self.Body.IsGrounded || self.RemainingJumps > 0) && _jumpCooldown <= 0f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.35f;
            }

            // 若装备疾风冲刺且就绪，直接冲刺 200px 闪电脱离毒气区域
            if (self.SubWeapon is DashSubWeapon dash && dash.IsReady && _dodgeCooldown <= 0f)
            {
                input.AimVector = MathHelper2D.SafeNormalize(new Vector2(escapeToxicX, -0.2f));
                input.IsSecondaryDown = true;
                _dodgeCooldown = 0.4f;
            }
        }

        // -----------------------------------------------------------------
        // 4. 反应式防御与来袭子弹规避 (Threat Projectile Defense)
        // -----------------------------------------------------------------
        Projectile? imminentThreat = null;
        float shortestTimeToImpact = float.MaxValue;
        Vector2 threatToSelf = Vector2.Zero;

        foreach (var proj in projectiles)
        {
            if (proj.Owner == self || proj.IsDestroyed) continue;

            Vector2 toSelf = selfPos - proj.Body.Position;
            float distToProj = toSelf.Length();
            if (distToProj > 360f) continue;

            float projSpeed = proj.Body.Velocity.Length();
            if (projSpeed < 100f) continue;

            float approachSpeed = Vector2.Dot(proj.Body.Velocity, MathHelper2D.SafeNormalize(toSelf));

            if (approachSpeed > 120f)
            {
                // 计算子弹飞掠轨迹与 AI 中心的垂直垂距
                float perpDist = MathF.Abs((selfPos.X - proj.Body.Position.X) * proj.Body.Velocity.Y - (selfPos.Y - proj.Body.Position.Y) * proj.Body.Velocity.X) / projSpeed;
                float threatRadius = self.Radius + proj.Radius + (targetHasCluster ? 65f : 24f);

                if (perpDist < threatRadius)
                {
                    float timeToImpact = distToProj / approachSpeed;
                    if (timeToImpact < shortestTimeToImpact)
                    {
                        shortestTimeToImpact = timeToImpact;
                        imminentThreat = proj;
                        threatToSelf = toSelf;
                    }
                }
            }
        }

        // 针对不同副武器执行高智商反应防御：
        if (imminentThreat != null && self.SubWeapon != null && self.SubWeapon.IsReady)
        {
            if (self.SubWeapon is BlockSubWeapon)
            {
                // 力场弹反：根据难度调节弹反成功率（简单 35%，普通 72%，地狱 95%）
                float parryChance = difficulty switch
                {
                    0 => 0.35f,
                    1 => 0.72f,
                    _ => 0.95f
                };

                // 在来袭子弹距离 150px 且预计到达时间 <= 0.16s 触发极速弹反！
                if (shortestTimeToImpact <= 0.16f && threatToSelf.Length() <= 150f)
                {
                    if (Random.Shared.NextDouble() <= parryChance)
                    {
                        input.AimVector = MathHelper2D.SafeNormalize(-imminentThreat.Body.Velocity);
                        input.IsSecondaryDown = true;
                    }
                }
            }
            else if (self.SubWeapon is DashSubWeapon && _dodgeCooldown <= 0f)
            {
                // 疾风冲刺：切勿正面撞击子弹！而是沿子弹速度垂线方向横向横移冲刺 200px 闪避！
                if (shortestTimeToImpact <= 0.18f && threatToSelf.Length() <= 260f)
                {
                    Vector2 bulletVel = imminentThreat.Body.Velocity;
                    Vector2 perp1 = new(-bulletVel.Y, bulletVel.X);
                    Vector2 perp2 = -perp1;

                    // 优先选择能避开下方尖刺的斜向/侧向冲刺方向
                    Vector2 chosenPerp = (perp1.Y < perp2.Y) ? perp1 : perp2;
                    Vector2 dashEvasion = MathHelper2D.SafeNormalize(chosenPerp + new Vector2(0f, -0.3f));

                    input.AimVector = dashEvasion;
                    input.IsSecondaryDown = true;
                    float baseCooldown = difficulty switch
                    {
                        0 => 0.70f,
                        1 => 0.45f,
                        _ => 0.32f
                    };
                    _dodgeCooldown = baseCooldown;
                }
            }
        }

        // 若敌人处于换弹状态（极度脆弱），且 AI 处于满弹/高血量状态，利用冲刺 200px 瞬间近身贴脸压制！
        if (targetIsReloading && distToTarget > 280f && distToTarget < 550f &&
            self.SubWeapon is DashSubWeapon aggroDash && aggroDash.IsReady &&
            self.CurrentAmmo >= 2 && !isDirectlyOverHazard && _dodgeCooldown <= 0f)
        {
            input.AimVector = MathHelper2D.SafeNormalize(deltaToTarget);
            input.IsSecondaryDown = true;
            _dodgeCooldown = 0.6f;
        }

        // -----------------------------------------------------------------
        // 5. 常规移动与战术走位决策 (包括装弹风筝、地面防坠落探针与悬挂箱/平台搭乘)
        // -----------------------------------------------------------------
        if (!isExecutingEmergencyEscape && !isInToxicCloud)
        {
            // 5.1 寻找最近的移动平台与悬挂物理箱
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

            // 【装弹风筝战术 (Tactical Reload Kiting)】：
            // 若 AI 自身正在换弹，由于无法开火，将战术交战距离拉大至 620px 进行防守拉扯与掩体借势；
            // 若敌人在换弹，AI 贴近至 320px 实施压制！
            float preferredDistance = 450f;
            if (self.IsReloading || self.CurrentAmmo == 0)
            {
                preferredDistance = 620f;
            }
            else if (targetIsReloading)
            {
                preferredDistance = 320f;
            }

            float desiredMoveX;
            if (isRidingHangingBox)
            {
                desiredMoveX = deltaToTarget.X > 0 ? 1.0f : -1.0f;
                if (_jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.50f;
                }
            }
            else if (isRidingPlatform)
            {
                float platformDeltaTargetX = deltaToTarget.X;
                if (MathF.Abs(platformDeltaTargetX) < 180f)
                {
                    desiredMoveX = platformDeltaTargetX > 0 ? 1.0f : -1.0f;
                }
                else
                {
                    desiredMoveX = 0f;
                }
            }
            else
            {
                // 距离拉扯
                if (distToTarget > preferredDistance + 70f)
                {
                    desiredMoveX = deltaToTarget.X > 0 ? 1.0f : -1.0f;
                }
                else if (distToTarget < preferredDistance - 70f)
                {
                    desiredMoveX = deltaToTarget.X > 0 ? -1.0f : 1.0f;
                }
                else
                {
                    desiredMoveX = _strafeDir * 0.75f;
                }

                // 前瞻步法探针 (Footing Safety Probe)：检测前方地面是否通向尖刺或敌方毒气
                if (self.Body.IsGrounded && MathF.Abs(desiredMoveX) > 0.1f)
                {
                    float probeSign = desiredMoveX > 0 ? 1.0f : -1.0f;
                    Vector2 probePos = selfPos + new Vector2(probeSign * 55f, 0f);

                    bool hazardAhead = false;
                    foreach (var h in hazards)
                    {
                        if (probePos.X >= h.Left - 10f && probePos.X <= h.Right + 10f && selfPos.Y >= h.Top - 180f)
                        {
                            hazardAhead = true;
                            break;
                        }
                    }

                    if (!hazardAhead && toxicClouds != null)
                    {
                        foreach (var cloud in toxicClouds)
                        {
                            if (!cloud.IsExpired && cloud.Owner != self && Vector2.Distance(probePos, cloud.Position) < cloud.Radius + 20f)
                            {
                                hazardAhead = true;
                                break;
                            }
                        }
                    }

                    if (hazardAhead)
                    {
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
                            if (MathF.Sign(toBoxX) == MathF.Sign(probeSign) && MathF.Abs(toBoxX) < 240f && MathF.Sign(deltaToTarget.X) == MathF.Sign(probeSign))
                            {
                                canReachHangingBox = true;
                            }
                        }

                        if (canBoardPlatform && _jumpCooldown <= 0f)
                        {
                            input.IsJumpDown = true;
                            _jumpCooldown = 0.5f;
                        }
                        else if (canReachHangingBox && _jumpCooldown <= 0f)
                        {
                            input.IsJumpDown = true;
                            desiredMoveX = probeSign;
                            _jumpCooldown = 0.45f;
                        }
                        else
                        {
                            desiredMoveX = -probeSign * 0.8f;
                        }
                    }
                }
            }

            // 冰面微调
            if (self.Body.IsGrounded && map.TerrainObjects.Any(t => t.Style == TerrainStyle.IceCrystal && t.Collider.Bounds.Contains((int)selfPos.X, (int)selfPos.Y + 28)))
            {
                desiredMoveX *= 0.55f;
            }

            input.MoveVector = new Vector2(desiredMoveX, 0f);

            // 智能跳跃与贴墙/悬挂箱蹬墙跳
            if (self.Body.IsTouchingWall && !self.Body.IsGrounded && _jumpCooldown <= 0f)
            {
                input.IsJumpDown = true;
                _jumpCooldown = 0.36f;
            }
            else
            {
                Vector2 forwardRay = new(desiredMoveX > 0 ? 1f : -1f, 0f);
                var wallHit = world.Raycast(selfPos + forwardRay * (self.Radius + 4f), forwardRay, 50f, self.Collider);

                if (wallHit.HasHit && self.Body.IsGrounded && _jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.42f;
                }
                else if (_stuckTimer > 0.8f && self.Body.IsGrounded && _jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.45f;
                    _stuckTimer = 0f;
                }
                else if (targetPos.Y < selfPos.Y - 100f && MathF.Abs(deltaToTarget.X) < 280f && (self.Body.IsGrounded || (self.RemainingJumps > 0 && self.Body.Velocity.Y > 0f)) && _jumpCooldown <= 0f)
                {
                    input.IsJumpDown = true;
                    _jumpCooldown = 0.45f;
                }
            }
        }

        // -----------------------------------------------------------------
        // 6. 重力抛物线抬枪补偿与超前预判瞄准 (Ballistic Lead & Arc Compensation)
        // -----------------------------------------------------------------
        float bulletSpeed = MathF.Max(self.BulletSpeed, 650f);
        float flightTime = distToTarget / bulletSpeed;
        float bulletGravity = world.Gravity.Y * 0.55f;

        float leadFactor = difficulty switch
        {
            0 => 0.20f, // 简单：极弱预判，容易被快速横移躲避
            1 => 0.72f, // 普通：均衡战术预判
            _ => 0.98f  // 地狱：精准物理飞行轨迹超前预判
        };

        float dropFactor = difficulty switch
        {
            0 => 0.58f,
            1 => 0.86f,
            _ => 0.96f
        };

        Vector2 dropCompensation = new(0f, -0.5f * bulletGravity * flightTime * flightTime * dropFactor);
        Vector2 predictedTargetPos = targetPos + target.Body.Velocity * (flightTime * leadFactor) + dropCompensation;

        // 若持有集束爆破或爆炸子弹，且目标贴地，瞄准目标脚底地面，依靠 75px 冲击波形成稳定范围溅射
        if (hasClusterBomb && target.Body.IsGrounded)
        {
            predictedTargetPos += new Vector2(0f, 15f);
        }

        Vector2 directAimDirection = MathHelper2D.SafeNormalize(predictedTargetPos - selfPos);

        // 简单难度附加准星微小散布误差
        if (difficulty == 0)
        {
            float spreadAngle = (float)(Random.Shared.NextDouble() * 0.22 - 0.11); // ±0.11 rad 散布
            float cos = MathF.Cos(spreadAngle);
            float sin = MathF.Sin(spreadAngle);
            directAimDirection = new Vector2(
                directAimDirection.X * cos - directAimDirection.Y * sin,
                directAimDirection.X * sin + directAimDirection.Y * cos
            );
        }

        // -----------------------------------------------------------------
        // 7. 动态掩体遮挡感知与【目标折射/反弹战术射击 (Bank Shot & Target Bounce)】
        // -----------------------------------------------------------------
        var losCheck = world.Raycast(selfPos + directAimDirection * (self.Radius + 6f), directAimDirection, distToTarget, self.Collider);
        bool hasClearShot = true;
        Vector2 finalAim = directAimDirection;

        if (losCheck.HasHit && losCheck.Distance < distToTarget - 45f)
        {
            var hitTerrain = map.TerrainObjects.Find(t => t.Collider == losCheck.HitCollider);
            if (hitTerrain != null && hitTerrain.IsDestructible)
            {
                // 可破坏砖块：主动射击破拆掩体
                hasClearShot = true;
                finalAim = directAimDirection;
            }
            else
            {
                hasClearShot = false;

                // 若装备了【目标折射】或具备子弹反弹能力，计算天花板/地面反射折射角！
                if (hasTargetBounce)
                {
                    Vector2[] bounceCandidates = new Vector2[]
                    {
                        MathHelper2D.SafeNormalize(new Vector2(deltaToTarget.X > 0 ? 0.75f : -0.75f, -0.65f)),
                        MathHelper2D.SafeNormalize(new Vector2(deltaToTarget.X > 0 ? 0.75f : -0.75f, 0.45f)),
                        MathHelper2D.SafeNormalize(new Vector2(deltaToTarget.X > 0 ? 0.40f : -0.40f, -0.90f))
                    };

                    foreach (var candidate in bounceCandidates)
                    {
                        var bounceHit = world.Raycast(selfPos + candidate * (self.Radius + 4f), candidate, 700f, self.Collider);
                        if (bounceHit.HasHit)
                        {
                            Vector2 hitNormal = bounceHit.Normal;
                            Vector2 toEnemyFromHit = targetPos - bounceHit.Point;

                            // 碰撞表面法向朝向敌人所在侧，满足目标折射条件
                            if (Vector2.Dot(hitNormal, toEnemyFromHit) > 0.05f)
                            {
                                finalAim = candidate;
                                hasClearShot = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // 若此前未被特技动作（如闪避冲刺或自救冲刺）指定准星，应用计算出的射击准星
        if (input.AimVector == Vector2.Zero)
        {
            input.AimVector = finalAim;
        }

        // 8. 智能开火控制
        if (hasClearShot && distToTarget < 1400f && !self.IsReloading && self.CurrentAmmo > 0)
        {
            bool canFire = difficulty switch
            {
                0 => Random.Shared.NextDouble() < 0.72, // 简单难度：偶尔迟疑
                _ => true
            };

            if (canFire)
            {
                input.IsFireDown = true;
            }
        }

        return input;
    }
}

