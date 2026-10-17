using System;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 以战养战技能卡插件 (Scavenger)。
/// 职责边界：直接命中敌人时即刻返还 1 发弹药入膛，精准射手近乎无需主动换弹；代价是弹匣上限减 1 且换弹周期大幅延长。
/// </summary>
public class ScavengerCard : ICardPlugin
{
    public string Id => "card_scavenger";
    public string Name => "以战养战";
    public string Description => "子弹命中即刻返还 1发\n最大弹匣 -1\n换弹时间 +0.5s";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.HasScavenger = true;
        actor.MaxAmmo = Math.Max(1, actor.MaxAmmo - 1);
        actor.CurrentAmmo = Math.Min(actor.CurrentAmmo, actor.MaxAmmo);
        actor.ReloadDuration += 0.50f;
    }
}
