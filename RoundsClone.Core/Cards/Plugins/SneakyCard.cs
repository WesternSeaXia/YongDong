using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 贴地滑翔技能卡插件 (Sneaky)。
/// 职责边界：子弹在平地与小于等于 45 度的缓坡上产生气垫排斥贴地滑翔，面对垂直 90 度墙面或陡坡失效直接撞击；代价是子弹初速降低 20%。
/// </summary>
public class SneakyCard : ICardPlugin
{
    public string Id => "card_sneaky";
    public string Name => "贴地滑翔";
    public string Description => "地表与45度坡气垫滑翔\n垂直90度墙正常撞击\n子弹初速 -20%";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Crosshair;

    public void OnApply(Actor actor)
    {
        actor.HasSneakyBullet = true;
        actor.BulletBounces += 1;
        actor.BulletSpeed *= 0.80f;
    }

    public void OnProjectileSpawn(Actor actor, Projectile projectile)
    {
        projectile.IsSneakyBullet = true;
    }
}
