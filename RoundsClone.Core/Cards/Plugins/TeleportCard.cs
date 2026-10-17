using System;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Cards;

/// <summary>
/// 瞬移折跃技能卡插件 (Teleport)。
/// 职责边界：副武器激活时沿准心发射前置射线检测，精准跃迁至障碍物前贴墙停步，杜绝穿墙卡死；代价是增加副武器冷却并削减生命上限。
/// </summary>
public class TeleportCard : ICardPlugin
{
    public string Id => "card_teleport";
    public string Name => "瞬移折跃";
    public string Description => "副武器替换为瞬移折跃\n防穿墙贴边安全跃迁\n冷却 +0.4s\n最大生命 -20";
    public CardTier Tier => CardTier.Epic;
    public IconType Icon => IconType.Dash;

    public void OnApply(Actor actor)
    {
        actor.HasTeleport = true;
        if (actor.SubWeapon != null)
        {
            actor.SubWeapon.Cooldown += 0.4f;
        }

        actor.MaxHealth = MathF.Max(30f, actor.MaxHealth - 20f);
        actor.CurrentHealth = MathF.Min(actor.CurrentHealth, actor.MaxHealth);
        actor.RecalculateHealthScaling();
    }
}
