using System.Numerics;
using System.Windows;

namespace CloudView.Controls;

/// <summary>
/// 点云中的单个点
/// </summary>
public struct PointCloudPoint
{
    public Vector3 Position;
    public Vector4 Color;

    public PointCloudPoint(Vector3 position, Vector4 color)
    {
        Position = position;
        Color = color;
    }

    public PointCloudPoint(float x, float y, float z, float r = 1f, float g = 1f, float b = 1f, float a = 1f)
    {
        Position = new Vector3(x, y, z);
        Color = new Vector4(r, g, b, a);
    }
}

/// <summary>
/// ROI 选择区域信息
/// </summary>
public class RoiSelectionEventArgs : RoutedEventArgs
{
    /// <summary>
    /// ROI 区域内的点索引列表
    /// </summary>
    public IReadOnlyList<int> SelectedIndices { get; }

    /// <summary>
    /// ROI 区域内的点集合
    /// </summary>
    public IReadOnlyList<PointCloudPoint> SelectedPoints { get; }

    /// <summary>
    /// 屏幕空间的 ROI 矩形区域
    /// </summary>
    public Rect ScreenRect { get; }

    public RoiSelectionEventArgs(IReadOnlyList<int> indices, IReadOnlyList<PointCloudPoint> points, Rect screenRect)
    {
        SelectedIndices = indices;
        SelectedPoints = points;
        ScreenRect = screenRect;
    }
}
