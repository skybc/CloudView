using System.Numerics;

namespace CloudView.Controls;

/// <summary>
/// ROI 点云裁剪模式。
/// </summary>
public enum RoiCropMode
{
    KeepInside,
    RemoveInside,
}

/// <summary>
/// ROI 点云筛选结果。
/// </summary>
public sealed class RoiFilterResult
{
    public static RoiFilterResult Empty { get; } = new(null, Array.Empty<int>(), Array.Empty<PointCloudPoint>());

    public RoiFilterResult(RoiBase? roi, IReadOnlyList<int> selectedIndices, IReadOnlyList<PointCloudPoint> selectedPoints)
    {
        Roi = roi;
        SelectedIndices = selectedIndices;
        SelectedPoints = selectedPoints;
    }

    public RoiBase? Roi { get; }

    public IReadOnlyList<int> SelectedIndices { get; }

    public IReadOnlyList<PointCloudPoint> SelectedPoints { get; }
}

/// <summary>
/// ROI 内点云统计结果。
/// </summary>
public sealed class RoiStatisticsResult
{
    public static RoiStatisticsResult Empty { get; } = new(null, 0, Vector3.Zero, Vector3.Zero, Vector3.Zero);

    public RoiStatisticsResult(RoiBase? roi, int pointCount, Vector3 centroid, Vector3 min, Vector3 max)
    {
        Roi = roi;
        PointCount = pointCount;
        Centroid = centroid;
        Min = min;
        Max = max;
    }

    public RoiBase? Roi { get; }

    public int PointCount { get; }

    public Vector3 Centroid { get; }

    public Vector3 Min { get; }

    public Vector3 Max { get; }
}

/// <summary>
/// ROI 点云裁剪结果。
/// </summary>
public sealed class RoiCropResult
{
    public RoiCropResult(RoiBase? roi, RoiCropMode mode, IReadOnlyList<int> keptIndices, IReadOnlyList<PointCloudPoint> keptPoints, IReadOnlyList<int> removedIndices, IReadOnlyList<PointCloudPoint> removedPoints)
    {
        Roi = roi;
        Mode = mode;
        KeptIndices = keptIndices;
        KeptPoints = keptPoints;
        RemovedIndices = removedIndices;
        RemovedPoints = removedPoints;
    }

    public RoiBase? Roi { get; }

    public RoiCropMode Mode { get; }

    public IReadOnlyList<int> KeptIndices { get; }

    public IReadOnlyList<PointCloudPoint> KeptPoints { get; }

    public IReadOnlyList<int> RemovedIndices { get; }

    public IReadOnlyList<PointCloudPoint> RemovedPoints { get; }
}

/// <summary>
/// ROI 结果变更事件参数。
/// </summary>
public sealed class RoiResultsChangedEventArgs : EventArgs
{
    public RoiResultsChangedEventArgs(RoiBase? activeRoi, RoiFilterResult filterResult, RoiStatisticsResult statisticsResult)
    {
        ActiveRoi = activeRoi;
        FilterResult = filterResult;
        StatisticsResult = statisticsResult;
    }

    public RoiBase? ActiveRoi { get; }

    public RoiFilterResult FilterResult { get; }

    public RoiStatisticsResult StatisticsResult { get; }
}

/// <summary>
/// ROI 编辑事件参数。
/// </summary>
public sealed class RoiEditedEventArgs : EventArgs
{
    public RoiEditedEventArgs(RoiBase roi)
    {
        Roi = roi;
    }

    public RoiBase Roi { get; }
}

/// <summary>
/// 活动 ROI 切换事件参数。
/// </summary>
public sealed class ActiveRoiChangedEventArgs : EventArgs
{
    public ActiveRoiChangedEventArgs(RoiBase? roi)
    {
        Roi = roi;
    }

    public RoiBase? Roi { get; }
}
