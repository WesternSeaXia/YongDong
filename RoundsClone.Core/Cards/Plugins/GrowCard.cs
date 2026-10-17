using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 飞行增殖技能卡插件 (Grow)。
/// 职责边界：子弹刚出膛时受到贴脸近战惩罚，但随着在空中飞行时间越长，弹头体积膨胀（最高 2.4 倍）且威力剧增（最高 2.5 倍）。
/// </summary>
public class GrowCard : ICardPlugin
{
    public string Id => "card_grow";
    public string Name => "飞行增殖";
    public string Description => "空中飞行体积成长 2.4倍\n飞行伤害增幅 2.5倍\n贴脸初始伤害 -20%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.HasGrowBullet = true;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.IsGrowBullet = true;
        projectile.BaseDamage = projectile.Damage * 0.80f;
        projectile.Damage = projectile.BaseDamage;
    }
}
