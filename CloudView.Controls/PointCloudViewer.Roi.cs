using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    private enum RoiHandleKind
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ,
        RotateX,
        RotateY,
        RotateZ,
    }

    private sealed class RoiHandleVisual
    {
        public required RoiBase Roi { get; init; }

        public required RoiHandleKind Kind { get; init; }

        public required Vector3 Position { get; init; }

        public required Vector3 LocalAxis { get; init; }
    }

    private readonly struct RoiScreenSegment
    {
        public RoiScreenSegment(RoiBase roi, Vector3 start, Vector3 end)
        {
            Roi = roi;
            Start = start;
            End = end;
        }

        public RoiBase Roi { get; }

        public Vector3 Start { get; }

        public Vector3 End { get; }
    }

    private static void OnRoisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            viewer.OnRoiInputsChanged();
        }
    }

    /// <summary>
    /// 刷新 ROI 可视化与业务结果。
    /// </summary>
    public void RefreshRois()
    {
        OnRoiInputsChanged();
    }

    /// <summary>
    /// 基于当前活动 ROI 执行点云筛选。
    /// </summary>
    public RoiFilterResult FilterActiveRoi()
    {
        UpdateRoiResults();
        return _activeRoiFilterResult;
    }

    /// <summary>
    /// 计算当前活动 ROI 的点云统计信息。
    /// </summary>
    public RoiStatisticsResult CalculateActiveRoiStatistics()
    {
        UpdateRoiResults();
        return _activeRoiStatisticsResult;
    }

    /// <summary>
    /// 基于当前活动 ROI 执行点云裁剪。
    /// </summary>
    public RoiCropResult CropActiveRoi(RoiCropMode mode)
    {
        return RoiPointQueryService.Crop(Points, _activeRoi, mode);
    }

    private void OnRoiInputsChanged()
    {
        EnsureActiveRoi();
        _roiNeedsRebuild = true;
        UpdateRoiResults();
        _needsRender = true;
    }

    private void EnsureActiveRoi()
    {
        if (_activeRoi != null && Rois?.Contains(_activeRoi) == true && _activeRoi.IsVisible)
        {
            return;
        }

        SetActiveRoi(Rois?.FirstOrDefault(r => r.IsVisible));
    }

    private void SetActiveRoi(RoiBase? roi)
    {
        if (ReferenceEquals(_activeRoi, roi))
        {
            return;
        }

        _activeRoi = roi;
        _roiNeedsRebuild = true;
        UpdateRoiResults();
        ActiveRoiChanged?.Invoke(this, new ActiveRoiChangedEventArgs(_activeRoi));
        _needsRender = true;
    }

    private void UpdateRoiResults()
    {
        _activeRoiFilterResult = RoiPointQueryService.Filter(Points, _activeRoi);
        _activeRoiStatisticsResult = RoiPointQueryService.CalculateStatistics(_activeRoiFilterResult);

        SelectedIndices = _activeRoiFilterResult.SelectedIndices;
        SelectedPoints = _activeRoiFilterResult.SelectedPoints;

        var eventArgs = new RoiSelectionEventArgs(
            _activeRoiFilterResult.SelectedIndices,
            _activeRoiFilterResult.SelectedPoints,
            GetProjectedBounds(_activeRoi));
        eventArgs.RoutedEvent = RoiSelectedEvent;
        RaiseEvent(eventArgs);

        RoiResultsChanged?.Invoke(this, new RoiResultsChangedEventArgs(_activeRoi, _activeRoiFilterResult, _activeRoiStatisticsResult));
    }

    private void CleanupRoiBuffers()
    {
        ClearRoiBuffers();
    }

    private void ClearRoiBuffers()
    {
        if (_gl == null)
        {
            _roiRenderItems.Clear();
            _roiVisualShapes.Clear();
            _roiHandleVisuals.Clear();
            _roiScreenSegments.Clear();
            return;
        }

        foreach (var item in _roiRenderItems)
        {
            if (item.Vao != 0)
            {
                _gl.DeleteVertexArray(item.Vao);
            }

            if (item.Vbo != 0)
            {
                _gl.DeleteBuffer(item.Vbo);
            }
        }

        _roiRenderItems.Clear();
        _roiVisualShapes.Clear();
        _roiHandleVisuals.Clear();
        _roiScreenSegments.Clear();
    }

    private void UpdateRoiBuffers()
    {
        if (_gl == null || !_isInitialized)
        {
            return;
        }

        ClearRoiBuffers();

        if (Rois == null || Rois.Count == 0)
        {
            _roiNeedsRebuild = false;
            return;
        }

        foreach (var roi in Rois.Where(r => r.IsVisible))
        {
            AddRoiBodyVisuals(roi);

            if (ReferenceEquals(roi, _activeRoi) && !roi.IsLocked)
            {
                AddRoiEditVisuals(roi);
            }
        }

        foreach (var shape in _roiVisualShapes)
        {
            var builder = ResolveBuilder(shape.GetType());
            if (builder == null)
            {
                continue;
            }

            var geometry = builder.Build(shape);
            if (geometry.IsEmpty)
            {
                continue;
            }

            var renderItem = CreateShapeRenderItem(geometry);
            if (renderItem.HasValue)
            {
                _roiRenderItems.Add(renderItem.Value);
            }
        }

        _roiNeedsRebuild = false;
    }

    private void RenderRois()
    {
        if (_gl == null)
        {
            return;
        }

        if (_roiNeedsRebuild)
        {
            UpdateRoiBuffers();
        }

        if (_roiRenderItems.Count == 0)
        {
            return;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        for (int i = 0; i < _roiRenderItems.Count; i++)
        {
            var item = _roiRenderItems[i];
            _gl.LineWidth(item.LineWidth);
            _gl.BindVertexArray(item.Vao);
            _gl.DrawArrays(item.PrimitiveType, 0, (uint)item.VertexCount);
            _gl.BindVertexArray(0);
        }

        _gl.LineWidth(1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
    }

    private void AddRoiBodyVisuals(RoiBase roi)
    {
        Color baseColor = ReferenceEquals(roi, _activeRoi)
            ? Brighten(roi.Color, 28)
            : ApplyAlpha(roi.Color, 210);
        float lineWidth = ReferenceEquals(roi, _activeRoi) ? 2.4f : 1.6f;

        switch (roi)
        {
            case BoxRoi box:
                BuildBoxVisuals(box, baseColor, lineWidth, includeHandles: false);
                break;
            case SphereRoi sphere:
                BuildSphereVisuals(sphere, baseColor, lineWidth, includeHandles: false);
                break;
            case CylinderRoi cylinder:
                BuildCylinderVisuals(cylinder, baseColor, lineWidth, includeHandles: false);
                break;
            case ConeRoi cone:
                BuildConeVisuals(cone, baseColor, lineWidth, includeHandles: false);
                break;
        }
    }

    private void AddRoiEditVisuals(RoiBase roi)
    {
        Color handleColor = Colors.Gold;
        float handleLineWidth = 2.0f;

        switch (roi)
        {
            case BoxRoi box:
                BuildBoxVisuals(box, handleColor, handleLineWidth, includeHandles: true);
                break;
            case SphereRoi sphere:
                BuildSphereVisuals(sphere, handleColor, handleLineWidth, includeHandles: true);
                break;
            case CylinderRoi cylinder:
                BuildCylinderVisuals(cylinder, handleColor, handleLineWidth, includeHandles: true);
                break;
            case ConeRoi cone:
                BuildConeVisuals(cone, handleColor, handleLineWidth, includeHandles: true);
                break;
        }

        AddRotationVisuals(roi);
    }

    private void BuildBoxVisuals(BoxRoi box, Color color, float lineWidth, bool includeHandles)
    {
        Vector3 half = box.Size * 0.5f;
        Vector3[] corners =
        {
            box.LocalToWorld(new Vector3(-half.X, -half.Y, -half.Z)),
            box.LocalToWorld(new Vector3(half.X, -half.Y, -half.Z)),
            box.LocalToWorld(new Vector3(half.X, half.Y, -half.Z)),
            box.LocalToWorld(new Vector3(-half.X, half.Y, -half.Z)),
            box.LocalToWorld(new Vector3(-half.X, -half.Y, half.Z)),
            box.LocalToWorld(new Vector3(half.X, -half.Y, half.Z)),
            box.LocalToWorld(new Vector3(half.X, half.Y, half.Z)),
            box.LocalToWorld(new Vector3(-half.X, half.Y, half.Z)),
        };

        AddPolylineShape(new[] { corners[0], corners[1], corners[2], corners[3] }, color, lineWidth, true, box, addSegments: true);
        AddPolylineShape(new[] { corners[4], corners[5], corners[6], corners[7] }, color, lineWidth, true, box, addSegments: true);
        AddLineShape(corners[0], corners[4], color, lineWidth, box, addSegments: true);
        AddLineShape(corners[1], corners[5], color, lineWidth, box, addSegments: true);
        AddLineShape(corners[2], corners[6], color, lineWidth, box, addSegments: true);
        AddLineShape(corners[3], corners[7], color, lineWidth, box, addSegments: true);

        if (!includeHandles)
        {
            return;
        }

        AddResizeHandle(box, RoiHandleKind.PositiveX, new Vector3(half.X, 0, 0), color);
        AddResizeHandle(box, RoiHandleKind.NegativeX, new Vector3(-half.X, 0, 0), color);
        AddResizeHandle(box, RoiHandleKind.PositiveY, new Vector3(0, half.Y, 0), color);
        AddResizeHandle(box, RoiHandleKind.NegativeY, new Vector3(0, -half.Y, 0), color);
        AddResizeHandle(box, RoiHandleKind.PositiveZ, new Vector3(0, 0, half.Z), color);
        AddResizeHandle(box, RoiHandleKind.NegativeZ, new Vector3(0, 0, -half.Z), color);
    }

    private void BuildSphereVisuals(SphereRoi sphere, Color color, float lineWidth, bool includeHandles)
    {
        AddCircleShape(sphere, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, sphere.Radius, color, lineWidth);
        AddCircleShape(sphere, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, sphere.Radius, color, lineWidth);
        AddCircleShape(sphere, Vector3.Zero, Vector3.UnitY, Vector3.UnitZ, sphere.Radius, color, lineWidth);

        if (!includeHandles)
        {
            return;
        }

        AddResizeHandle(sphere, RoiHandleKind.PositiveX, new Vector3(sphere.Radius, 0, 0), color);
        AddResizeHandle(sphere, RoiHandleKind.PositiveY, new Vector3(0, sphere.Radius, 0), color);
        AddResizeHandle(sphere, RoiHandleKind.PositiveZ, new Vector3(0, 0, sphere.Radius), color);
    }

    private void BuildCylinderVisuals(CylinderRoi cylinder, Color color, float lineWidth, bool includeHandles)
    {
        float halfHeight = cylinder.Height * 0.5f;
        AddCircleShape(cylinder, new Vector3(0, halfHeight, 0), Vector3.UnitX, Vector3.UnitZ, cylinder.Radius, color, lineWidth);
        AddCircleShape(cylinder, new Vector3(0, -halfHeight, 0), Vector3.UnitX, Vector3.UnitZ, cylinder.Radius, color, lineWidth);

        Vector3[] spokes =
        {
            new(cylinder.Radius, halfHeight, 0),
            new(-cylinder.Radius, halfHeight, 0),
            new(0, halfHeight, cylinder.Radius),
            new(0, halfHeight, -cylinder.Radius),
        };

        foreach (var top in spokes)
        {
            AddLineShape(cylinder.LocalToWorld(top), cylinder.LocalToWorld(new Vector3(top.X, -halfHeight, top.Z)), color, lineWidth, cylinder, addSegments: true);
        }

        if (!includeHandles)
        {
            return;
        }

        AddResizeHandle(cylinder, RoiHandleKind.PositiveX, new Vector3(cylinder.Radius, 0, 0), color);
        AddResizeHandle(cylinder, RoiHandleKind.PositiveZ, new Vector3(0, 0, cylinder.Radius), color);
        AddResizeHandle(cylinder, RoiHandleKind.PositiveY, new Vector3(0, halfHeight, 0), color);
        AddResizeHandle(cylinder, RoiHandleKind.NegativeY, new Vector3(0, -halfHeight, 0), color);
    }

    private void BuildConeVisuals(ConeRoi cone, Color color, float lineWidth, bool includeHandles)
    {
        float halfHeight = cone.Height * 0.5f;
        Vector3 apex = new(0, halfHeight, 0);
        Vector3 baseCenter = new(0, -halfHeight, 0);

        AddCircleShape(cone, baseCenter, Vector3.UnitX, Vector3.UnitZ, cone.Radius, color, lineWidth);

        Vector3[] basePoints =
        {
            new(cone.Radius, -halfHeight, 0),
            new(-cone.Radius, -halfHeight, 0),
            new(0, -halfHeight, cone.Radius),
            new(0, -halfHeight, -cone.Radius),
        };

        foreach (var point in basePoints)
        {
            AddLineShape(cone.LocalToWorld(apex), cone.LocalToWorld(point), color, lineWidth, cone, addSegments: true);
        }

        if (!includeHandles)
        {
            return;
        }

        AddResizeHandle(cone, RoiHandleKind.PositiveX, new Vector3(cone.Radius, -halfHeight, 0), color);
        AddResizeHandle(cone, RoiHandleKind.PositiveY, apex, color);
        AddResizeHandle(cone, RoiHandleKind.NegativeY, baseCenter, color);
    }

    private void AddRotationVisuals(RoiBase roi)
    {
        float radius = MathF.Max(roi.GetBoundingRadius() * 1.2f, GetHandleScale(roi) * 2.5f);
        AddCircleShape(roi, Vector3.Zero, Vector3.UnitY, Vector3.UnitZ, radius, ApplyAlpha(Colors.IndianRed, 210), 1.5f);
        AddCircleShape(roi, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, radius, ApplyAlpha(Colors.LightGreen, 210), 1.5f);
        AddCircleShape(roi, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, radius, ApplyAlpha(Colors.SkyBlue, 210), 1.5f);

        AddRotationHandle(roi, RoiHandleKind.RotateX, new Vector3(0, radius, 0), Colors.IndianRed);
        AddRotationHandle(roi, RoiHandleKind.RotateY, new Vector3(radius, 0, 0), Colors.LightGreen);
        AddRotationHandle(roi, RoiHandleKind.RotateZ, new Vector3(radius, 0, 0), Colors.SkyBlue);
    }

    private void AddResizeHandle(RoiBase roi, RoiHandleKind kind, Vector3 localPosition, Color color)
    {
        Vector3 worldPosition = roi.LocalToWorld(localPosition);
        _roiHandleVisuals.Add(new RoiHandleVisual
        {
            Roi = roi,
            Kind = kind,
            Position = worldPosition,
            LocalAxis = Vector3.Normalize(localPosition == Vector3.Zero ? Vector3.UnitX : localPosition),
        });

        AddHandleMarker(worldPosition, ApplyAlpha(color, 255), roi);
    }

    private void AddRotationHandle(RoiBase roi, RoiHandleKind kind, Vector3 localPosition, Color color)
    {
        Vector3 axis = kind switch
        {
            RoiHandleKind.RotateX => Vector3.UnitX,
            RoiHandleKind.RotateY => Vector3.UnitY,
            _ => Vector3.UnitZ,
        };

        Vector3 worldPosition = roi.LocalToWorld(localPosition);
        _roiHandleVisuals.Add(new RoiHandleVisual
        {
            Roi = roi,
            Kind = kind,
            Position = worldPosition,
            LocalAxis = axis,
        });

        AddHandleMarker(worldPosition, ApplyAlpha(color, 255), roi);
    }

    private void AddHandleMarker(Vector3 worldPosition, Color color, RoiBase roi)
    {
        float scale = GetHandleScale(roi);
        AddLineShape(worldPosition + new Vector3(-scale, 0, 0), worldPosition + new Vector3(scale, 0, 0), color, 2.2f, roi, addSegments: false);
        AddLineShape(worldPosition + new Vector3(0, -scale, 0), worldPosition + new Vector3(0, scale, 0), color, 2.2f, roi, addSegments: false);
        AddLineShape(worldPosition + new Vector3(0, 0, -scale), worldPosition + new Vector3(0, 0, scale), color, 2.2f, roi, addSegments: false);
    }

    private void AddCircleShape(RoiBase roi, Vector3 localCenter, Vector3 localAxisX, Vector3 localAxisY, float radius, Color color, float lineWidth)
    {
        var points = CreateCirclePoints(roi, localCenter, localAxisX, localAxisY, radius, 40);
        AddPolylineShape(points, color, lineWidth, true, roi, addSegments: true);
    }

    private Vector3[] CreateCirclePoints(RoiBase roi, Vector3 localCenter, Vector3 localAxisX, Vector3 localAxisY, float radius, int segments)
    {
        var points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = MathF.PI * 2f * i / segments;
            Vector3 local = localCenter + localAxisX * (MathF.Cos(angle) * radius) + localAxisY * (MathF.Sin(angle) * radius);
            points[i] = roi.LocalToWorld(local);
        }

        return points;
    }

    private void AddPolylineShape(IEnumerable<Vector3> vertices, Color color, float lineWidth, bool isClosed, RoiBase roi, bool addSegments)
    {
        var list = vertices.ToList();
        if (list.Count < 2)
        {
            return;
        }

        _roiVisualShapes.Add(new LineSharp(list, color, lineWidth, isClosed)
        {
            Name = $"{roi.Name}-{roi.Kind}-Polyline"
        });

        if (!addSegments)
        {
            return;
        }

        for (int i = 0; i < list.Count - 1; i++)
        {
            _roiScreenSegments.Add(new RoiScreenSegment(roi, list[i], list[i + 1]));
        }

        if (isClosed)
        {
            _roiScreenSegments.Add(new RoiScreenSegment(roi, list[^1], list[0]));
        }
    }

    private void AddLineShape(Vector3 start, Vector3 end, Color color, float lineWidth, RoiBase roi, bool addSegments)
    {
        _roiVisualShapes.Add(new LineSharp(new[] { start, end }, color, lineWidth)
        {
            Name = $"{roi.Name}-{roi.Kind}-Line"
        });

        if (addSegments)
        {
            _roiScreenSegments.Add(new RoiScreenSegment(roi, start, end));
        }
    }

    private float GetHandleScale(RoiBase roi)
    {
        float distance = Vector3.Distance(_cameraPosition, roi.Center);
        return MathF.Max(0.03f, distance * 0.03f);
    }

    private static Color ApplyAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Brighten(Color color, byte delta)
    {
        byte ClampAdd(byte value) => (byte)Math.Min(255, value + delta);
        return Color.FromArgb(color.A, ClampAdd(color.R), ClampAdd(color.G), ClampAdd(color.B));
    }

    private Rect GetProjectedBounds(RoiBase? roi)
    {
        if (roi == null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        var mvp = GetCurrentMvp(width, height);
        var points = _roiScreenSegments.Where(s => ReferenceEquals(s.Roi, roi))
            .SelectMany(s => new[] { WorldToScreen(s.Start, mvp, width, height), WorldToScreen(s.End, mvp, width, height) })
            .ToList();
        if (points.Count == 0)
        {
            return Rect.Empty;
        }

        double minX = points.Min(p => p.X);
        double maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxY = points.Max(p => p.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private Matrix4x4 GetCurrentMvp(int width, int height)
    {
        var model = Matrix4x4.Identity;
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);
        return model * view * projection;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private partial bool TryBeginRoiInteraction(Point position)
    {
        if (Rois == null || Rois.Count == 0)
        {
            return false;
        }

        if (TryFindHandle(position, out var handle))
        {
            SetActiveRoi(handle.Roi);
            _activeRoiHandle = handle;
            _roiInteractionMode = IsRotationHandle(handle.Kind) ? RoiInteractionMode.Rotate : RoiInteractionMode.Resize;
            _lastInteractionScreenPoint = position;
            _lastMousePosition = position;

            if (_roiInteractionMode == RoiInteractionMode.Resize)
            {
                BeginPlaneInteraction(handle.Position, position);
            }

            CaptureMouse();
            return true;
        }

        if (TryFindBody(position, out var roi))
        {
            SetActiveRoi(roi);
            _activeRoiHandle = null;
            _roiInteractionMode = RoiInteractionMode.Move;
            _lastInteractionScreenPoint = position;
            _lastMousePosition = position;
            BeginPlaneInteraction(roi.Center, position);
            CaptureMouse();
            return true;
        }

        return false;
    }

    private void BeginPlaneInteraction(Vector3 planePoint, Point position)
    {
        _interactionPlanePoint = planePoint;
        _interactionPlaneNormal = Vector3.Normalize(_cameraTarget - _cameraPosition);
        if (_interactionPlaneNormal.LengthSquared() <= 1e-6f)
        {
            _interactionPlaneNormal = Vector3.UnitZ;
        }

        if (TryIntersectScreenWithPlane(position, _interactionPlanePoint, _interactionPlaneNormal, out var worldPoint))
        {
            _lastInteractionWorldPoint = worldPoint;
        }
        else
        {
            _lastInteractionWorldPoint = planePoint;
        }
    }

    private partial void UpdateRoiInteraction(Point currentPosition)
    {
        if (_activeRoi == null || _activeRoi.IsLocked || _roiInteractionMode == RoiInteractionMode.None)
        {
            return;
        }

        switch (_roiInteractionMode)
        {
            case RoiInteractionMode.Move:
                ApplyMoveInteraction(currentPosition);
                break;
            case RoiInteractionMode.Resize:
                ApplyResizeInteraction(currentPosition);
                break;
            case RoiInteractionMode.Rotate:
                ApplyRotationInteraction(currentPosition);
                break;
        }

        _roiNeedsRebuild = true;
        _needsRender = true;
    }

    private void ApplyMoveInteraction(Point currentPosition)
    {
        if (_activeRoi == null)
        {
            return;
        }

        if (!TryIntersectScreenWithPlane(currentPosition, _interactionPlanePoint, _interactionPlaneNormal, out var worldPoint))
        {
            return;
        }

        var delta = worldPoint - _lastInteractionWorldPoint;
        _activeRoi.Center += delta;
        _interactionPlanePoint += delta;
        _lastInteractionWorldPoint = worldPoint;
    }

    private void ApplyResizeInteraction(Point currentPosition)
    {
        if (_activeRoi == null || _activeRoiHandle == null)
        {
            return;
        }

        if (!TryIntersectScreenWithPlane(currentPosition, _interactionPlanePoint, _interactionPlaneNormal, out var worldPoint))
        {
            return;
        }

        var deltaWorld = worldPoint - _lastInteractionWorldPoint;
        _lastInteractionWorldPoint = worldPoint;

        switch (_activeRoi)
        {
            case BoxRoi box:
                ResizeBox(box, _activeRoiHandle, deltaWorld);
                break;
            case SphereRoi sphere:
                ResizeSphere(sphere, _activeRoiHandle, deltaWorld);
                break;
            case CylinderRoi cylinder:
                ResizeCylinder(cylinder, _activeRoiHandle, deltaWorld);
                break;
            case ConeRoi cone:
                ResizeCone(cone, _activeRoiHandle, deltaWorld);
                break;
        }
    }

    private void ApplyRotationInteraction(Point currentPosition)
    {
        if (_activeRoi == null || _activeRoiHandle == null)
        {
            return;
        }

        var delta = currentPosition - _lastInteractionScreenPoint;
        float angle = (float)(delta.X + delta.Y) * 0.01f;
        if (MathF.Abs(angle) <= 1e-4f)
        {
            return;
        }

        Vector3 axis = _activeRoiHandle.Kind switch
        {
            RoiHandleKind.RotateX => Vector3.UnitX,
            RoiHandleKind.RotateY => Vector3.UnitY,
            _ => Vector3.UnitZ,
        };

        var rotationDelta = Quaternion.CreateFromAxisAngle(axis, angle);
        _activeRoi.Rotation = Quaternion.Normalize(_activeRoi.Rotation * rotationDelta);
        _lastInteractionScreenPoint = currentPosition;
    }

    private void ResizeBox(BoxRoi box, RoiHandleVisual handle, Vector3 deltaWorld)
    {
        Vector3 axis = box.LocalAxisToWorld(GetPrincipalAxis(handle.Kind));
        float delta = Vector3.Dot(deltaWorld, axis);
        Vector3 size = box.Size;
        float sign = GetHandleSign(handle.Kind);

        if (handle.Kind is RoiHandleKind.PositiveX or RoiHandleKind.NegativeX)
        {
            size.X = MathF.Max(0.01f, size.X + sign * delta);
        }
        else if (handle.Kind is RoiHandleKind.PositiveY or RoiHandleKind.NegativeY)
        {
            size.Y = MathF.Max(0.01f, size.Y + sign * delta);
        }
        else if (handle.Kind is RoiHandleKind.PositiveZ or RoiHandleKind.NegativeZ)
        {
            size.Z = MathF.Max(0.01f, size.Z + sign * delta);
        }

        box.Size = size;
        box.Center += axis * (delta * 0.5f);
    }

    private void ResizeSphere(SphereRoi sphere, RoiHandleVisual handle, Vector3 deltaWorld)
    {
        Vector3 axis = sphere.LocalAxisToWorld(GetPrincipalAxis(handle.Kind));
        float delta = Vector3.Dot(deltaWorld, axis);
        sphere.Radius = MathF.Max(0.01f, sphere.Radius + delta);
    }

    private void ResizeCylinder(CylinderRoi cylinder, RoiHandleVisual handle, Vector3 deltaWorld)
    {
        if (handle.Kind is RoiHandleKind.PositiveX or RoiHandleKind.PositiveZ)
        {
            Vector3 axis = cylinder.LocalAxisToWorld(GetPrincipalAxis(handle.Kind));
            float delta = Vector3.Dot(deltaWorld, axis);
            cylinder.Radius = MathF.Max(0.01f, cylinder.Radius + delta);
            return;
        }

        Vector3 heightAxis = cylinder.LocalAxisToWorld(Vector3.UnitY);
        float heightDelta = Vector3.Dot(deltaWorld, heightAxis);
        cylinder.Height = MathF.Max(0.01f, cylinder.Height + GetHandleSign(handle.Kind) * heightDelta);
        cylinder.Center += heightAxis * (heightDelta * 0.5f);
    }

    private void ResizeCone(ConeRoi cone, RoiHandleVisual handle, Vector3 deltaWorld)
    {
        if (handle.Kind == RoiHandleKind.PositiveX)
        {
            Vector3 axis = cone.LocalAxisToWorld(Vector3.UnitX);
            float delta = Vector3.Dot(deltaWorld, axis);
            cone.Radius = MathF.Max(0.01f, cone.Radius + delta);
            return;
        }

        Vector3 heightAxis = cone.LocalAxisToWorld(Vector3.UnitY);
        float heightDelta = Vector3.Dot(deltaWorld, heightAxis);
        cone.Height = MathF.Max(0.01f, cone.Height + GetHandleSign(handle.Kind) * heightDelta);
        cone.Center += heightAxis * (heightDelta * 0.5f);
    }

    private static bool IsRotationHandle(RoiHandleKind kind)
    {
        return kind is RoiHandleKind.RotateX or RoiHandleKind.RotateY or RoiHandleKind.RotateZ;
    }

    private static Vector3 GetPrincipalAxis(RoiHandleKind kind)
    {
        return kind switch
        {
            RoiHandleKind.PositiveX or RoiHandleKind.NegativeX or RoiHandleKind.RotateX => Vector3.UnitX,
            RoiHandleKind.PositiveY or RoiHandleKind.NegativeY or RoiHandleKind.RotateY => Vector3.UnitY,
            _ => Vector3.UnitZ,
        };
    }

    private static float GetHandleSign(RoiHandleKind kind)
    {
        return kind switch
        {
            RoiHandleKind.NegativeX or RoiHandleKind.NegativeY or RoiHandleKind.NegativeZ => -1f,
            _ => 1f,
        };
    }

    private bool TryFindHandle(Point position, out RoiHandleVisual handle)
    {
        handle = null!;
        if (_activeRoi == null || _roiHandleVisuals.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        var mvp = GetCurrentMvp(width, height);
        double bestDistance = double.MaxValue;
        RoiHandleVisual? best = null;

        foreach (var candidate in _roiHandleVisuals.Where(h => ReferenceEquals(h.Roi, _activeRoi)))
        {
            var screen = WorldToScreen(candidate.Position, mvp, width, height);
            double distance = (screen - position).Length;
            if (distance < 12 && distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best == null)
        {
            return false;
        }

        handle = best;
        return true;
    }

    private bool TryFindBody(Point position, out RoiBase roi)
    {
        roi = null!;
        if (_roiScreenSegments.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        var mvp = GetCurrentMvp(width, height);
        double bestDistance = double.MaxValue;
        RoiBase? best = null;

        foreach (var segment in _roiScreenSegments)
        {
            var start = WorldToScreen(segment.Start, mvp, width, height);
            var end = WorldToScreen(segment.End, mvp, width, height);
            double distance = DistanceToSegment(position, start, end);
            if (distance < 10 && distance < bestDistance)
            {
                bestDistance = distance;
                best = segment.Roi;
            }
        }

        if (best == null)
        {
            return false;
        }

        roi = best;
        return true;
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        System.Windows.Vector segment = end - start;
        double lengthSquared = segment.LengthSquared;
        if (lengthSquared <= double.Epsilon)
        {
            return (point - start).Length;
        }

        double t = System.Windows.Vector.Multiply(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        Point projection = start + segment * t;
        return (point - projection).Length;
    }

    private bool TryIntersectScreenWithPlane(Point screenPoint, Vector3 planePoint, Vector3 planeNormal, out Vector3 intersection)
    {
        intersection = Vector3.Zero;
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        if (!TryCreateScreenRay(screenPoint, (int)ActualWidth, (int)ActualHeight, out var origin, out var direction))
        {
            return false;
        }

        float denominator = Vector3.Dot(direction, planeNormal);
        if (MathF.Abs(denominator) < 1e-5f)
        {
            return false;
        }

        float distance = Vector3.Dot(planePoint - origin, planeNormal) / denominator;
        if (distance < 0)
        {
            return false;
        }

        intersection = origin + direction * distance;
        return true;
    }

    private bool TryCreateScreenRay(Point screenPoint, int width, int height, out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.Zero;
        direction = Vector3.UnitZ;

        var mvp = GetCurrentMvp(width, height);
        if (!Matrix4x4.Invert(mvp, out var inverse))
        {
            return false;
        }

        float ndcX = (float)(screenPoint.X / width * 2 - 1);
        float ndcY = -(float)(screenPoint.Y / height * 2 - 1);

        var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, -1, 1), inverse);
        var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1, 1), inverse);

        if (MathF.Abs(nearPoint.W) < 1e-6f || MathF.Abs(farPoint.W) < 1e-6f)
        {
            return false;
        }

        origin = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z) / nearPoint.W;
        Vector3 far = new Vector3(farPoint.X, farPoint.Y, farPoint.Z) / farPoint.W;
        direction = Vector3.Normalize(far - origin);
        return direction.LengthSquared() > 1e-6f;
    }

    private partial void CompleteRoiInteraction(bool raiseEditedEvent)
    {
        if (_roiInteractionMode != RoiInteractionMode.None && _activeRoi != null)
        {
            _roiNeedsRebuild = true;
            UpdateRoiResults();
            if (raiseEditedEvent)
            {
                RoiEdited?.Invoke(this, new RoiEditedEventArgs(_activeRoi));
            }
        }

        _roiInteractionMode = RoiInteractionMode.None;
        _activeRoiHandle = null;
    }
}
