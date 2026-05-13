using System.Numerics;

namespace CloudView.Controls;

/// <summary>
/// 圆柱 ROI。默认局部 Y 轴为高度方向，Center 为圆柱几何中心。
/// </summary>
public sealed class CylinderRoi : RoiBase
{
    private float _radius = 0.5f;
    private float _height = 1.0f;

    /// <summary>
    /// 圆柱半径。
    /// </summary>
    public float Radius
    {
        get => _radius;
        set => _radius = MathF.Max(0.01f, value);
    }

    /// <summary>
    /// 圆柱高度。
    /// </summary>
    public float Height
    {
        get => _height;
        set => _height = MathF.Max(0.01f, value);
    }

    /// <inheritdoc />
    public override RoiKind Kind => RoiKind.Cylinder;

    /// <inheritdoc />
    public override float GetBoundingRadius()
    {
        float halfHeight = Height * 0.5f;
        return MathF.Sqrt(Radius * Radius + halfHeight * halfHeight);
    }

    /// <inheritdoc />
    public override bool Contains(Vector3 worldPoint)
    {
        var local = WorldToLocal(worldPoint);
        float halfHeight = Height * 0.5f;
        if (MathF.Abs(local.Y) > halfHeight)
        {
            return false;
        }

        return local.X * local.X + local.Z * local.Z <= Radius * Radius;
    }
}
