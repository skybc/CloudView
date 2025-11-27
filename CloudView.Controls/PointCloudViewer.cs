using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Silk.NET.OpenGL;
using PixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace CloudView.Controls;

/// <summary>
/// 使用 Silk.NET.OpenGL 渲染三维点云的 WPF 控件
/// </summary>
public partial class PointCloudViewer : Control
{
    static PointCloudViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PointCloudViewer), 
            new FrameworkPropertyMetadata(typeof(PointCloudViewer)));
    }
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

    public static readonly DependencyProperty ShowCoordinateAxisProperty =
        DependencyProperty.Register(
            nameof(ShowCoordinateAxis),
            typeof(bool),
            typeof(PointCloudViewer),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(
            nameof(ShowGrid),
            typeof(bool),
            typeof(PointCloudViewer),
            new PropertyMetadata(true, OnRenderPropertyChanged));

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

    /// <summary>
    /// 是否显示坐标系轴线
    /// </summary>
    public bool ShowCoordinateAxis
    {
        get => (bool)GetValue(ShowCoordinateAxisProperty);
        set => SetValue(ShowCoordinateAxisProperty, value);
    }

    /// <summary>
    /// 是否显示 XY 平面网格
    /// </summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
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

    public static readonly RoutedEvent MousePositionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(MousePositionChanged),
            RoutingStrategy.Bubble,
            typeof(EventHandler<MousePositionEventArgs>),
            typeof(PointCloudViewer));

    /// <summary>
    /// 鼠标位置变化事件
    /// </summary>
    public event EventHandler<MousePositionEventArgs>? MousePositionChanged
    {
        add => AddHandler(MousePositionChangedEvent, value);
        remove => RemoveHandler(MousePositionChangedEvent, value);
    }

    #endregion

    #region 私有字段

    private IntPtr _hDC;
    private IntPtr _hGLRC;
    private GL? _gl;
    private bool _isInitialized;

    // 着色器程序
    private uint _shaderProgram;
    private uint _screenSpaceShaderProgram;
    private uint _overlayShaderProgram;
    private uint _overlayVao;
    private uint _overlayVbo;
    private uint _textShaderProgram;
    private uint _textVao;
    private uint _textVbo;
    private uint _textTexture;
    private int _textVertexCount;
    private uint _vao;
    private uint _vbo;
    private uint _roiVao;
    private uint _roiVbo;
    private int _vertexCount;

    // 坐标系轴线
    private uint _axisVao;
    private uint _axisVbo;
    private int _axisVertexCount;

    // XY平面网格
    private uint _gridVao;
    private uint _gridVbo;
    private int _gridVertexCount;

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

    // 模板中的 UI 元素
    private Grid? _glHostGrid;
    private HwndHost? _glHost;
    // 当前鼠标位置 (世界坐标)
    private Vector3 _currentMouseWorldPosition = Vector3.Zero;

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

    // 简单的屏幕空间矩形着色器（用于右上角信息背景）
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

    // 文本渲染着色器（使用正交投影，像素坐标）
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

    public PointCloudViewer()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 应用控件模板后初始化
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 获取模板中的 GLHost 元素
        _glHostGrid = (Grid)GetTemplateChild("PART_GLHost");

        if (_glHostGrid == null)
        {
            throw new InvalidOperationException("Required template element PART_GLHost not found.");
        }

        // 创建 OpenGL 宿主并添加到 Grid 中
        _glHost = new OpenGLHost(this);
        _glHostGrid.Children.Add(_glHost);

        // 注册鼠标事件
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseWheel += OnMouseWheel;
    }

    private void UpdateRoiRectangleStyle()
    {
        // ROI 矩形样式现在由 OpenGL 处理，无需在 WPF 中设置
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
        InitializeTextShaders();
        InitializeBuffers();
        CreateCoordinateAxisAndGrid();

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

            // 清理坐标系轴线的 VAO/VBO
            if (_axisVao != 0)
            {
                _gl.DeleteVertexArray(_axisVao);
                _axisVao = 0;
            }
            if (_axisVbo != 0)
            {
                _gl.DeleteBuffer(_axisVbo);
                _axisVbo = 0;
            }

            // 清理网格的 VAO/VBO
            if (_gridVao != 0)
            {
                _gl.DeleteVertexArray(_gridVao);
                _gridVao = 0;
            }
            if (_gridVbo != 0)
            {
                _gl.DeleteBuffer(_gridVbo);
                _gridVbo = 0;
            }

            // 清理 ROI 矩形的 VAO/VBO
            if (_roiVao != 0)
            {
                _gl.DeleteVertexArray(_roiVao);
                _roiVao = 0;
            }
            if (_roiVbo != 0)
            {
                _gl.DeleteBuffer(_roiVbo);
                _roiVbo = 0;
            }

            // 清理文本 VAO/VBO
            if (_textVao != 0)
            {
                _gl.DeleteVertexArray(_textVao);
                _textVao = 0;
            }
            if (_textVbo != 0)
            {
                _gl.DeleteBuffer(_textVbo);
                _textVbo = 0;
            }

            // 清理着色器程序
            if (_overlayShaderProgram != 0)
            {
                _gl.DeleteProgram(_overlayShaderProgram);
                _overlayShaderProgram = 0;
            }
            if (_textShaderProgram != 0)
            {
                _gl.DeleteProgram(_textShaderProgram);
                _textShaderProgram = 0;
            }
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

        // 初始化覆盖层着色器程序
        uint overlayVertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(overlayVertexShader, OverlayVertexShaderSource);
        _gl.CompileShader(overlayVertexShader);

        _gl.GetShader(overlayVertexShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(overlayVertexShader);
            throw new Exception($"Overlay vertex shader compilation failed: {infoLog}");
        }

        uint overlayFragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(overlayFragmentShader, OverlayFragmentShaderSource);
        _gl.CompileShader(overlayFragmentShader);

        _gl.GetShader(overlayFragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(overlayFragmentShader);
            throw new Exception($"Overlay fragment shader compilation failed: {infoLog}");
        }

        _overlayShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_overlayShaderProgram, overlayVertexShader);
        _gl.AttachShader(_overlayShaderProgram, overlayFragmentShader);
        _gl.LinkProgram(_overlayShaderProgram);

        _gl.GetProgram(_overlayShaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_overlayShaderProgram);
            throw new Exception($"Overlay shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(overlayVertexShader);
        _gl.DeleteShader(overlayFragmentShader);
    }

    private unsafe void InitializeTextShaders()
    {
        if (_gl == null) return;

        // 编译文本顶点着色器
        uint textVertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(textVertexShader, TextVertexShaderSource);
        _gl.CompileShader(textVertexShader);

        _gl.GetShader(textVertexShader, ShaderParameterName.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(textVertexShader);
            throw new Exception($"Text vertex shader compilation failed: {infoLog}");
        }

        // 编译文本片段着色器
        uint textFragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(textFragmentShader, TextFragmentShaderSource);
        _gl.CompileShader(textFragmentShader);

        _gl.GetShader(textFragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(textFragmentShader);
            throw new Exception($"Text fragment shader compilation failed: {infoLog}");
        }

        _textShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_textShaderProgram, textVertexShader);
        _gl.AttachShader(_textShaderProgram, textFragmentShader);
        _gl.LinkProgram(_textShaderProgram);

        _gl.GetProgram(_textShaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_textShaderProgram);
            throw new Exception($"Text shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(textVertexShader);
        _gl.DeleteShader(textFragmentShader);
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

    private unsafe void CreateCoordinateAxisAndGrid()
    {
        if (_gl == null) return;

        // 创建坐标系轴线
        CreateCoordinateAxis();

        // 网格在 Render 中动态生成
    }

    private unsafe void CreateCoordinateAxis()
    {
        if (_gl == null) return;

        // 坐标系轴线：X轴(红), Y轴(绿), Z轴(蓝)
        // 每条轴线长度为 1.0，从原点出发
        const float axisLength = 1.0f;
        var axisVertices = new float[]
        {
            // X 轴 (红色)
            0, 0, 0, 1, 0, 0, 1,  // 起点
            axisLength, 0, 0, 1, 0, 0, 1,  // 终点

            // Y 轴 (绿色)
            0, 0, 0, 0, 1, 0, 1,  // 起点
            0, axisLength, 0, 0, 1, 0, 1,  // 终点

            // Z 轴 (蓝色)
            0, 0, 0, 0, 0, 1, 1,  // 起点
            0, 0, axisLength, 0, 0, 1, 1,  // 终点
        };

        _axisVertexCount = 6;
        _axisVao = _gl.GenVertexArray();
        _axisVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_axisVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _axisVbo);

        fixed (float* data = axisVertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(axisVertices.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }

        // 位置属性
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // 颜色属性
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void UpdateXYGrid(float gridRange)
    {
        if (_gl == null) return;

        // 创建 XY 平面网格 (Z = 0)
        // 网格间距固定，范围根据摄像机缩放动态调整
        const float gridSpacing = 0.1f;
        const float gridAlpha = 0.3f;  // 浅色
        const float gridR = 0.7f;
        const float gridG = 0.7f;
        const float gridB = 0.7f;

        var gridVertices = new List<float>();

        // X 方向的线（平行于 X 轴）
        for (float y = -gridRange; y <= gridRange; y += gridSpacing)
        {
            // 起点
            gridVertices.Add(-gridRange);
            gridVertices.Add(y);
            gridVertices.Add(0);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            // 终点
            gridVertices.Add(gridRange);
            gridVertices.Add(y);
            gridVertices.Add(0);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);
        }

        // Y 方向的线（平行于 Y 轴）
        for (float x = -gridRange; x <= gridRange; x += gridSpacing)
        {
            // 起点
            gridVertices.Add(x);
            gridVertices.Add(-gridRange);
            gridVertices.Add(0);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            // 终点
            gridVertices.Add(x);
            gridVertices.Add(gridRange);
            gridVertices.Add(0);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);
        }

        _gridVertexCount = gridVertices.Count / 7;

        // 创建或重新绑定 VAO/VBO
        if (_gridVao == 0)
        {
            _gridVao = _gl.GenVertexArray();
            _gridVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);

        var gridArray = gridVertices.ToArray();
        fixed (float* data = gridArray)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gridArray.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        // 位置属性
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // 颜色属性
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

        // 设置矩阵（用于坐标系、网格和点云）
        var model = Matrix4x4.Identity;
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);

        // 应用旋转
        model = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);

        _gl.UseProgram(_shaderProgram);
        SetUniformMatrix4(_gl, _shaderProgram, "uModel", model);
        SetUniformMatrix4(_gl, _shaderProgram, "uView", view);
        SetUniformMatrix4(_gl, _shaderProgram, "uProjection", projection);

        // 绘制坐标系轴线
        if (ShowCoordinateAxis && _axisVertexCount > 0)
        {
            _gl.LineWidth(2.0f);
            _gl.BindVertexArray(_axisVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_axisVertexCount);
            _gl.BindVertexArray(0);
            _gl.LineWidth(1.0f);
        }

        // 绘制 XY 平面网格（动态调整大小以覆盖显示区域）
        if (ShowGrid)
        {
            // 根据摄像机缩放和视角角度计算网格范围
            float gridRange = _zoom * 0.5f;  // 网格范围随摄像机缩放调整
            
            // 更新网格
            UpdateXYGrid(gridRange);
            
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

        // 绘制点云
        if (_vertexCount > 0)
        {
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_vertexCount);
            _gl.BindVertexArray(0);
        }

        // 绘制 ROI 矩形
        if (_isDrawingRoi)
        {
            DrawRoiRectangle();
        }

        // 绘制右上角鼠标位置信息覆盖层
        DrawOverlay(width, height);

        SwapBuffers(_hDC);
    }

    private unsafe void DrawRoiRectangle()
    {
        if (_gl == null) return;

        var roiRect = GetRoiRect();
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        // 禁用深度测试以确保 ROI 矩形总是可见
        _gl.Disable(EnableCap.DepthTest);

        // 使用正交投影绘制屏幕空间的矩形
        // 参数顺序: left, right, bottom, top (OpenGL 标准: bottom < top)
        var orthoProjection = CreateOrthoMatrix(0, width, height, 0, -1, 1);

        // 绘制 ROI 矩形的四条边
        DrawRoiBorder(roiRect, orthoProjection);
        DrawRoiFill(roiRect, orthoProjection);

        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>
    /// 在右上角绘制一个简单的覆盖层，显示当前鼠标世界坐标（背景矩形 + 三行彩色条，单位 mm）。
    /// </summary>
    private unsafe void DrawOverlay(int width, int height)
    {
        if (_gl == null || _overlayShaderProgram == 0)
            return;

        // 在屏幕空间中定义右上角的一个固定大小区域（单位：像素）
        const int overlayWidthPx = 140;  // 固定宽度 300 像素
        const int overlayHeightPx = 100; // 固定高度 100 像素

        // 转换为 NDC 坐标
        float overlayWidthNdc = (2.0f * overlayWidthPx) / width;
        float overlayHeightNdc = (2.0f * overlayHeightPx) / height;

        float right = 1.0f;
        float top = 1.0f;
        float left = right - overlayWidthNdc;
        float bottom = top - overlayHeightNdc;

        // 行布局（3 行：X、Y、Z），上方再留一行标题“Mouse(mm)”
        int rowCount = 4;
        float rowHeight = overlayHeightNdc / rowCount;

        // 背景颜色: 半透明深灰
        float bgR = 0.0f, bgG = 0.0f, bgB = 0.0f, bgA = 0.65f;

        var verticesList = new List<float>();

        // 整体背景矩形
        void AddQuad(float lx, float rx, float by, float ty, float r, float g, float b, float a)
        {
            // triangle strip: (lx,ty) (rx,ty) (lx,by) (rx,by)
            verticesList.Add(lx); verticesList.Add(ty); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(rx); verticesList.Add(ty); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(lx); verticesList.Add(by); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
            verticesList.Add(rx); verticesList.Add(by); verticesList.Add(r); verticesList.Add(g); verticesList.Add(b); verticesList.Add(a);
        }

        AddQuad(left, right, bottom, top, bgR, bgG, bgB, bgA);

        // 行内左右布局: 左侧标签区域、右侧值区域
        float labelWidth = overlayWidthNdc * 0.35f;
        float valueWidth = overlayWidthNdc * 0.6f;

        // 预计算数值（毫米）
        float xMm = _currentMouseWorldPosition.X * 1000f;
        float yMm = _currentMouseWorldPosition.Y * 1000f;
        float zMm = _currentMouseWorldPosition.Z * 1000f;

        // 预估一个范围，用于条形长度归一化（比如 ±1000mm）
        float rangeMm = 1000f;
        float Normalize(float v) => Math.Clamp((v + rangeMm) / (2 * rangeMm), 0f, 1f);

        float nx = Normalize(xMm);
        float ny = Normalize(yMm);
        float nz = Normalize(zMm);

        // 各行的顶部 Y
        float titleTop = top;
        float xRowTop = top - rowHeight;
        float yRowTop = top - 2 * rowHeight;
        float zRowTop = top - 3 * rowHeight;
         
        var vertices = verticesList.ToArray();

        // 创建或更新 VAO/VBO
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

        // 设置顶点属性: location 0 -> vec2 position, location 1 -> vec4 color
        uint stride = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        // 启用混合以支持透明
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        _gl.UseProgram(_overlayShaderProgram);
        //// 绘制所有三角形条带（每个 Quad 是 4 个顶点）
        //uint vertexCount = (uint)vertices.Length / 6; // 6 个浮点数每个顶点 (2 pos + 4 color)
        //_gl.DrawArrays(PrimitiveType.TriangleStrip, 0, vertexCount);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // 绘制文本标签和数值（使用像素坐标，固定字体大小）
        const float fontSize = 16;        // 固定字体大小（像素）
        const float lineSpacing = 22;     // 行间距（像素）
        const float marginRight = 10;     // 右边距（像素）
        const float marginTop = 10;       // 上边距（像素）

        // 计算起始位置 - 右上角
        float startX = width - overlayWidthPx + marginRight;
        float startY = marginTop;

        DrawText($"X: {xMm:F1} mm", startX, startY, fontSize, width, height);
        DrawText($"Y: {yMm:F1} mm", startX, startY + lineSpacing, fontSize, width, height);
        DrawText($"Z: {zMm:F1} mm", startX, startY + lineSpacing * 2, fontSize, width, height);
    }

    /// <summary>
    /// 使用 FormattedText 生成文本的位图纹理
    /// </summary>
    private unsafe uint CreateTextTexture(string text, int fontSize = 14)
    {
        // 使用 WPF 的 FormattedText 生成文本位图 - 文本颜色为绿色
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

        // 确保宽高为2的幂
        width = 1 << (int)Math.Ceiling(Math.Log2(width));
        height = 1 << (int)Math.Ceiling(Math.Log2(height));

        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawText(formattedText, new System.Windows.Point(2, 2));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);

        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);

        // 转换为 RGBA 格式（从 PBGRA）
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // 交换 R 和 B (PBGRA -> RGBA)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }

        uint texture = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);

        fixed (byte* pixelPtr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixelPtr);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return texture;
    }

    /// <summary>
    /// 绘制文本到指定位置（像素坐标系）
    /// </summary>
    /// <param name="text">要绘制的文本</param>
    /// <param name="pixelX">屏幕 X 坐标（像素）</param>
    /// <param name="pixelY">屏幕 Y 坐标（像素）</param>
    /// <param name="fontSize">字体大小（像素）</param>
    /// <param name="windowWidth">窗口宽度</param>
    /// <param name="windowHeight">窗口高度</param>
    private unsafe void DrawText(string text, float pixelX, float pixelY, float fontSize, int windowWidth, int windowHeight)
    {
        if (_gl == null || _textShaderProgram == 0)
            return;

        // 生成文本纹理
        uint textTexture = CreateTextTexture(text, (int)fontSize);

        // 获取纹理实际尺寸（2的幂对齐后）
        // 使用 FormattedText 计算实际文本尺寸
        var formattedText = new System.Windows.Media.FormattedText(
            text,
            System.Globalization.CultureInfo.GetCultureInfo("en-us"),
            System.Windows.FlowDirection.LeftToRight,
            new System.Windows.Media.Typeface("Arial"),
            fontSize,
            System.Windows.Media.Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // 计算纹理尺寸（2的幂对齐）
        int texWidth = 1 << (int)Math.Ceiling(Math.Log2((int)formattedText.Width + 4));
        int texHeight = 1 << (int)Math.Ceiling(Math.Log2((int)formattedText.Height + 4));

        // 使用像素坐标构建矩形顶点
        // 左上角为 (pixelX, pixelY)，向右下延伸
        float left = pixelX;
        float top = pixelY;
        float right = pixelX + texWidth;
        float bottom = pixelY + texHeight;

        // 四个顶点: 左上、右上、左下、右下
        // 纹理坐标：OpenGL 纹理原点在左下角，WPF 位图原点在左上角
        var vertices = new float[]
        {
            left, top, 0, 1,           // 左上 (纹理坐标 0,1)
            right, top, 1, 1,          // 右上 (纹理坐标 1,1)
            left, bottom, 0, 0,        // 左下 (纹理坐标 0,0)
            right, bottom, 1, 0        // 右下 (纹理坐标 1,0)
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

        // 位置属性 (location 0): 2 floats
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // 纹理坐标属性 (location 1): 2 floats
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.UseProgram(_textShaderProgram);

        // 创建正交投影矩阵 - 使用像素坐标系
        // left=0, right=windowWidth, top=0, bottom=windowHeight (Y轴向下)
        var orthoProjection = Matrix4x4.CreateOrthographicOffCenter(0, windowWidth, windowHeight, 0, -1, 1);
        SetUniformMatrix4(_gl, _textShaderProgram, "uProjection", orthoProjection);

        // 设置 uniform - 文本颜色为绿色
        int colorLoc = _gl.GetUniformLocation(_textShaderProgram, "uColor");
        _gl.Uniform4(colorLoc, 0.0f, 1.0f, 0.0f, 1.0f);  // 绿色 (0, 1, 0, 1)

        int texLoc = _gl.GetUniformLocation(_textShaderProgram, "uTexture");
        _gl.Uniform1(texLoc, 0);

        // 绑定纹理
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, textTexture);

        // 启用混合
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);

        // 绘制矩形（三角形条带）
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // 删除临时纹理
        _gl.DeleteTexture(textTexture);
    }

    private Matrix4x4 CreateOrthoMatrix(float left, float right, float bottom, float top, float near, float far)
    {
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

    private unsafe void DrawRoiBorder(Rect roiRect, Matrix4x4 projection)
    {
        if (_gl == null) return;

        // 创建边框顶点 (四个角和闭合)
        var roiBorderColor = RoiBorderColor;
        var vertices = new float[]
        {
            (float)roiRect.Left, (float)roiRect.Top, 0, roiBorderColor.R / 255f, roiBorderColor.G / 255f, roiBorderColor.B / 255f, roiBorderColor.A / 255f,
            (float)roiRect.Right, (float)roiRect.Top, 0, roiBorderColor.R / 255f, roiBorderColor.G / 255f, roiBorderColor.B / 255f, roiBorderColor.A / 255f,
            (float)roiRect.Right, (float)roiRect.Bottom, 0, roiBorderColor.R / 255f, roiBorderColor.G / 255f, roiBorderColor.B / 255f, roiBorderColor.A / 255f,
            (float)roiRect.Left, (float)roiRect.Bottom, 0, roiBorderColor.R / 255f, roiBorderColor.G / 255f, roiBorderColor.B / 255f, roiBorderColor.A / 255f,
            (float)roiRect.Left, (float)roiRect.Top, 0, roiBorderColor.R / 255f, roiBorderColor.G / 255f, roiBorderColor.B / 255f, roiBorderColor.A / 255f,
        };

        // 创建或更新 VAO/VBO
        if (_roiVao == 0)
        {
            _roiVao = _gl.GenVertexArray();
            _roiVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_roiVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _roiVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        // 设置顶点属性指针 (每次都设置确保正确)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.UseProgram(_shaderProgram);
        SetUniformMatrix4(_gl, _shaderProgram, "uModel", Matrix4x4.Identity);
        SetUniformMatrix4(_gl, _shaderProgram, "uView", Matrix4x4.Identity);
        SetUniformMatrix4(_gl, _shaderProgram, "uProjection", projection);

        _gl.LineWidth(2.0f);
        _gl.DrawArrays(PrimitiveType.LineStrip, 0, 5);
        _gl.LineWidth(1.0f);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void DrawRoiFill(Rect roiRect, Matrix4x4 projection)
    {
        if (_gl == null) return;

        var roiColor = RoiColor;
        var vertices = new float[]
        {
            (float)roiRect.Left, (float)roiRect.Top, 0, roiColor.R / 255f, roiColor.G / 255f, roiColor.B / 255f, roiColor.A / 255f,
            (float)roiRect.Right, (float)roiRect.Top, 0, roiColor.R / 255f, roiColor.G / 255f, roiColor.B / 255f, roiColor.A / 255f,
            (float)roiRect.Right, (float)roiRect.Bottom, 0, roiColor.R / 255f, roiColor.G / 255f, roiColor.B / 255f, roiColor.A / 255f,
            (float)roiRect.Left, (float)roiRect.Bottom, 0, roiColor.R / 255f, roiColor.G / 255f, roiColor.B / 255f, roiColor.A / 255f,
        };

        // 使用同一个 VAO 用于填充（复用 _roiVao）
        _gl.BindVertexArray(_roiVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _roiVbo);

        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        // 设置顶点属性指针
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.UseProgram(_shaderProgram);
        SetUniformMatrix4(_gl, _shaderProgram, "uModel", Matrix4x4.Identity);
        SetUniformMatrix4(_gl, _shaderProgram, "uView", Matrix4x4.Identity);
        SetUniformMatrix4(_gl, _shaderProgram, "uProjection", projection);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

        _gl.Disable(EnableCap.Blend);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
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
        _roiStart = e.GetPosition(this);
        _roiEnd = _roiStart;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(this);
        
        // 计算和显示鼠标的世界坐标
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        
        if (width > 0 && height > 0)
        {
            _currentMouseWorldPosition = ScreenToWorld(currentPos, width, height);
            UpdateMousePositionDisplay();
        }

        if (_isDrawingRoi)
        {
            _roiEnd = currentPos;
            Render();
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

    /// <summary>
    /// 更新鼠标位置显示（现在仅用于触发 OpenGL 覆盖层渲染）
    /// </summary>
    private void UpdateMousePositionDisplay()
    {
        // 不再更新 WPF 文本，Render() 中会读取 _currentMouseWorldPosition 并在 OpenGL 覆盖层绘制
        Render();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingRoi)
        {
            _isDrawingRoi = false;
            ReleaseMouseCapture();

            _roiEnd = e.GetPosition(this);

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

            Render();
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

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom -= e.Delta * 0.005f;
        _zoom = Math.Clamp(_zoom, 1f, 50f);
        _cameraPosition = new Vector3(0, 0, _zoom);
        Render();
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

    /// <summary>
    /// 将屏幕坐标转换为世界坐标（沿着摄像机看向平面投影）
    /// </summary>
    private Vector3 ScreenToWorld(Point screenPos, int width, int height)
    {
        // 将屏幕坐标转换为 NDC 坐标 (-1 到 1)
        float ndcX = (float)(screenPos.X / width * 2 - 1);
        float ndcY = -(float)(screenPos.Y / height * 2 - 1); // Y 轴翻转

        // 构建投影矩阵的逆矩阵
        var model = Matrix4x4.CreateRotationX(_rotationX) * Matrix4x4.CreateRotationY(_rotationY);
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);
        var mvp = model * view * projection;

        if (!Matrix4x4.Invert(mvp, out var mvpInverse))
        {
            return Vector3.Zero;
        }

        // NDC 坐标在近平面上
        var ndcPos = new Vector4(ndcX, ndcY, -1, 1);
        var worldPos = Vector4.Transform(ndcPos, mvpInverse);

        if (worldPos.W != 0)
        {
            return new Vector3(worldPos.X / worldPos.W, worldPos.Y / worldPos.W, worldPos.Z / worldPos.W);
        }

        return Vector3.Zero;
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
