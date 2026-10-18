using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Entities;

/// <summary>
/// 极寒减速负面状态下附着在角色身上的多棱双锥体冰晶数据。
/// </summary>
public readonly struct FrostCrystalData
{
    public Vector2 RelativeOffset { get; }
    public float Angle { get; }
    public float PrismLength { get; }
    public float PrismWidth { get; }
    public float PyramidHeight { get; }

    public FrostCrystalData(Vector2 relativeOffset, float angle, float prismLength, float prismWidth, float pyramidHeight)
    {
        RelativeOffset = relativeOffset;
        Angle = angle;
        PrismLength = prismLength;
        PrismWidth = prismWidth;
        PyramidHeight = pyramidHeight;
    }
}
