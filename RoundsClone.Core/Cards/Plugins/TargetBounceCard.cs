using System;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 目标折射技能卡插件 (Target Bounce)。
/// 职责边界：子弹在撞击墙面或地形时持续精准重定向追踪敌人；付出弹匣容量减 1 的代价。
/// </summary>
public class TargetBounceCard : ICardPlugin
{
    public string Id => "card_target_bounce";
    public string Name => "目标折射";
    public string Description => "撞墙精准追踪折射敌人\n反弹次数 +1\n最大弹匣 -1";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Bounce;

    public void OnApply(Actor actor)
    {
        actor.HasTargetBounce = true;
        actor.BulletBounces += 1;
        actor.MaxAmmo = Math.Max(1, actor.MaxAmmo - 1);
        actor.CurrentAmmo = Math.Min(actor.CurrentAmmo, actor.MaxAmmo);
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.HasTargetBounce = true;
    }
}
