namespace RoundsClone.Core.Audio;

/// <summary>
/// 游戏内置标准音效枚举。
/// </summary>
public enum SoundType
{
    Shoot,      // 子弹射击
    Explosion,  // 剧烈爆炸
    Bounce,     // 墙面反弹/折射
    Hit,        // 击中敌人受击
    Freeze,     // 极寒冰冻/霜晶
    ToxicGas,   // 毒云喷涌嘶嘶声
    Reload,     // 装弹完成咔哒声
    Dash,       // 冲刺位移呼啸声
    Block,      // 护盾格挡回弹音
    Jump,       // 跳跃起跳腾空声
    Step,       // 地面移动脚步啪嗒声
    UiHover,    // 按钮/选项划过悬停音
    UiClick,    // 按钮/选项确认点击音
    BlackHole,  // 黑洞坍缩/引力场呼啸
    Phoenix,    // 凤凰涅槃圣咏金身复活音
    DryFire,    // 空弹仓击空撞针清脆咔哒音
    Death,      // 角色生命归零震撼终结重音
    CardPick,   // 选卡锁定能量升华音
    PoisonSizzle, // 剧毒腐蚀细微滋滋音
    Teleport,   // 时空折跃瞬移相位嗡鸣
    WallHit     // 子弹无反弹撞墙碎裂声
}
