using System;
using Microsoft.Xna.Framework;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 生命虹吸技能卡插件。
/// 职责边界：提升角色最大生命值上限，并在子弹命中敌人时按造成伤害的一定比例恢复自身生命值。
/// </summary>
public class VampiricLeechCard : ICardPlugin
{
    public string Id => "card_vampiric_leech";
    public string Name => "生命虹吸";
    public string Description => "全伤害吸血 +30%\n最大生命 +25\n换弹时间 +0.35s";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Leech;

    public void OnApply(Actor actor)
    {
        actor.HasVampiricLeech = true;
        actor.MaxHealth += 25f;
        actor.CurrentHealth = MathF.Min(actor.CurrentHealth + 25f, actor.MaxHealth);
        actor.ReloadDuration += 0.35f;
        actor.RecalculateHealthScaling();
    }
}
