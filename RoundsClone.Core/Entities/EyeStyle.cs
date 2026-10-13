namespace RoundsClone.Core.Entities;

/// <summary>
/// 角色面部眼神与表情风格枚举。
/// 支持在角色定制界面自由切换经典、卖萌、猥琐、搞怪等多种程序化神态。
/// </summary>
public enum EyeStyle
{
    /// <summary>
    /// 经典斗士：标准灵动瞳孔与英气眉型。
    /// </summary>
    Classic,

    /// <summary>
    /// 水汪萌眼：超大亮瞳、双重星光高光、粉红腮红与猫耳小嘴。
    /// </summary>
    Cute,

    /// <summary>
    /// 猥琐眯眼：半阖上下眼睑、斜视小眼珠与挑眉歪嘴坏笑。
    /// </summary>
    Sneaky,

    /// <summary>
    /// 智慧斗鸡：大小不对称眼眶、动态对眼瞳孔与呆萌圆嘴。
    /// </summary>
    Derp,

    /// <summary>
    /// 腹黑笑眼：月牙弯弧笑眼、腮红与微笑弧线。
    /// </summary>
    Happy,

    /// <summary>
    /// 狂暴死鱼：黑眼圈眼袋、暴突眼白、红环针尖瞳孔与陡峭怒眉。
    /// </summary>
    Crazy
}
