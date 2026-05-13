using System.Numerics;

namespace CloudView.Controls;

/// <summary>
/// 立方体 / 长方体 ROI。
/// </summary>
public sealed class BoxRoi : RoiBase
{
    private Vector3 _size = new(1, 1, 1);

    /// <summary>
    /// ROI 尺寸（X/Y/Z 三轴全长）。
    /// </summary>
    public Vector3 Size
    {
        get => _size;
        set => _size = new Vector3(
            MathF.Max(0.01f, value.X),
            MathF.Max(0.01f, value.Y),
            MathF.Max(0.01f, value.Z));
    }

    /// <inheritdoc />
    public override RoiKind Kind => RoiKind.Box;

    /// <inheritdoc />
    public override float GetBoundingRadius() => Size.Length() * 0.5f;

    /// <inheritdoc />
    public override bool Contains(Vector3 worldPoint)
    {
        var local = WorldToLocal(worldPoint);
        var half = Size * 0.5f;
        return MathF.Abs(local.X) <= half.X &&
               MathF.Abs(local.Y) <= half.Y &&
               MathF.Abs(local.Z) <= half.Z;
    }
}
