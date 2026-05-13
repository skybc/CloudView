using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    #region 鼠标事件处理

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!AllowDrawingRoi)
        {
            return;
        }

        _isDrawingRoi = true;
        _roiStart = e.GetPosition(this);
        _roiEnd = _roiStart;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(this);

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        if (width > 0 && height > 0)
        {
            _currentMouseWorldPosition = ScreenToWorld(currentPos, width, height);
            _needsRender = true;
        }

        if (_isDrawingRoi)
        {
            _roiEnd = currentPos;
            _needsRender = true;
        }
        else if (_isRotating)
        {
            var delta = currentPos - _lastMousePosition;
            _rotationY += (float)delta.X * 0.01f;
            _rotationX += (float)delta.Y * 0.01f;

            UpdateCameraPositionWithRotation();

            _lastMousePosition = currentPos;
            _needsRender = true;
        }
        else if (_isPanning)
        {
            var delta = currentPos - _lastMousePosition;

            var forward = Vector3.Normalize(_cameraTarget - _cameraPosition);
            var right = Vector3.Normalize(Vector3.Cross(forward, _cameraUp));
            var up = Vector3.Normalize(Vector3.Cross(right, forward));

            float panSpeed = _zoom * 0.01f;

            var panDelta = -right * (float)delta.X * panSpeed - up * (float)delta.Y * panSpeed;

            _cameraTarget += panDelta;
            _panOffset += panDelta;

            var direction = _cameraPosition - _cameraTarget;
            float distance = direction.Length();
            if (distance > 0)
            {
                var normalizedDirection = Vector3.Normalize(direction);
                _cameraPosition = _cameraTarget + normalizedDirection * _zoom;
            }

            _lastMousePosition = currentPos;
            _needsRender = true;
        }
    }

    private void UpdateCameraPositionWithRotation()
    {
        var direction = _cameraPosition - _cameraTarget;
        float distance = direction.Length();

        if (distance == 0) return;

        var rotationMatrix = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);
        var rotatedDirection = Vector3.Transform(new Vector3(0, 0, distance), rotationMatrix);

        _cameraPosition = _cameraTarget + rotatedDirection;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingRoi)
        {
            _isDrawingRoi = false;
            ReleaseMouseCapture();

            _roiEnd = e.GetPosition(this);

            var selectedIndices = new List<int>();
            var selectedPoints = new List<PointCloudPoint>();
            var screenRect = GetRoiRect();

            if (Points != null && screenRect.Width > 1 && screenRect.Height > 1)
            {
                int width = (int)ActualWidth;
                int height = (int)ActualHeight;

                var model = Matrix4x4.Identity;
                var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
                var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);
                var mvp = model * view * projection;

                for (int i = 0; i < Points.Count; i++)
                {
                    var point = Points[i];
                    var screenPos = WorldToScreen(point.Position, mvp, width, height);

                    if (screenRect.Contains(screenPos))
                    {
                        selectedIndices.Add(i);
                        selectedPoints.Add(point);
                    }
                }
            }

            SelectedIndices = selectedIndices;
            SelectedPoints = selectedPoints;

            var args = new RoiSelectionEventArgs(selectedIndices, selectedPoints, screenRect);
            args.RoutedEvent = RoiSelectedEvent;
            args.Source = this;
            RaiseEvent(args);

            _needsRender = true;
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRotating = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        ReleaseMouseCapture();
    }

    private void OnMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        ReleaseMouseCapture();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            OnMouseMiddleButtonDown(sender, e);
        }
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Released && _isPanning)
        {
            OnMouseMiddleButtonUp(sender, e);
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom -= e.Delta * 0.005f;
        _zoom = Math.Clamp(_zoom, 0.1f, 100f);

        var direction = _cameraPosition - _cameraTarget;
        float currentDistance = direction.Length();

        if (currentDistance > 0)
        {
            var normalizedDirection = Vector3.Normalize(direction);
            _cameraPosition = _cameraTarget + normalizedDirection * _zoom;
        }
        else
        {
            _cameraPosition = _cameraTarget + new Vector3(0, 0, _zoom);
        }

        _needsRender = true;
    }

    private Rect GetRoiRect()
    {
        double left = Math.Min(_roiStart.X, _roiEnd.X);
        double top = Math.Min(_roiStart.Y, _roiEnd.Y);
        double width = Math.Abs(_roiEnd.X - _roiStart.X);
        double height = Math.Abs(_roiEnd.Y - _roiStart.Y);
        return new Rect(left, top, width, height);
    }

    private static Point WorldToScreen(Vector3 worldPos, Matrix4x4 mvp, int width, int height)
    {
        var clipPos = Vector4.Transform(new Vector4(worldPos, 1), mvp);
        if (clipPos.W == 0) return new Point(-1, -1);

        var ndcPos = new Vector3(clipPos.X / clipPos.W, clipPos.Y / clipPos.W, clipPos.Z / clipPos.W);

        double screenX = (ndcPos.X + 1) * 0.5 * width;
        double screenY = (1 - ndcPos.Y) * 0.5 * height;

        return new Point(screenX, screenY);
    }

    private Vector3 ScreenToWorld(Point screenPos, int width, int height)
    {
        float ndcX = (float)(screenPos.X / width * 2 - 1);
        float ndcY = -(float)(screenPos.Y / height * 2 - 1);

        var model = Matrix4x4.Identity;
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);
        var mvp = model * view * projection;

        if (!Matrix4x4.Invert(mvp, out var mvpInverse))
        {
            return Vector3.Zero;
        }

        var ndcPos = new Vector4(ndcX, ndcY, -1, 1);
        var worldPos = Vector4.Transform(ndcPos, mvpInverse);

        if (worldPos.W != 0)
        {
            return new Vector3(worldPos.X / worldPos.W, worldPos.Y / worldPos.W, worldPos.Z / worldPos.W);
        }

        return Vector3.Zero;
    }

    #endregion
}
