using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Silk.NET.OpenGL;
using PixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    #region 着色器代码

    // 主场景着色器：负责把点云、坐标轴、网格、ROI 以及通用几何对象统一变换到裁剪空间。
    private const string VertexShaderSource = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aColor;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec4 vColor;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
    vColor = aColor;
}
";

    private const string FragmentShaderSource = @"
#version 330 core
in vec4 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vColor;
}
";

    private const string OverlayVertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec4 aColor;

out vec4 vColor;

void main()
{
    gl_Position = vec4(aPosition.xy, 0.0, 1.0);
    vColor = aColor;
}
";

    private const string OverlayFragmentShaderSource = @"
#version 330 core
in vec4 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vColor;
}
";

    private const string TextVertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

uniform mat4 uProjection;

out vec2 vTexCoord;

void main()
{
    gl_Position = uProjection * vec4(aPosition.xy, 0.0, 1.0);
    vTexCoord = aTexCoord;
}
";

    private const string TextFragmentShaderSource = @"
#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec4 uColor;

void main()
{
    vec4 texColor = texture(uTexture, vec2(vTexCoord.x, 1.0 - vTexCoord.y));
    FragColor = vec4(uColor.rgb, texColor.a * uColor.a);
}
";

    #endregion

    #region 渲染

    private unsafe void Render()
    {
        if (_gl == null || !_isInitialized) return;

        // 任何一帧渲染前，先把 WGL 上下文切成当前控制的上下文。
        Win32Interop.wglMakeCurrent(_hDC, _hGLRC);

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0 || height <= 0) return;

        _gl.Viewport(0, 0, (uint)width, (uint)height);

        var bgColor = BackgroundColor;
        _gl.ClearColor(bgColor.R / 255f, bgColor.G / 255f, bgColor.B / 255f, bgColor.A / 255f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.PointSize(PointSize);

        var model = Matrix4x4.Identity;
        // 视图矩阵由相机位置、目标点和上方向决定；投影矩阵则决定透视和视锥范围。
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);

        _gl.UseProgram(_shaderProgram);
        SetUniformMatrix4(_gl, _shaderProgram, "uModel", model);
        SetUniformMatrix4(_gl, _shaderProgram, "uView", view);
        SetUniformMatrix4(_gl, _shaderProgram, "uProjection", projection);

        // 绘制坐标系轴线
        if (ShowCoordinateAxis && _axisVertexCount > 0)
        {
            // 轴线使用更粗的线宽，只是为了提升方向感，不参与深度层级竞争。
            _gl.LineWidth(2.0f);
            _gl.BindVertexArray(_axisVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_axisVertexCount);
            _gl.BindVertexArray(0);
            _gl.LineWidth(1.0f);
        }

        // 绘制 XY 平面网格
        if (ShowGrid)
        {
            // 网格范围随着缩放变化：看得远时网格铺得更大，看得近时网格更密。
            float gridRange = _zoom * 0.5f;

            if (_gridNeedsUpdate || MathF.Abs(_lastGridZoom - _zoom) > 0.01f)
            {
                UpdateXYGrid(gridRange);
                _lastGridZoom = _zoom;
                _gridNeedsUpdate = false;
            }

            if (_gridVertexCount > 0)
            {
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _gl.LineWidth(1.0f);
                _gl.BindVertexArray(_gridVao);
                _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_gridVertexCount);
                _gl.BindVertexArray(0);
                _gl.Disable(EnableCap.Blend);
            }
        }

        // 绘制几何对象
        RenderShapes();

        // 绘制点云
        if (_vertexCount > 0)
        {
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_vertexCount);
            _gl.BindVertexArray(0);
        }

        // 在点云之后绘制 ROI，保证线框控制点清晰可见且不遮挡数据主体。
        RenderRois();

        // 绘制右上角鼠标位置信息覆盖层
        DrawOverlay(width, height);

        // 绘制右下角坐标轴指示器
        DrawOrientationGizmo(width, height);

        // 最后交换前后缓冲，把本帧内容一次性呈现到屏幕。
        Win32Interop.SwapBuffers(_hDC);
    }

    private unsafe void DrawOverlay(int width, int height)
    {
        if (_gl == null || _overlayShaderProgram == 0)
            return;

        const int overlayWidthPx = 140;
        const int overlayHeightPx = 100;

        float overlayWidthNdc = (2.0f * overlayWidthPx) / width;
        float overlayHeightNdc = (2.0f * overlayHeightPx) / height;

        float right = 1.0f;
        float top = 1.0f;
        float left = right - overlayWidthNdc;
        float bottom = top - overlayHeightNdc;

        int rowCount = 4;
        float rowHeight = overlayHeightNdc / rowCount;

        float bgR = 0.0f, bgG = 0.0f, bgB = 0.0f, bgA = 0.65f;

        // 覆盖层先做一个半透明背景，再用单独的文本纹理绘制鼠标世界坐标。
        var verticesList = new List<float>();

        void AddQuad(float lx, float rx, float by, float ty, float r, float g, float b, float a)
        {
            verticesList.Add(lx); verticesList.Add(ty); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(rx); verticesList.Add(ty); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(lx); verticesList.Add(by); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(rx); verticesList.Add(by); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
        }

        AddQuad(left, right, bottom, top, bgR, bgG, bgB, bgA);

        float labelWidth = overlayWidthNdc * 0.35f;
        float valueWidth = overlayWidthNdc * 0.6f;

        float xMm = _currentMouseWorldPosition.X;
        float yMm = _currentMouseWorldPosition.Y;
        float zMm = _currentMouseWorldPosition.Z;

        float rangeMm = 1000f;
        float Normalize(float v) => Math.Clamp((v + rangeMm) / (2 * rangeMm), 0f, 1f);

        float nx = Normalize(xMm);
        float ny = Normalize(yMm);
        float nz = Normalize(zMm);

        float titleTop = top;
        float xRowTop = top - rowHeight;
        float yRowTop = top - 2 * rowHeight;
        float zRowTop = top - 3 * rowHeight;

        var vertices = verticesList.ToArray();

        if (_overlayVao == 0)
        {
            _overlayVao = _gl.GenVertexArray();
            _overlayVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_overlayVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _overlayVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        uint stride = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _gl.UseProgram(_overlayShaderProgram);

        // 这里只负责把半透明底板送进 GPU；真正的数字文本由 DrawCachedText 负责。
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        const float fontSize = 16;
        const float marginRight = 10;
        const float marginTop = 10;

        float startX = width - overlayWidthPx + marginRight;
        float startY = marginTop;

        string combinedText = $"X: {xMm:F3} mm\nY: {yMm:F3} mm\nZ: {zMm:F3} mm";
        DrawCachedText(combinedText, startX, startY, fontSize, width, height);
    }

    private unsafe void DrawCachedText(string text, float pixelX, float pixelY, float fontSize, int windowWidth, int windowHeight)
    {
        if (_gl == null || _textShaderProgram == 0)
            return;

        // 文本内容发生变化时才重建纹理，避免每帧重复生成位图和纹理对象。
        if (_cachedTextTexture == 0 || _cachedTextContent != text)
        {
            if (_cachedTextTexture != 0)
            {
                _gl.DeleteTexture(_cachedTextTexture);
            }

            _cachedTextTexture = CreateMultiLineTextTexture(text, (int)fontSize, out _cachedTextWidth, out _cachedTextHeight);
            _cachedTextContent = text;
        }

        float left = pixelX;
        float top = pixelY;
        float right = pixelX + _cachedTextWidth;
        float bottom = pixelY + _cachedTextHeight;

        var vertices = new float[]
        {
            left, top, 0, 1,
            right, top, 1, 1,
            left, bottom, 0, 0,
            right, bottom, 1, 0
        };

        if (_textVao == 0)
        {
            _textVao = _gl.GenVertexArray();
            _textVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.UseProgram(_textShaderProgram);

        // 文本使用像素坐标的正交投影，这样 UI 尺寸和字体大小不会随着 3D 相机而变化。
        var orthoProjection = Matrix4x4.CreateOrthographicOffCenter(0, windowWidth, windowHeight, 0, -1, 1);
        SetUniformMatrix4(_gl, _textShaderProgram, "uProjection", orthoProjection);

        int colorLoc = _gl.GetUniformLocation(_textShaderProgram, "uColor");
        _gl.Uniform4(colorLoc, 0.0f, 1.0f, 0.0f, 1.0f);

        int texLoc = _gl.GetUniformLocation(_textShaderProgram, "uTexture");
        _gl.Uniform1(texLoc, 0);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _cachedTextTexture);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void DrawOrientationGizmo(int width, int height)
    {
        if (_gl == null || _overlayShaderProgram == 0) return;

        const int gizmoSize = 80;
        const int margin = 20;

        float centerX = width - margin - gizmoSize / 2f;
        float centerY = height - margin - gizmoSize / 2f;
        float axisPixelLength = gizmoSize * 0.4f;

        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);

        var xProj = Vector3.TransformNormal(Vector3.UnitX, view);
        var yProj = Vector3.TransformNormal(Vector3.UnitY, view);
        var zProj = Vector3.TransformNormal(Vector3.UnitZ, view);

        // 根据相机朝向把世界三轴投影到屏幕上，再按深度排序，保证“远轴先画”。
        var axes = new (float px, float py, float pz, Vector4 color, string label)[]
        {
            (xProj.X, xProj.Y, xProj.Z, new Vector4(1, 0.2f, 0.2f, 1), "X"),
            (yProj.X, yProj.Y, yProj.Z, new Vector4(0.2f, 1, 0.2f, 1), "Y"),
            (zProj.X, zProj.Y, zProj.Z, new Vector4(0.2f, 0.2f, 1, 1), "Z"),
        };

        // Sort by depth: draw far axes first (painter's algorithm)
        Array.Sort(axes, (a, b) => a.pz.CompareTo(b.pz));

        // Axis lines only
        var verticesList = new List<float>();
        float ndcCenterX = (centerX / width) * 2 - 1;
        float ndcCenterY = 1 - (centerY / height) * 2;

        foreach (var axis in axes)
        {
            // 线段终点按投影方向偏移，形成右下角的小坐标轴指示器。
            float endPixelX = centerX + axis.px * axisPixelLength;
            float endPixelY = centerY - axis.py * axisPixelLength;

            float ndcEndX = (endPixelX / width) * 2 - 1;
            float ndcEndY = 1 - (endPixelY / height) * 2;

            float alpha = axis.pz < 0 ? 0.35f : 1.0f;
            var c = axis.color;

            verticesList.Add(ndcCenterX); verticesList.Add(ndcCenterY);
            verticesList.Add(c.X); verticesList.Add(c.Y); verticesList.Add(c.Z); verticesList.Add(alpha);

            verticesList.Add(ndcEndX); verticesList.Add(ndcEndY);
            verticesList.Add(c.X); verticesList.Add(c.Y); verticesList.Add(c.Z); verticesList.Add(alpha);
        }

        var vertices = verticesList.ToArray();

        if (_gizmoVao == 0)
        {
            _gizmoVao = _gl.GenVertexArray();
            _gizmoVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_gizmoVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gizmoVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        uint stride = 6 * sizeof(float);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _gl.UseProgram(_overlayShaderProgram);

        _gl.LineWidth(2.0f);
        _gl.DrawArrays(PrimitiveType.Lines, 0, 6);
        _gl.LineWidth(1.0f);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // Draw axis labels
        const float labelFontSize = 16;
        const float labelOffset = 12;

        foreach (var axis in axes)
        {
            // 标签位置沿着轴线外侧再偏移一小段，避免和线段本体重叠。
            float endPixelX = centerX + axis.px * axisPixelLength;
            float endPixelY = centerY - axis.py * axisPixelLength;

            float labelX = endPixelX + axis.px * labelOffset;
            float labelY = endPixelY - axis.py * labelOffset - 6;

            float alpha = axis.pz < 0 ? 0.35f : 1.0f;
            var labelColor = axis.color;
            labelColor.W = alpha;

            DrawTextWithColor(axis.label, labelX, labelY, labelFontSize, width, height, labelColor);
        }
    }

    private unsafe void DrawTextWithColor(string text, float pixelX, float pixelY, float fontSize, int windowWidth, int windowHeight, Vector4 color)
    {
        if (_gl == null || _textShaderProgram == 0) return;

        if (!_gizmoTextCache.TryGetValue(text, out var cache))
        {
            int texWidth, texHeight;
            uint texture = CreateMultiLineTextTexture(text, (int)fontSize, out texWidth, out texHeight);
            cache = (texture, texWidth, texHeight);
            _gizmoTextCache[text] = cache;
        }

        float left = pixelX;
        float top = pixelY;
        float right = pixelX + cache.width;
        float bottom = pixelY + cache.height;

        var vertices = new float[]
        {
            left, top, 0, 1,
            right, top, 1, 1,
            left, bottom, 0, 0,
            right, bottom, 1, 0
        };

        if (_textVao == 0)
        {
            _textVao = _gl.GenVertexArray();
            _textVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.UseProgram(_textShaderProgram);

        var orthoProjection = Matrix4x4.CreateOrthographicOffCenter(0, windowWidth, windowHeight, 0, -1, 1);
        SetUniformMatrix4(_gl, _textShaderProgram, "uProjection", orthoProjection);

        int colorLoc = _gl.GetUniformLocation(_textShaderProgram, "uColor");
        _gl.Uniform4(colorLoc, color.X, color.Y, color.Z, color.W);

        int texLoc = _gl.GetUniformLocation(_textShaderProgram, "uTexture");
        _gl.Uniform1(texLoc, 0);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, cache.texture);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe uint CreateMultiLineTextTexture(string text, int fontSize, out int texWidth, out int texHeight)
    {
        // 先用 WPF 的文本排版引擎生成文字位图，再上传到 OpenGL 作为纹理。
        var formattedText = new System.Windows.Media.FormattedText(
            text,
            System.Globalization.CultureInfo.GetCultureInfo("en-us"),
            System.Windows.FlowDirection.LeftToRight,
            new System.Windows.Media.Typeface("Arial"),
            fontSize,
            System.Windows.Media.Brushes.Lime,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        int width = (int)formattedText.Width + 4;
        int height = (int)formattedText.Height + 4;

        texWidth = 1 << (int)Math.Ceiling(Math.Log2(width));
        texHeight = 1 << (int)Math.Ceiling(Math.Log2(height));

        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawText(formattedText, new System.Windows.Point(2, 2));
        }

        var bitmap = new RenderTargetBitmap(texWidth, texHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);

        byte[] pixels = new byte[texWidth * texHeight * 4];
        bitmap.CopyPixels(pixels, texWidth * 4, 0);

        // WPF 位图是 BGRA，OpenGL 这里使用 RGBA，所以要交换 R/B 通道。
        for (int i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }

        uint texture = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);

        fixed (byte* pixelPtr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)texWidth, (uint)texHeight, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixelPtr);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return texture;
    }

    #endregion

    #region 矩阵工具

    private Matrix4x4 CreateOrthoMatrix(float left, float right, float bottom, float top, float near, float far)
    {
        // 手写正交矩阵，便于理解坐标范围如何映射到裁剪空间。
        float width = right - left;
        float height = top - bottom;
        float depth = far - near;

        return new Matrix4x4(
            2 / width, 0, 0, 0,
            0, 2 / height, 0, 0,
            0, 0, -2 / depth, 0,
            -(right + left) / width, -(top + bottom) / height, -(far + near) / depth, 1
        );
    }

    private static unsafe void SetUniformMatrix4(GL gl, uint program, string name, Matrix4x4 matrix)
    {
        int location = gl.GetUniformLocation(program, name);
        if (location >= 0)
        {
            float* matrixPtr = (float*)&matrix;
            gl.UniformMatrix4(location, 1, false, matrixPtr);
        }
    }

    private static Matrix4x4 CreateLookAtMatrix(Vector3 eye, Vector3 target, Vector3 up)
    {
        // 经典 LookAt：zAxis 指向“从目标看向相机”的方向，xAxis/yAxis 构成相机局部基。
        var zAxis = Vector3.Normalize(eye - target);
        var xAxis = Vector3.Normalize(Vector3.Cross(up, zAxis));
        var yAxis = Vector3.Cross(zAxis, xAxis);

        return new Matrix4x4(
            xAxis.X, yAxis.X, zAxis.X, 0,
            xAxis.Y, yAxis.Y, zAxis.Y, 0,
            xAxis.Z, yAxis.Z, zAxis.Z, 0,
            -Vector3.Dot(xAxis, eye), -Vector3.Dot(yAxis, eye), -Vector3.Dot(zAxis, eye), 1
        );
    }

    private static Matrix4x4 CreatePerspectiveMatrix(float fov, float aspect, float near, float far)
    {
        // 透视矩阵决定远处物体更小、近处更大的视觉效果。
        float tanHalfFov = MathF.Tan(fov / 2f);
        return new Matrix4x4(
            1f / (aspect * tanHalfFov), 0, 0, 0,
            0, 1f / tanHalfFov, 0, 0,
            0, 0, -(far + near) / (far - near), -1,
            0, 0, -(2f * far * near) / (far - near), 0
        );
    }

    internal void OnResize(int width, int height)
    {
        if (_gl != null && _isInitialized && width > 0 && height > 0)
        {
            // 宿主窗口尺寸变化后，直接更新视口并重绘，避免拉伸或黑边。
            Win32Interop.wglMakeCurrent(_hDC, _hGLRC);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            Render();
        }
    }

    #endregion
}
