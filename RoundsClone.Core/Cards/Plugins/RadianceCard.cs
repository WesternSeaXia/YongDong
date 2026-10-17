using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 换弹日冕技能卡插件 (Radiance)。
/// 职责边界：换弹期间角色化身微型太阳，向四周持续脉冲释放高热太阳能量波，击退并重创试图近身压制的对手；代价是换弹时间延长 0.5 秒。
/// </summary>
public class RadianceCard : ICardPlugin
{
    public string Id => "card_radiance";
    public string Name => "换弹日冕";
    public string Description => "换弹释放太阳脉冲光波\n近身震退并烧伤敌人\n换弹时间 +0.5s";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Star;

    public void OnApply(Actor actor)
    {
        actor.HasRadiance = true;
        actor.ReloadDuration += 0.50f;
    }
}
