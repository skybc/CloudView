using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

/// <summary>
/// 使用 Silk.NET.OpenGL 渲染三维点云的 WPF 控件
/// </summary>
public class PointCloudViewer : UserControl
{
    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int ChoosePixelFormat(IntPtr hDC, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetPixelFormat(IntPtr hDC, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SwapBuffers(IntPtr hDC);

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern IntPtr wglCreateContext(IntPtr hDC);

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern bool wglMakeCurrent(IntPtr hDC, IntPtr hGLRC);

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern bool wglDeleteContext(IntPtr hGLRC);

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern IntPtr wglGetProcAddress(string lpszProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpLibFileName);

    [StructLayout(LayoutKind.Sequential)]
    private struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits;
        public byte cRedShift;
        public byte cGreenBits;
        public byte cGreenShift;
        public byte cBlueBits;
        public byte cBlueShift;
        public byte cAlphaBits;
        public byte cAlphaShift;
        public byte cAccumBits;
        public byte cAccumRedBits;
        public byte cAccumGreenBits;
        public byte cAccumBlueBits;
        public byte cAccumAlphaBits;
        public byte cDepthBits;
        public byte cStencilBits;
        public byte cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask;
        public uint dwVisibleMask;
        public uint dwDamageMask;
    }

    private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
    private const uint PFD_SUPPORT_OPENGL = 0x00000020;
    private const uint PFD_DOUBLEBUFFER = 0x00000001;
    private const byte PFD_TYPE_RGBA = 0;
    private const byte PFD_MAIN_PLANE = 0;

    #endregion

    #region 依赖属性

    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(
            nameof(Points),
            typeof(IList<PointCloudPoint>),
            typeof(PointCloudViewer),
            new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty PointSizeProperty =
        DependencyProperty.Register(
            nameof(PointSize),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(3.0f, OnRenderPropertyChanged));

    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(
            nameof(BackgroundColor),
            typeof(Color),
            typeof(PointCloudViewer),
            new PropertyMetadata(Colors.Black, OnRenderPropertyChanged));

    public static readonly DependencyProperty RoiColorProperty =
        DependencyProperty.Register(
            nameof(RoiColor),
            typeof(Color),
            typeof(PointCloudViewer),
            new PropertyMetadata(Color.FromArgb(80, 0, 120, 215), OnRenderPropertyChanged));

    public static readonly DependencyProperty RoiBorderColorProperty =
        DependencyProperty.Register(
            nameof(RoiBorderColor),
            typeof(Color),
            typeof(PointCloudViewer),
            new PropertyMetadata(Color.FromArgb(255, 0, 120, 215), OnRenderPropertyChanged));

    public static readonly DependencyProperty SelectedPointsProperty =
        DependencyProperty.Register(
            nameof(SelectedPoints),
            typeof(IReadOnlyList<PointCloudPoint>),
            typeof(PointCloudViewer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedIndicesProperty =
        DependencyProperty.Register(
            nameof(SelectedIndices),
            typeof(IReadOnlyList<int>),
            typeof(PointCloudViewer),
            new PropertyMetadata(null));

    /// <summary>
    /// 点云数据
    /// </summary>
    public IList<PointCloudPoint>? Points
    {
        get => (IList<PointCloudPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>
    /// 点的大小
    /// </summary>
    public float PointSize
    {
        get => (float)GetValue(PointSizeProperty);
        set => SetValue(PointSizeProperty, value);
    }

    /// <summary>
    /// 背景颜色
    /// </summary>
    public Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// ROI 区域填充颜色
    /// </summary>
    public Color RoiColor
    {
        get => (Color)GetValue(RoiColorProperty);
        set => SetValue(RoiColorProperty, value);
    }

    /// <summary>
    /// ROI 区域边框颜色
    /// </summary>
    public Color RoiBorderColor
    {
        get => (Color)GetValue(RoiBorderColorProperty);
        set => SetValue(RoiBorderColorProperty, value);
    }

    /// <summary>
    /// 当前选中的点集合
    /// </summary>
    public IReadOnlyList<PointCloudPoint>? SelectedPoints
    {
        get => (IReadOnlyList<PointCloudPoint>?)GetValue(SelectedPointsProperty);
        set => SetValue(SelectedPointsProperty, value);
    }

    /// <summary>
    /// 当前选中的点索引列表
    /// </summary>
    public IReadOnlyList<int>? SelectedIndices
    {
        get => (IReadOnlyList<int>?)GetValue(SelectedIndicesProperty);
        set => SetValue(SelectedIndicesProperty, value);
    }

    #endregion

    #region 事件

    public static readonly RoutedEvent RoiSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(RoiSelected),
            RoutingStrategy.Bubble,
            typeof(EventHandler<RoiSelectionEventArgs>),
            typeof(PointCloudViewer));

    /// <summary>
    /// ROI 选择完成事件
    /// </summary>
    public event EventHandler<RoiSelectionEventArgs>? RoiSelected
    {
        add => AddHandler(RoiSelectedEvent, value);
        remove => RemoveHandler(RoiSelectedEvent, value);
    }

    #endregion

    #region 私有字段

    private IntPtr _hDC;
    private IntPtr _hGLRC;
    private GL? _gl;
    private bool _isInitialized;

    // 着色器程序
    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private int _vertexCount;

    // 相机参数
    private Vector3 _cameraPosition = new(0, 0, 5);
    private Vector3 _cameraTarget = Vector3.Zero;
    private Vector3 _cameraUp = Vector3.UnitY;
    private float _fov = 45.0f;
    private float _rotationX;
    private float _rotationY;
    private float _zoom = 5.0f;

    // 鼠标交互
    private bool _isRotating;
    private bool _isDrawingRoi;
    private Point _lastMousePosition;
    private Point _roiStart;
    private Point _roiEnd;

    // UI 元素
    private readonly Grid _mainGrid;
    private readonly HwndHost _glHost;
    private readonly Canvas _overlayCanvas;
    private readonly Rectangle _roiRectangle;

    // 渲染定时器
    private DispatcherTimer? _renderTimer;

    #endregion

    #region 着色器代码

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

    #endregion

    public PointCloudViewer()
    {
        // 创建主布局
        _mainGrid = new Grid();

        // 创建 OpenGL 宿主
        _glHost = new OpenGLHost(this);
        _mainGrid.Children.Add(_glHost);

        // 创建覆盖层 Canvas 用于绘制 ROI
        _overlayCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = true
        };
        _mainGrid.Children.Add(_overlayCanvas);

        // 创建 ROI 矩形
        _roiRectangle = new Rectangle
        {
            Visibility = Visibility.Collapsed,
            StrokeThickness = 1
        };
        _overlayCanvas.Children.Add(_roiRectangle);
        UpdateRoiRectangleStyle();

        Content = _mainGrid;

        // 设置事件处理
        _overlayCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
        _overlayCanvas.MouseMove += OnMouseMove;
        _overlayCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
        _overlayCanvas.MouseRightButtonDown += OnMouseRightButtonDown;
        _overlayCanvas.MouseRightButtonUp += OnMouseRightButtonUp;
        _overlayCanvas.MouseWheel += OnMouseWheel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void UpdateRoiRectangleStyle()
    {
        _roiRectangle.Fill = new SolidColorBrush(RoiColor);
        _roiRectangle.Stroke = new SolidColorBrush(RoiBorderColor);
    }

    #region OpenGL 宿主

    private class OpenGLHost : HwndHost
    {
        private readonly PointCloudViewer _parent;
        private IntPtr _hwnd;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_CLIPCHILDREN = 0x02000000;

        public OpenGLHost(PointCloudViewer parent)
        {
            _parent = parent;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hwnd = CreateWindowEx(
                0,
                "static",
                "",
                WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
                0, 0,
                (int)_parent.ActualWidth,
                (int)_parent.ActualHeight,
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            _parent.InitializeOpenGL(_hwnd);
            return new HandleRef(this, _hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            _parent.CleanupOpenGL();
            DestroyWindow(hwnd.Handle);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _parent.OnResize((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
        }
    }

    #endregion

    #region OpenGL 初始化和清理

    private void InitializeOpenGL(IntPtr hwnd)
    {
        _hDC = GetDC(hwnd);
        if (_hDC == IntPtr.Zero)
            throw new Exception("Failed to get device context");

        var pfd = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
            iPixelType = PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = PFD_MAIN_PLANE
        };

        int pixelFormat = ChoosePixelFormat(_hDC, ref pfd);
        if (pixelFormat == 0)
            throw new Exception("Failed to choose pixel format");

        if (!SetPixelFormat(_hDC, pixelFormat, ref pfd))
            throw new Exception("Failed to set pixel format");

        _hGLRC = wglCreateContext(_hDC);
        if (_hGLRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL context");

        if (!wglMakeCurrent(_hDC, _hGLRC))
            throw new Exception("Failed to make OpenGL context current");

        // 使用 Silk.NET 加载 OpenGL
        _gl = GL.GetApi(GetProcAddressFunc);

        InitializeShaders();
        InitializeBuffers();

        _isInitialized = true;
    }

    private nint GetProcAddressFunc(string name)
    {
        var addr = wglGetProcAddress(name);
        if (addr != IntPtr.Zero)
            return addr;

        var opengl32 = LoadLibrary("opengl32.dll");
        return GetProcAddress(opengl32, name);
    }

    private void CleanupOpenGL()
    {
        if (_gl != null && _isInitialized)
        {
            wglMakeCurrent(_hDC, _hGLRC);

            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteProgram(_shaderProgram);
        }

        if (_hGLRC != IntPtr.Zero)
        {
            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            wglDeleteContext(_hGLRC);
            _hGLRC = IntPtr.Zero;
        }

        _isInitialized = false;
    }

    private unsafe void InitializeShaders()
    {
        if (_gl == null) return;

        // 编译顶点着色器
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, VertexShaderSource);
        _gl.CompileShader(vertexShader);

        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(vertexShader);
            throw new Exception($"Vertex shader compilation failed: {infoLog}");
        }

        // 编译片段着色器
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, FragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(fragmentShader);
            throw new Exception($"Fragment shader compilation failed: {infoLog}");
        }

        // 链接着色器程序
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_shaderProgram);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    private unsafe void InitializeBuffers()
    {
        if (_gl == null) return;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // 位置属性 (location = 0)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // 颜色属性 (location = 1)
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void UpdatePointCloudBuffer()
    {
        if (_gl == null || !_isInitialized || Points == null) return;

        wglMakeCurrent(_hDC, _hGLRC);

        var points = Points;
        _vertexCount = points.Count;

        if (_vertexCount == 0) return;

        // 准备顶点数据: x, y, z, r, g, b, a
        var vertices = new float[_vertexCount * 7];
        for (int i = 0; i < _vertexCount; i++)
        {
            var point = points[i];
            int offset = i * 7;
            vertices[offset] = point.Position.X;
            vertices[offset + 1] = point.Position.Y;
            vertices[offset + 2] = point.Position.Z;
            vertices[offset + 3] = point.Color.X;
            vertices[offset + 4] = point.Color.Y;
            vertices[offset + 5] = point.Color.Z;
            vertices[offset + 6] = point.Color.W;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        Render();
    }

    #endregion

    #region 渲染

    private unsafe void Render()
    {
        if (_gl == null || !_isInitialized) return;

        wglMakeCurrent(_hDC, _hGLRC);

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

        if (_vertexCount > 0)
        {
            _gl.UseProgram(_shaderProgram);

            // 设置矩阵
            var model = Matrix4x4.Identity;
            var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
            var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);

            // 应用旋转
            model = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);

            SetUniformMatrix4(_gl, _shaderProgram, "uModel", model);
            SetUniformMatrix4(_gl, _shaderProgram, "uView", view);
            SetUniformMatrix4(_gl, _shaderProgram, "uProjection", projection);

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_vertexCount);
            _gl.BindVertexArray(0);
        }

        SwapBuffers(_hDC);
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
        float tanHalfFov = MathF.Tan(fov / 2f);
        return new Matrix4x4(
            1f / (aspect * tanHalfFov), 0, 0, 0,
            0, 1f / tanHalfFov, 0, 0,
            0, 0, -(far + near) / (far - near), -1,
            0, 0, -(2f * far * near) / (far - near), 0
        );
    }

    private void OnResize(int width, int height)
    {
        if (_gl != null && _isInitialized && width > 0 && height > 0)
        {
            wglMakeCurrent(_hDC, _hGLRC);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            Render();
        }
    }

    #endregion

    #region 鼠标事件处理

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDrawingRoi = true;
        _roiStart = e.GetPosition(_overlayCanvas);
        _roiEnd = _roiStart;

        _roiRectangle.Visibility = Visibility.Visible;
        UpdateRoiRectangle();

        _overlayCanvas.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(_overlayCanvas);

        if (_isDrawingRoi)
        {
            _roiEnd = currentPos;
            UpdateRoiRectangle();
        }
        else if (_isRotating)
        {
            var delta = currentPos - _lastMousePosition;
            _rotationY += (float)delta.X * 0.01f;
            _rotationX += (float)delta.Y * 0.01f;
            _lastMousePosition = currentPos;
            Render();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingRoi)
        {
            _isDrawingRoi = false;
            _overlayCanvas.ReleaseMouseCapture();

            _roiEnd = e.GetPosition(_overlayCanvas);
            UpdateRoiRectangle();

            // 计算选中的点
            var selectedIndices = new List<int>();
            var selectedPoints = new List<PointCloudPoint>();
            var screenRect = GetRoiRect();

            if (Points != null && screenRect.Width > 1 && screenRect.Height > 1)
            {
                int width = (int)ActualWidth;
                int height = (int)ActualHeight;

                var model = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);
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

            // 更新依赖属性
            SelectedIndices = selectedIndices;
            SelectedPoints = selectedPoints;

            // 触发事件
            var args = new RoiSelectionEventArgs(selectedIndices, selectedPoints, screenRect);
            args.RoutedEvent = RoiSelectedEvent;
            args.Source = this;
            RaiseEvent(args);

            // 隐藏 ROI 矩形
            _roiRectangle.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRotating = true;
        _lastMousePosition = e.GetPosition(_overlayCanvas);
        _overlayCanvas.CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        _overlayCanvas.ReleaseMouseCapture();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom -= e.Delta * 0.005f;
        _zoom = Math.Clamp(_zoom, 1f, 50f);
        _cameraPosition = new Vector3(0, 0, _zoom);
        Render();
    }

    private void UpdateRoiRectangle()
    {
        var rect = GetRoiRect();
        Canvas.SetLeft(_roiRectangle, rect.Left);
        Canvas.SetTop(_roiRectangle, rect.Top);
        _roiRectangle.Width = rect.Width;
        _roiRectangle.Height = rect.Height;
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

        // 转换到屏幕坐标 (注意 Y 轴翻转)
        double screenX = (ndcPos.X + 1) * 0.5 * width;
        double screenY = (1 - ndcPos.Y) * 0.5 * height;

        return new Point(screenX, screenY);
    }

    #endregion

    #region 事件处理

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 启动渲染定时器
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _renderTimer.Tick += (s, args) => Render();
        _renderTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            viewer.UpdatePointCloudBuffer();
        }
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            if (e.Property == RoiColorProperty || e.Property == RoiBorderColorProperty)
            {
                viewer.UpdateRoiRectangleStyle();
            }
            viewer.Render();
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 从 float 数组加载点云数据
    /// </summary>
    /// <param name="positions">点的位置数组 (x, y, z, x, y, z, ...)</param>
    /// <param name="defaultColor">默认颜色</param>
    public void LoadFromFloatArray(float[] positions, Vector4? defaultColor = null)
    {
        var color = defaultColor ?? new Vector4(1, 1, 1, 1);
        var points = new List<PointCloudPoint>();

        for (int i = 0; i < positions.Length - 2; i += 3)
        {
            points.Add(new PointCloudPoint(
                new Vector3(positions[i], positions[i + 1], positions[i + 2]),
                color));
        }

        Points = points;
    }

    /// <summary>
    /// 从 Vector3 列表加载点云数据
    /// </summary>
    /// <param name="positions">点的位置列表</param>
    /// <param name="defaultColor">默认颜色</param>
    public void LoadFromVector3List(List<Vector3> positions, Vector4? defaultColor = null)
    {
        var color = defaultColor ?? new Vector4(1, 1, 1, 1);
        var points = positions.Select(p => new PointCloudPoint(p, color)).ToList();
        Points = points;
    }

    /// <summary>
    /// 重置相机视图
    /// </summary>
    public void ResetView()
    {
        _rotationX = 0;
        _rotationY = 0;
        _zoom = 5;
        _cameraPosition = new Vector3(0, 0, _zoom);
        Render();
    }

    /// <summary>
    /// 自动调整视图以适应所有点
    /// </summary>
    public void FitToView()
    {
        if (Points == null || Points.Count == 0) return;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var point in Points)
        {
            min = Vector3.Min(min, point.Position);
            max = Vector3.Max(max, point.Position);
        }

        var center = (min + max) * 0.5f;
        var size = (max - min).Length();

        _cameraTarget = center;
        _zoom = size * 2;
        _cameraPosition = new Vector3(center.X, center.Y, center.Z + _zoom);
        _rotationX = 0;
        _rotationY = 0;

        Render();
    }

    #endregion
}
