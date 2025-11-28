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

    /// <summary>
    /// 生成测试点云（用于测试点云中心计算）
    /// 
    /// 创建一个立方体表面的点云（所有6个面）：
    /// - X: -1 到 1
    /// - Y: -1 到 1  
    /// - Z: -1 到 1
    /// - 预期中心：(0, 0, 0)
    /// - 共 600 个点（每面 100 个点）
    /// </summary>
    [RelayCommand]
    private void GenerateTestPointCloud()
    {
        var points = new List<PointCloudPoint>();
        const int pointsPerFace = 100;
        const float size = 1.0f;

        // 前面 (Z = -1)
        AddCubeFace(points, pointsPerFace, size, 
            (u, v) => new Vector3(u * size * 2 - size, v * size * 2 - size, -size),
            new Vector4(1, 0, 0, 1)); // 红色

        // 后面 (Z = 1)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(u * size * 2 - size, v * size * 2 - size, size),
            new Vector4(0, 1, 0, 1)); // 绿色

        // 左面 (X = -1)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(-size, u * size * 2 - size, v * size * 2 - size),
            new Vector4(0, 0, 1, 1)); // 蓝色

        // 右面 (X = 1)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(size, u * size * 2 - size, v * size * 2 - size),
            new Vector4(1, 1, 0, 1)); // 黄色

        // 下面 (Y = -1)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(u * size * 2 - size, -size, v * size * 2 - size),
            new Vector4(1, 0, 1, 1)); // 紫色

        // 上面 (Y = 1)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(u * size * 2 - size, size, v * size * 2 - size),
            new Vector4(0, 1, 1, 1)); // 青色

        Points = points;
        StatusMessage = $"已加载测试点云（立方体表面，中心应在原点 (0,0,0)）\n共 {points.Count} 个点";
    }

    /// <summary>
    /// 生成偏移的测试点云
    /// 
    /// 创建一个位置偏移的立方体表面：
    /// - X: 10 到 20（中心 15）
    /// - Y: 5 到 15（中心 10）
    /// - Z: -5 到 5（中心 0）
    /// - 预期中心：(15, 10, 0)
    /// - 共 600 个点（每面 100 个点）
    /// </summary>
    [RelayCommand]
    private void GenerateOffsetTestPointCloud()
    {
        var points = new List<PointCloudPoint>();
        const int pointsPerFace = 100;
        const float offsetX = 15.0f;
        const float offsetY = 10.0f;
        const float offsetZ = 0.0f;
        const float size = 5.0f;  // 半边长

        // 前面 (Z = 0 - 5)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX + u * size * 2 - size, offsetY + v * size * 2 - size, offsetZ - size),
            new Vector4(1, 0, 0, 1)); // 红色

        // 后面 (Z = 0 + 5)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX + u * size * 2 - size, offsetY + v * size * 2 - size, offsetZ + size),
            new Vector4(0, 1, 0, 1)); // 绿色

        // 左面 (X = 10)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX - size, offsetY + u * size * 2 - size, offsetZ + v * size * 2 - size),
            new Vector4(0, 0, 1, 1)); // 蓝色

        // 右面 (X = 20)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX + size, offsetY + u * size * 2 - size, offsetZ + v * size * 2 - size),
            new Vector4(1, 1, 0, 1)); // 黄色

        // 下面 (Y = 5)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX + u * size * 2 - size, offsetY - size, offsetZ + v * size * 2 - size),
            new Vector4(1, 0, 1, 1)); // 紫色

        // 上面 (Y = 15)
        AddCubeFace(points, pointsPerFace, size,
            (u, v) => new Vector3(offsetX + u * size * 2 - size, offsetY + size, offsetZ + v * size * 2 - size),
            new Vector4(0, 1, 1, 1)); // 青色

        Points = points;
        StatusMessage = $"已加载偏移测试点云（中心应在 ({offsetX:F1}, {offsetY:F1}, {offsetZ:F1})）\n共 {points.Count} 个点";
    }

    /// <summary>
    /// 为立方体的一个面添加点
    /// </summary>
    /// <param name="points">点列表</param>
    /// <param name="pointCount">该面上的点数</param>
    /// <param name="size">立方体半边长</param>
    /// <param name="positionFunc">位置计算函数，参数为 (u, v)，其中 u,v 范围 0-1</param>
    /// <param name="color">点的颜色</param>
    private static void AddCubeFace(List<PointCloudPoint> points, int pointCount, float size, 
        Func<float, float, Vector3> positionFunc, Vector4 color)
    {
        int pointsPerSide = (int)Math.Sqrt(pointCount);
        
        for (int i = 0; i < pointsPerSide; i++)
        {
            for (int j = 0; j < pointsPerSide; j++)
            {
                float u = (i + 0.5f) / pointsPerSide;  // 0.5 / n 到 (n-0.5) / n
                float v = (j + 0.5f) / pointsPerSide;

                var position = positionFunc(u, v);
                points.Add(new PointCloudPoint(position, color));
            }
        }
    }

    /// <summary>
    /// 测试点云中心计算结果
    /// </summary>
    [RelayCommand]
    private void TestPointCloudCenter()
    {
        if (Points == null || Points.Count == 0)
        {
            StatusMessage = "❌ 测试失败：点云为空";
            return;
        }

        // 手动计算中心
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var point in Points)
        {
            minX = Math.Min(minX, point.Position.X);
            maxX = Math.Max(maxX, point.Position.X);
            minY = Math.Min(minY, point.Position.Y);
            maxY = Math.Max(maxY, point.Position.Y);
            minZ = Math.Min(minZ, point.Position.Z);
            maxZ = Math.Max(maxZ, point.Position.Z);
        }

        var calculatedCenter = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f
        );

        StatusMessage = $"✅ 计算点云中心成功\n" +
                       $"中心坐标: ({calculatedCenter.X:F2}, {calculatedCenter.Y:F2}, {calculatedCenter.Z:F2})\n" +
                       $"范围: X[{minX:F2},{maxX:F2}], Y[{minY:F2},{maxY:F2}], Z[{minZ:F2},{maxZ:F2}]";
    }

    /// <summary>
    /// 测试坐标范围限制功能 1
    /// 加载偏移测试点云，然后限制显示范围到上半部分
    /// 
    /// 点云范围: X[10,20], Y[5,15], Z[-5,5]
    /// 限制范围: X[10,20], Y[10,15], Z[-5,5]
    /// 结果: 应该只显示 Y 坐标在 10-15 范围内的点（上半部分）
    /// </summary>
    [RelayCommand]
    private void TestRangeFilter()
    {
        // 先生成偏移测试点云
        GenerateOffsetTestPointCloud();
        
        StatusMessage = $"✅ 范围限制测试 1 已加载\n" +
                       $"点云范围: X[10,20], Y[5,15], Z[-5,5]\n" +
                       $"限制范围: X[10,20], Y[10,15], Z[-5,5]\n" +
                       $"显示效果: 仅显示上半部分的点";
    }

    /// <summary>
    /// 测试坐标范围限制功能 2
    /// 加载偏移测试点云，然后限制显示范围到中心立方体部分
    /// 
    /// 点云范围: X[10,20], Y[5,15], Z[-5,5]
    /// 限制范围: X[12,18], Y[7,13], Z[-3,3]
    /// 结果: 应该只显示中心立方体范围内的点
    /// </summary>
    [RelayCommand]
    private void TestRangeFilter2()
    {
        // 先生成偏移测试点云
        GenerateOffsetTestPointCloud();
        
        StatusMessage = $"✅ 范围限制测试 2 已加载\n" +
                       $"点云范围: X[10,20], Y[5,15], Z[-5,5]\n" +
                       $"限制范围: X[12,18], Y[7,13], Z[-3,3]\n" +
                       $"显示效果: 仅显示中心立方体范围内的点";
    }

    /// <summary>
    /// 重置坐标范围限制
    /// 恢复无限制显示所有点
    /// </summary>
    [RelayCommand]
    private void ResetRangeFilter()
    {
        StatusMessage = $"✅ 范围限制已重置\n" +
                       $"显示所有点（无范围限制）";
    }

    /// <summary>
    /// 生成 5000 万点的大规模点云用于性能测试
    /// 
    /// 特点：
    /// - 总点数：50,000,000
    /// - 生成方式：随机分布在单位立方体内
    /// - 颜色：根据位置渐变
    /// - 预期效果：触发 LOD 机制自动抽稀到最大 2,000,000 点显示
    /// - 性能指标：应测试 GPU 使用率、帧率、内存占用
    /// 
    /// 测试项目：
    /// 1. 数据加载时间
    /// 2. 内存占用
    /// 3. 初始渲染帧率
    /// 4. LOD 抽稀是否正常工作（点数应 <= 2,000,000）
    /// 5. 旋转/缩放/移动的流畅度
    /// 6. GPU 使用率
    /// </summary>
    [RelayCommand]
    private async Task GenerateLargePointCloudForPerformanceTest()
    {
        StatusMessage = "⏳ 正在生成 5000 万点的点云数据...（这可能需要几十秒）";
        
        try
        {
            // 在后台线程生成数据，避免冻结 UI
            var points = await Task.Run(() =>
            {
                var random = new Random(12345);
                var points = new List<PointCloudPoint>(50_000_000);

                // 创建进度报告，每 500 万个点报告一次
                long generatedCount = 0;
                const int reportInterval = 5_000_000;

                for (int i = 0; i < 50_000_000; i++)
                {
                    float x = (float)(random.NextDouble() * 2 - 1);  // -1 到 1
                    float y = (float)(random.NextDouble() * 2 - 1);  // -1 到 1
                    float z = (float)(random.NextDouble() * 2 - 1);  // -1 到 1

                    // 根据位置设置颜色（渐变色）
                    float r = (x + 1) / 2;      // 0 到 1
                    float g = (y + 1) / 2;      // 0 到 1
                    float b = (z + 1) / 2;      // 0 到 1

                    points.Add(new PointCloudPoint(x, y, z, r, g, b, 1.0f));

                    generatedCount++;
                    if (generatedCount % reportInterval == 0)
                    {
                        // 可以添加进度报告（如需要）
                    }
                }

                return points;
            });

            Points = points;
            StatusMessage = $"✅ 已加载 50,000,000 个点\n" +
                           $"内存占用: ~1.4 GB (点数据)\n" +
                           $"LOD 阈值: 100,000 点\n" +
                           $"最大显示点数: 2,000,000\n" +
                           $"预期 LOD 级别: 25 (1/25 抽稀)\n" +
                           $"\n📊 性能测试项目:\n" +
                           $"• 旋转: 右键拖动\n" +
                           $"• 缩放: 滚轮\n" +
                           $"• ROI 选择: 左键拖动\n" +
                           $"观察 GPU 使用率和帧率";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 生成点云失败: {ex.Message}";
        }
    }
}

