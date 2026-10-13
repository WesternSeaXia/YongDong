using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 极速连发技能卡插件。
/// 职责边界：提升射击频率、单次齐射弹丸数量与弹匣容量，打造高密度弹幕压制流派。
/// </summary>
public class RapidFireCard : ICardPlugin
{
    public string Id => "card_rapid_fire";
    public string Name => "极速连发";
    public string Description => "射速 +65%\n每次发射子弹 +1\n弹匣容量 +3";
    public CardTier Tier => CardTier.Common;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.FireRateCooldown *= 0.55f;
        actor.BulletsPerShot += 1;
        actor.BulletSpread += 0.12f;
        actor.MaxAmmo += 3;
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.ReloadDuration *= 0.85f;
    }
}
