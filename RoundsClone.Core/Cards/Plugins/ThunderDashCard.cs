using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 雷霆冲刺技能卡插件。
/// 职责边界：提升冲刺副武器的爆发速度、缩短副武器冷却，并在每次释放副武器时瞬间补满弹匣。
/// </summary>
public class ThunderDashCard : ICardPlugin
{
    public string Id => "card_thunder_dash";
    public string Name => "雷霆冲刺";
    public string Description => "冲刺速度 +40%\n副武器冷却 -30%";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Dash;

    public void OnApply(Actor actor)
    {
        if (actor.SubWeapon is DashSubWeapon dash)
        {
            dash.DashSpeed *= 1.4f;
            dash.Cooldown *= 0.70f;
        }
        else if (actor.SubWeapon != null)
        {
            actor.SubWeapon.Cooldown *= 0.8f;
        }
    }

    public void OnSecondaryUsed(Actor actor, ISubWeapon weapon)
    {
        actor.Body.ApplyImpulse(actor.Body.Velocity * 0.25f);
        actor.CurrentAmmo = actor.MaxAmmo;
        actor.ReloadTimer = 0f;
    }
}
