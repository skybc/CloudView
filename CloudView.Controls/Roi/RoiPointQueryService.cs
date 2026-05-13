using System.Numerics;

namespace CloudView.Controls;

internal static class RoiPointQueryService
{
    public static RoiFilterResult Filter(IList<PointCloudPoint>? points, RoiBase? roi)
    {
        if (points == null || points.Count == 0 || roi == null || !roi.IsVisible)
        {
            return RoiFilterResult.Empty;
        }

        var indices = new List<int>();
        var selectedPoints = new List<PointCloudPoint>();

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (!roi.Contains(point.Position))
            {
                continue;
            }

            indices.Add(i);
            selectedPoints.Add(point);
        }

        return new RoiFilterResult(roi, indices, selectedPoints);
    }

    public static RoiStatisticsResult CalculateStatistics(RoiFilterResult filterResult)
    {
        if (filterResult.SelectedPoints.Count == 0)
        {
            return RoiStatisticsResult.Empty;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var sum = Vector3.Zero;

        foreach (var point in filterResult.SelectedPoints)
        {
            min = Vector3.Min(min, point.Position);
            max = Vector3.Max(max, point.Position);
            sum += point.Position;
        }

        var centroid = sum / filterResult.SelectedPoints.Count;
        return new RoiStatisticsResult(filterResult.Roi, filterResult.SelectedPoints.Count, centroid, min, max);
    }

    public static RoiCropResult Crop(IList<PointCloudPoint>? points, RoiBase? roi, RoiCropMode mode)
    {
        if (points == null || points.Count == 0 || roi == null || !roi.IsVisible)
        {
            return new RoiCropResult(roi, mode, Array.Empty<int>(), Array.Empty<PointCloudPoint>(), Array.Empty<int>(), Array.Empty<PointCloudPoint>());
        }

        var keptIndices = new List<int>();
        var keptPoints = new List<PointCloudPoint>();
        var removedIndices = new List<int>();
        var removedPoints = new List<PointCloudPoint>();

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            bool isInside = roi.Contains(point.Position);
            bool keep = mode == RoiCropMode.KeepInside ? isInside : !isInside;

            if (keep)
            {
                keptIndices.Add(i);
                keptPoints.Add(point);
            }
            else
            {
                removedIndices.Add(i);
                removedPoints.Add(point);
            }
        }

        return new RoiCropResult(roi, mode, keptIndices, keptPoints, removedIndices, removedPoints);
    }
}
