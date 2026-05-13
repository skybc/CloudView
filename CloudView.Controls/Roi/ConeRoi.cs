using System.Numerics;

namespace CloudView.Controls;

/// <summary>
/// 圆锥 ROI。默认局部 Y 轴为高度方向，Center 位于几何中心。
/// 顶点位于 +Y 半高处，底面位于 -Y 半高处。
/// </summary>
public sealed class ConeRoi : RoiBase
{
    private float _radius = 0.5f;
    private float _height = 1.0f;

    /// <summary>
    /// 底面半径。
    /// </summary>
    public float Radius
    {
        get => _radius;
        set => _radius = MathF.Max(0.01f, value);
    }

    /// <summary>
    /// 圆锥高度。
    /// </summary>
    public float Height
    {
        get => _height;
        set => _height = MathF.Max(0.01f, value);
    }

    /// <inheritdoc />
    public override RoiKind Kind => RoiKind.Cone;

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
        if (local.Y < -halfHeight || local.Y > halfHeight)
        {
            return false;
        }

        float ratio = (halfHeight - local.Y) / Height;
        float allowedRadius = Radius * ratio;
        float radial = MathF.Sqrt(local.X * local.X + local.Z * local.Z);
        return radial <= allowedRadius;
    }
}
