using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

/// <summary>
/// ROI 类型枚举。
/// </summary>
public enum RoiKind
{
    Box,
    Sphere,
    Cylinder,
    Cone,
}

/// <summary>
/// 可编辑 ROI 的抽象基类。
/// </summary>
public abstract class RoiBase
{
    private Vector3 _center;
    private Quaternion _rotation = Quaternion.Identity;
    private Color _color = Color.FromArgb(255, 255, 215, 0);
    private bool _isVisible = true;
    private bool _isLocked;

    /// <summary>
    /// ROI 标识。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ROI 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ROI 中心点。
    /// </summary>
    public Vector3 Center
    {
        get => _center;
        set => _center = value;
    }

    /// <summary>
    /// ROI 姿态旋转。
    /// </summary>
    public Quaternion Rotation
    {
        get => _rotation;
        set => _rotation = Quaternion.Normalize(value);
    }

    /// <summary>
    /// ROI 颜色。
    /// </summary>
    public Color Color
    {
        get => _color;
        set => _color = value;
    }

    /// <summary>
    /// 是否可见。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    /// <summary>
    /// 是否锁定编辑。
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;
        set => _isLocked = value;
    }

    /// <summary>
    /// ROI 类型。
    /// </summary>
    public abstract RoiKind Kind { get; }

    /// <summary>
    /// 获取 ROI 的包围半径，用于拾取和绘制辅助元素。
    /// </summary>
    public abstract float GetBoundingRadius();

    /// <summary>
    /// 判断给定世界坐标点是否位于 ROI 内部。
    /// </summary>
    public abstract bool Contains(Vector3 worldPoint);

    /// <summary>
    /// 将局部坐标变换到世界坐标。
    /// </summary>
    public Vector3 LocalToWorld(Vector3 localPoint)
    {
        return Vector3.Transform(localPoint, Rotation) + Center;
    }

    /// <summary>
    /// 将世界坐标变换到局部坐标。
    /// </summary>
    public Vector3 WorldToLocal(Vector3 worldPoint)
    {
        var inverse = Quaternion.Inverse(Rotation);
        return Vector3.Transform(worldPoint - Center, inverse);
    }

    /// <summary>
    /// 获取局部轴在世界空间中的方向。
    /// </summary>
    public Vector3 LocalAxisToWorld(Vector3 localAxis)
    {
        var worldAxis = Vector3.TransformNormal(localAxis, Matrix4x4.CreateFromQuaternion(Rotation));
        if (worldAxis.LengthSquared() <= 1e-6f)
        {
            return localAxis;
        }

        return Vector3.Normalize(worldAxis);
    }
}
