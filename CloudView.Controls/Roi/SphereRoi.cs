using System.Numerics;

namespace CloudView.Controls;

/// <summary>
/// 球体 ROI。
/// </summary>
public sealed class SphereRoi : RoiBase
{
    private float _radius = 0.5f;

    /// <summary>
    /// 球体半径。
    /// </summary>
    public float Radius
    {
        get => _radius;
        set => _radius = MathF.Max(0.01f, value);
    }

    /// <inheritdoc />
    public override RoiKind Kind => RoiKind.Sphere;

    /// <inheritdoc />
    public override float GetBoundingRadius() => Radius;

    /// <inheritdoc />
    public override bool Contains(Vector3 worldPoint)
    {
        var local = WorldToLocal(worldPoint);
        return local.LengthSquared() <= Radius * Radius;
    }
}
