using System;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 极速装填技能卡插件 (Quick Reload)。
/// 职责边界：大幅减少主武器换弹耗时 1.0 秒，打造高机动连续火力输出流派。
/// </summary>
public class QuickReloadCard : ICardPlugin
{
    public string Id => "card_quick_reload";
    public string Name => "极速装填";
    public string Description => "换弹时间 -1.0s";
    public CardTier Tier => CardTier.Rare;
    public IconType Icon => IconType.Bullet;

    public void OnApply(Actor actor)
    {
        actor.ReloadDuration = MathF.Max(0.20f, actor.ReloadDuration - 1.0f);
    }
}
