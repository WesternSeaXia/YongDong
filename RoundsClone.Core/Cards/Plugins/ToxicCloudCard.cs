using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 毒云弥漫技能卡插件 (Toxic Cloud)。
/// 职责边界：子弹命中任何物体均会在原地遗留持续 3.5 秒的范围毒雾，为经过的敌人施加 35% 移动减速与周期性毒素伤害；代价是换弹时间增加 0.3 秒。
/// </summary>
public class ToxicCloudCard : ICardPlugin
{
    public string Id => "card_toxic_cloud";
    public string Name => "毒云弥漫";
    public string Description => "每次碰撞生成持续毒雾\n毒雾伤害 10点/秒\n区域减速 35%\n换弹时间 +0.3s";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Poison;

    public void OnApply(Actor actor)
    {
        actor.HasToxicCloud = true;
        actor.ReloadDuration += 0.30f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.SpawnsToxicCloud = true;
    }
}
