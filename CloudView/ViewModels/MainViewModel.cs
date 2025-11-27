using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudView.Controls;

namespace CloudView.ViewModels;

/// <summary>
/// 主窗口 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private IList<PointCloudPoint>? _points;

    [ObservableProperty]
    private float _pointSize = 3.0f;

    [ObservableProperty]
    private int _selectedPointCount;

    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    [ObservableProperty]
    private IReadOnlyList<PointCloudPoint>? _selectedPoints;

    [ObservableProperty]
    private IReadOnlyList<int>? _selectedIndices;

    public MainViewModel()
    {
        // 生成示例点云数据
        GenerateSamplePointCloud();
    }

    /// <summary>
    /// 生成示例点云数据
    /// </summary>
    [RelayCommand]
    private void GenerateSamplePointCloud()
    {
        var random = new Random(42);
        var points = new List<PointCloudPoint>();

        // 生成一个球形点云
        int pointCount = 10000;
        for (int i = 0; i < pointCount; i++)
        {
            // 随机球面坐标
            float theta = (float)(random.NextDouble() * Math.PI * 2);
            float phi = (float)(random.NextDouble() * Math.PI);
            float r = (float)(0.8 + random.NextDouble() * 0.4); // 半径在 0.8-1.2 之间

            float x = r * MathF.Sin(phi) * MathF.Cos(theta);
            float y = r * MathF.Sin(phi) * MathF.Sin(theta);
            float z = r * MathF.Cos(phi);

            // 根据位置设置颜色
            float red = (x + 1) / 2;
            float green = (y + 1) / 2;
            float blue = (z + 1) / 2;

            points.Add(new PointCloudPoint(x, y, z, red, green, blue, 1.0f));
        }

        Points = points;
        StatusMessage = $"已加载 {pointCount} 个点";
    }

    /// <summary>
    /// 生成立方体点云
    /// </summary>
    [RelayCommand]
    private void GenerateCubePointCloud()
    {
        var random = new Random(42);
        var points = new List<PointCloudPoint>();

        int pointCount = 10000;
        for (int i = 0; i < pointCount; i++)
        {
            float x = (float)(random.NextDouble() * 2 - 1);
            float y = (float)(random.NextDouble() * 2 - 1);
            float z = (float)(random.NextDouble() * 2 - 1);

            // 彩虹色
            float hue = (float)i / pointCount;
            var (r, g, b) = HsvToRgb(hue * 360, 1, 1);

            points.Add(new PointCloudPoint(x, y, z, r, g, b, 1.0f));
        }

        Points = points;
        StatusMessage = $"已加载立方体点云 {pointCount} 个点";
    }

    /// <summary>
    /// 生成螺旋点云
    /// </summary>
    [RelayCommand]
    private void GenerateSpiralPointCloud()
    {
        var points = new List<PointCloudPoint>();

        int pointCount = 5000;
        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / pointCount * 10 * MathF.PI;
            float r = t / (10 * MathF.PI);

            float x = r * MathF.Cos(t);
            float y = (float)i / pointCount * 2 - 1;
            float z = r * MathF.Sin(t);

            // 渐变色
            float hue = (float)i / pointCount * 360;
            var (red, green, blue) = HsvToRgb(hue, 0.8f, 1f);

            points.Add(new PointCloudPoint(x, y, z, red, green, blue, 1.0f));
        }

        Points = points;
        StatusMessage = $"已加载螺旋点云 {pointCount} 个点";
    }

    /// <summary>
    /// 处理 ROI 选择事件
    /// </summary>
    public void OnRoiSelected(RoiSelectionEventArgs e)
    {
        SelectedPoints = e.SelectedPoints;
        SelectedIndices = e.SelectedIndices;
        SelectedPointCount = e.SelectedIndices.Count;
        StatusMessage = $"选中了 {SelectedPointCount} 个点";
    }

    /// <summary>
    /// 增加点大小
    /// </summary>
    [RelayCommand]
    private void IncreasePointSize()
    {
        PointSize = Math.Min(PointSize + 1, 20);
    }

    /// <summary>
    /// 减小点大小
    /// </summary>
    [RelayCommand]
    private void DecreasePointSize()
    {
        PointSize = Math.Max(PointSize - 1, 1);
    }

    /// <summary>
    /// HSV 转 RGB
    /// </summary>
    private static (float r, float g, float b) HsvToRgb(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1 - MathF.Abs(h / 60 % 2 - 1));
        float m = v - c;

        float r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return (r + m, g + m, b + m);
    }
}
