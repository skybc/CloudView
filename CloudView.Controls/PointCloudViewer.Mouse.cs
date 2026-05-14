using System;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    #region 鼠标事件处理

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 左键先不直接决定“是选中还是旋转”，而是进入一个短暂的待判定窗口，
        // 后续由移动距离决定这是单击还是拖拽。
        var currentPos = e.GetPosition(this);
        TryBeginRoiInteraction(currentPos);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(this);
        const float orbitPitchLimit = 1.5533431f; // 89°，保留一点余量避免接近极点时翻转

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        if (width > 0 && height > 0)
        {
            // 鼠标的屏幕位置实时反算成世界坐标，供右上角覆盖层显示。
            _currentMouseWorldPosition = ScreenToWorld(currentPos, width, height);
            _needsRender = true;
        }

        UpdateHoveredHandle(currentPos);

        if (_roiInteractionMode != RoiInteractionMode.None)
        {
            // 一旦进入 ROI 编辑模式，拖拽优先解释为编辑，不再继续处理视图旋转/平移。
            UpdateRoiInteraction(currentPos);
            _lastMousePosition = currentPos;
            return;
        }

        TryPromotePendingLeftGestureToAction(currentPos);

        if (_isRotating)
        {
            // 左键拖拽视图时，按鼠标位移转换为欧拉角增量。
            // 注意：这里采用“拖哪边就朝哪边看”的直觉映射，所以角度增量需要与屏幕位移同向。
            var delta = currentPos - _lastMousePosition;
            _rotationY -= (float)delta.X * 0.01f;
            _rotationX -= (float)delta.Y * 0.01f;
            _rotationX = Math.Clamp(_rotationX, -orbitPitchLimit, orbitPitchLimit);

            UpdateCameraPositionWithRotation();

            _lastMousePosition = currentPos;
            _roiNeedsRebuild = true;
            _needsRender = true;
        }
        else if (_isPanning)
        {
            var delta = currentPos - _lastMousePosition;

            // 使用真实的相机-目标点距离计算屏幕像素到世界坐标的换算。
            // 这样平移手感会随视场和距离自然变化，而不是依赖一个经验缩放系数。
            float distance = Vector3.Distance(_cameraPosition, _cameraTarget);
            if (height <= 0 || distance <= 0f)
            {
                _lastMousePosition = currentPos;
                _needsRender = true;
                return;
            }

            float worldPerPixel = 2f * MathF.Tan(_fov * 0.5f * MathF.PI / 180f) * distance / height;

            // 使用稳定的世界上方向，避免在 forward 接近 up 时出现抖动、翻转或 NaN。
            Vector3 worldUp = Vector3.UnitY;

            var forward = Vector3.Normalize(_cameraTarget - _cameraPosition);
            var right = Vector3.Cross(forward, worldUp);
            if (right.LengthSquared() < 1e-6f)
                right = Vector3.UnitX;
            else
                right = Vector3.Normalize(right);

            var up = Vector3.Cross(right, forward);
            if (up.LengthSquared() > 0f)
                up = Vector3.Normalize(up);

            // 鼠标 X 对应左右平移，鼠标 Y 对应上下平移；方向取反后更符合人类直觉。
            var move = (-right * (float)delta.X + up * (float)delta.Y) * worldPerPixel;

            // Pan 必须让相机和目标点一起移动，保持相对关系不变，避免反推相机位置带来的数值误差。
            _cameraPosition += move;
            _cameraTarget += move;
            _panOffset += move;

            // 平移只改变视野中心，不改变相机朝向。
            // 这里同步旋转参数基准，避免下一次左键旋转仍然沿用旧的欧拉角状态，
            // 从而出现“旋转中心看起来不对”或旋转跳变。
            SyncRotationFromCameraOffset();

            _lastMousePosition = currentPos;
            _roiNeedsRebuild = true;
            _needsRender = true;
        }
    }

    private void UpdateCameraPositionWithRotation()
    {
        // Orbit 的核心：保留相机到目标点距离，把初始朝向向量旋转到新方向。
        var direction = _cameraPosition - _cameraTarget;
        float distance = direction.Length();

        if (distance == 0) return;

        var rotationMatrix = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);
        var rotatedDirection = Vector3.Transform(new Vector3(0, 0, distance), rotationMatrix);

        _cameraPosition = _cameraTarget + rotatedDirection;
    }

    private void SyncRotationFromCameraOffset()
    {
        // 当平移/缩放直接改变了相机位置时，需要重新从位置反推欧拉角，
        // 否则下一次 Orbit 会基于过时的旋转基准产生跳变。
        var direction = _cameraPosition - _cameraTarget;
        float distance = direction.Length();
        const float orbitPitchLimit = 1.5533431f; // 89°，避免 pitch 反推后落在极点外

        if (distance <= 0f)
            return;

        var normalizedDirection = direction / distance;

        // 与 UpdateCameraPositionWithRotation() 的旋转构造保持一致：
        // 先绕 X，再绕 Y。这样平移后重新建立的欧拉角能准确描述当前相机位置。
        _rotationX = Math.Clamp(MathF.Asin(Math.Clamp(-normalizedDirection.Y, -1f, 1f)), -orbitPitchLimit, orbitPitchLimit);

        float cosX = MathF.Cos(_rotationX);
        if (MathF.Abs(cosX) > 1e-6f)
        {
            _rotationY = MathF.Atan2(normalizedDirection.X, normalizedDirection.Z);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_roiInteractionMode != RoiInteractionMode.None)
        {
            // ROI 编辑完成后统一提交结果，再释放鼠标捕获。
            CompleteRoiInteraction(raiseEditedEvent: true);
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        CompletePendingLeftGestureSelection();

        if (_isRotating)
        {
            _isRotating = false;
        }

        ClearPendingLeftGestureState();
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 右键用于视野平移，因此按下即进入 pan 模式并捕获鼠标。
        _isPanning = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 滚轮只改变相机到目标点的距离，不改变观察方向。
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

        // 滚轮缩放只改变相机到目标点的距离，不改变当前朝向。
        // 同步旋转基准，保证随后左键 Orbit 时不会因为沿用旧欧拉角而突然跳变或看起来“放大/缩小”。
        SyncRotationFromCameraOffset();

        _roiNeedsRebuild = true;
        _needsRender = true;
    }

    private static Point WorldToScreen(Vector3 worldPos, Matrix4x4 mvp, int width, int height)
    {
        // 世界坐标 → 裁剪空间 → NDC → 屏幕像素，是 ROI 命中测试和标签摆放的基础。
        var clipPos = Vector4.Transform(new Vector4(worldPos, 1), mvp);
        if (clipPos.W == 0) return new Point(-1, -1);

        var ndcPos = new Vector3(clipPos.X / clipPos.W, clipPos.Y / clipPos.W, clipPos.Z / clipPos.W);

        double screenX = (ndcPos.X + 1) * 0.5 * width;
        double screenY = (1 - ndcPos.Y) * 0.5 * height;

        return new Point(screenX, screenY);
    }

    private Vector3 ScreenToWorld(Point screenPos, int width, int height)
    {
        // 反过来把屏幕像素映射到世界空间，用于鼠标世界坐标提示。
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
