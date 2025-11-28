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
/// 点云渲染控件，使用 Silk.NET.OpenGL 在 WPF 中实现三维点云的实时可视化。
/// 
/// 核心功能：
/// - 使用 VAO/VBO 高效渲染大规模点云数据集
/// - 支持摄像机操纵（旋转、缩放、平移）
/// - 支持鼠标矩形选择（ROI）
/// - 显示坐标系轴线和 XY 平面网格
/// - 实时文本覆盖层显示交互信息
/// - 双缓冲 OpenGL 渲染，60 FPS 渲染循环
/// 
/// 架构说明：
/// - 继承自 WPF 控件基类 (Control)，实现 IDisposable 以确保资源安全释放
/// - 使用 OpenGLHost (HwndHost) 在 WPF 中托管原生 Win32 窗口以进行 OpenGL 渲染
/// - 与 Win32Interop 合作管理原生 API 调用
/// - 采用依赖属性 (DependencyProperty) 实现 MVVM 数据绑定
/// - 事件驱动模式用于用户交互通知 (ROI 选择、鼠标位置)
/// </summary>
public partial class PointCloudViewer : Control, IDisposable
{
    static PointCloudViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PointCloudViewer), 
            new FrameworkPropertyMetadata(typeof(PointCloudViewer)));
    }

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

    public static readonly DependencyProperty AllowDrawingRoiProperty =
        DependencyProperty.Register(
            nameof(AllowDrawingRoi),
            typeof(bool),
            typeof(PointCloudViewer),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public static readonly DependencyProperty MinXProperty =
        DependencyProperty.Register(
            nameof(MinX),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MinValue, OnPointsChanged));

    public static readonly DependencyProperty MaxXProperty =
        DependencyProperty.Register(
            nameof(MaxX),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MaxValue, OnPointsChanged));

    public static readonly DependencyProperty MinYProperty =
        DependencyProperty.Register(
            nameof(MinY),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MinValue, OnPointsChanged));

    public static readonly DependencyProperty MaxYProperty =
        DependencyProperty.Register(
            nameof(MaxY),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MaxValue, OnPointsChanged));

    public static readonly DependencyProperty MinZProperty =
        DependencyProperty.Register(
            nameof(MinZ),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MinValue, OnPointsChanged));

    public static readonly DependencyProperty MaxZProperty =
        DependencyProperty.Register(
            nameof(MaxZ),
            typeof(float),
            typeof(PointCloudViewer),
            new PropertyMetadata(float.MaxValue, OnPointsChanged));

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

    /// <summary>
    /// 是否允许通过鼠标拖拽绘制 ROI 区域
    /// </summary>
    public bool AllowDrawingRoi
    {
        get => (bool)GetValue(AllowDrawingRoiProperty);
        set => SetValue(AllowDrawingRoiProperty, value);
    }

    /// <summary>
    /// 点云显示范围最小 X 坐标
    /// </summary>
    public float MinX
    {
        get => (float)GetValue(MinXProperty);
        set => SetValue(MinXProperty, value);
    }

    /// <summary>
    /// 点云显示范围最大 X 坐标
    /// </summary>
    public float MaxX
    {
        get => (float)GetValue(MaxXProperty);
        set => SetValue(MaxXProperty, value);
    }

    /// <summary>
    /// 点云显示范围最小 Y 坐标
    /// </summary>
    public float MinY
    {
        get => (float)GetValue(MinYProperty);
        set => SetValue(MinYProperty, value);
    }

    /// <summary>
    /// 点云显示范围最大 Y 坐标
    /// </summary>
    public float MaxY
    {
        get => (float)GetValue(MaxYProperty);
        set => SetValue(MaxYProperty, value);
    }

    /// <summary>
    /// 点云显示范围最小 Z 坐标
    /// </summary>
    public float MinZ
    {
        get => (float)GetValue(MinZProperty);
        set => SetValue(MinZProperty, value);
    }

    /// <summary>
    /// 点云显示范围最大 Z 坐标
    /// </summary>
    public float MaxZ
    {
        get => (float)GetValue(MaxZProperty);
        set => SetValue(MaxZProperty, value);
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

    internal IntPtr _hDC;
    internal IntPtr _hGLRC;
    internal GL? _gl;
    internal bool _isInitialized;

    // 着色器程序
    private uint _shaderProgram;
    private uint _overlayShaderProgram;
    private uint _overlayVao;
    private uint _overlayVbo;
    private uint _textShaderProgram;
    private uint _textVao;
    private uint _textVbo;
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

    // 点云中心
    private Vector3 _pointCloudCenter = Vector3.Zero;

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

    // 性能优化：网格缓存
    private float _lastGridZoom = -1f;
    private bool _gridNeedsUpdate = true;

    // 性能优化：脏标记，控制是否需要重新渲染
    private bool _needsRender = true;

    // 性能优化：文本缓存
    private uint _cachedTextTexture;
    private string _cachedTextContent = string.Empty;
    private int _cachedTextWidth;
    private int _cachedTextHeight;

    // 性能优化：LOD 相关
    private int _currentLodLevel;  // 0=全量, 1=1/2, 2=1/4, 3=1/8...
    private bool _useLod = true;  // 是否启用 LOD
    private const int LodThreshold = 100000;  // 超过此点数启用 LOD
    private const int MaxDisplayPoints = 2000000;  // 最大显示点数

    // 资源管理
    private bool _disposed;  // 标记是否已释放资源
    private IntPtr _opengl32Handle;  // opengl32.dll 模块句柄，用于统一释放
    private IntPtr _hwnd;  // 原生窗口句柄，用于释放 DC

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
    /// 释放控件使用的所有资源。
    /// 实现 IDisposable 模式，确保 OpenGL 资源、Win32 句柄和事件处理器被正确清理。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放托管和非托管资源。
    /// </summary>
    /// <param name="disposing">如果为 true，则释放托管资源；否则只释放非托管资源。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // 释放托管资源
            _renderTimer?.Stop();
            _renderTimer = null;

            // 解绑事件处理器
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            MouseLeftButtonDown -= OnMouseLeftButtonDown;
            MouseMove -= OnMouseMove;
            MouseLeftButtonUp -= OnMouseLeftButtonUp;
            MouseRightButtonDown -= OnMouseRightButtonDown;
            MouseRightButtonUp -= OnMouseRightButtonUp;
            MouseWheel -= OnMouseWheel;

            // 清理 OpenGL 宿主
            if (_glHost != null)
            {
                _glHostGrid?.Children.Remove(_glHost);
                _glHost.Dispose();
                _glHost = null;
            }
        }

        // 释放非托管资源（OpenGL 上下文、Win32 句柄等）
        CleanupOpenGL();

        _disposed = true;
    }

    /// <summary>
    /// 析构函数，确保非托管资源被释放。
    /// </summary>
    ~PointCloudViewer()
    {
        Dispose(false);
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

    // OpenGLHost 已提取为独立类，参见 OpenGLHost.cs

    #endregion

    #region OpenGL 初始化和清理

    /// <summary>
    /// 初始化 OpenGL 环境，创建渲染上下文并配置着色器等资源。
    /// </summary>
    /// <param name="hwnd">要绑定 OpenGL 的窗口句柄。</param>
    /// <exception cref="Exception">当设备上下文、像素格式或上下文创建失败时抛出。</exception>
    internal void InitializeOpenGL(IntPtr hwnd)
    {
        _hwnd = hwnd;  // 保存窗口句柄，用于后续释放 DC
        _hDC = Win32Interop.GetDC(hwnd);
        if (_hDC == IntPtr.Zero)
            throw new Exception("Failed to get device context");

        var pfd = new Win32Interop.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<Win32Interop.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Win32Interop.PFD_DRAW_TO_WINDOW | Win32Interop.PFD_SUPPORT_OPENGL | Win32Interop.PFD_DOUBLEBUFFER,
            iPixelType = Win32Interop.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = Win32Interop.PFD_MAIN_PLANE
        };

        int pixelFormat = Win32Interop.ChoosePixelFormat(_hDC, ref pfd);
        if (pixelFormat == 0)
            throw new Exception("Failed to choose pixel format");

        if (!Win32Interop.SetPixelFormat(_hDC, pixelFormat, ref pfd))
            throw new Exception("Failed to set pixel format");

        _hGLRC = Win32Interop.wglCreateContext(_hDC);
        if (_hGLRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL context");

        if (!Win32Interop.wglMakeCurrent(_hDC, _hGLRC))
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
        var addr = Win32Interop.wglGetProcAddress(name);
        if (addr != IntPtr.Zero)
            return addr;

        // 缓存 opengl32.dll 句柄，避免每次调用都 LoadLibrary 导致句柄泄漏
        if (_opengl32Handle == IntPtr.Zero)
        {
            _opengl32Handle = Win32Interop.LoadLibrary("opengl32.dll");
        }
        return Win32Interop.GetProcAddress(_opengl32Handle, name);
    }

    /// <summary>
    /// 清理 OpenGL 资源，删除缓冲、程序、纹理等，并销毁上下文。
    /// 此方法确保所有 GPU 资源和 Win32 资源被正确释放。
    /// </summary>
    internal void CleanupOpenGL()
    {
        if (_gl != null && _isInitialized)
        {
            // 确保当前上下文可用
            if (_hDC != IntPtr.Zero && _hGLRC != IntPtr.Zero)
            {
                Win32Interop.wglMakeCurrent(_hDC, _hGLRC);
            }

            // 清理点云 VAO/VBO（添加检查）
            if (_vao != 0)
            {
                _gl.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vbo != 0)
            {
                _gl.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            // 清理主着色器程序（添加检查）
            if (_shaderProgram != 0)
            {
                _gl.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
            }

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

            // 清理覆盖层 VAO/VBO（之前遗漏）
            if (_overlayVao != 0)
            {
                _gl.DeleteVertexArray(_overlayVao);
                _overlayVao = 0;
            }
            if (_overlayVbo != 0)
            {
                _gl.DeleteBuffer(_overlayVbo);
                _overlayVbo = 0;
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

            // 清理缓存的文本纹理
            if (_cachedTextTexture != 0)
            {
                _gl.DeleteTexture(_cachedTextTexture);
                _cachedTextTexture = 0;
                _cachedTextContent = string.Empty;
            }

            _gl = null;
        }

        // 释放 OpenGL 上下文
        if (_hGLRC != IntPtr.Zero)
        {
            Win32Interop.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Win32Interop.wglDeleteContext(_hGLRC);
            _hGLRC = IntPtr.Zero;
        }

        // 释放设备上下文（之前遗漏）
        if (_hDC != IntPtr.Zero && _hwnd != IntPtr.Zero)
        {
            Win32Interop.ReleaseDC(_hwnd, _hDC);
            _hDC = IntPtr.Zero;
        }

        // 释放 opengl32.dll 句柄
        if (_opengl32Handle != IntPtr.Zero)
        {
            Win32Interop.FreeLibrary(_opengl32Handle);
            _opengl32Handle = IntPtr.Zero;
        }

        _isInitialized = false;
    }

    /// <summary>
    /// 初始化着色器程序，包括点云渲染着色器和 ROI 覆盖层着色器。
    /// 
    /// 编译并链接两个着色器程序：
    /// 1. 主着色器程序：用于 3D 点云、坐标系和网格的渲染，支持模型、视图、投影矩阵变换
    /// 2. 覆盖层着色器程序：用于屏幕空间的 ROI 矩形渲染，使用 NDC 坐标
    /// 
    /// 着色器包含位置和颜色顶点属性，输出带颜色的像素。
    /// </summary>
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

    /// <summary>
    /// 初始化文本渲染着色器程序。
    /// 
    /// 用于在右上角渲染鼠标世界坐标文本。着色器使用正交投影处理像素坐标的顶点，
    /// 并通过纹理采样绘制文本字形。支持 Alpha 混合以实现文本透明效果。
    /// </summary>
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

    /// <summary>
    /// 初始化顶点缓冲对象 (VAO/VBO)，用于存储和管理点云数据。
    /// 
    /// 创建一个 VAO 和 VBO，配置顶点属性指针：
    /// - 位置属性 (location=0)：每个顶点 3 个浮点数 (X, Y, Z)
    /// - 颜色属性 (location=1)：每个顶点 4 个浮点数 (R, G, B, A)
    /// - 顶点跨度：7 个浮点数 (56 字节)
    /// 
    /// VBO 使用动态绘制用法 (DynamicDraw)，支持实时数据更新。
    /// </summary>
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

    /// <summary>
    /// 创建坐标系轴线和 XY 平面网格的初始化。
    /// 
    /// 坐标系：三条轴线从原点出发，X 红、Y 绿、Z 蓝，长度各为 1.0 单位。
    /// 网格：XY 平面 (Z=0) 的网格线，范围和间距根据摄像机缩放动态调整。
    /// </summary>
    private unsafe void CreateCoordinateAxisAndGrid()
    {
        if (_gl == null) return;

        // 创建坐标系轴线
        CreateCoordinateAxis();

        // 网格在 Render 中动态生成
    }

    /// <summary>
    /// 创建坐标系轴线的 VAO/VBO，包含 X、Y、Z 三条轴线。
    /// 
    /// 轴线定义：
    /// - X 轴（红色）：从中心位置到中心 + (1,0,0)
    /// - Y 轴（绿色）：从中心位置到中心 + (0,1,0)
    /// - Z 轴（蓝色）：从中心位置到中心 + (0,0,1)
    /// 
    /// 每条轴线由 2 个顶点组成，共 6 个顶点。使用线段模式 (GL_LINES) 绘制。
    /// 中心位置通过 _pointCloudCenter 定义。
    /// </summary>
    private unsafe void CreateCoordinateAxis()
    {
        if (_gl == null) return;

        // 坐标系轴线：X轴(红), Y轴(绿), Z轴(蓝)
        // 每条轴线长度为 1.0，从点云中心出发
        const float axisLength = 1.0f;
        var axisVertices = new float[]
        {
            // X 轴 (红色)
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 1, 0, 0, 1,  // 起点
            _pointCloudCenter.X + axisLength, _pointCloudCenter.Y, _pointCloudCenter.Z, 1, 0, 0, 1,  // 终点

            // Y 轴 (绿色)
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 0, 1, 0, 1,  // 起点
            _pointCloudCenter.X, _pointCloudCenter.Y + axisLength, _pointCloudCenter.Z, 0, 1, 0, 1,  // 终点

            // Z 轴 (蓝色)
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 0, 0, 1, 1,  // 起点
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z + axisLength, 0, 0, 1, 1,  // 终点
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

    /// <summary>
    /// 动态更新 XY 平面网格的顶点数据。
    /// 
    /// 网格特性：
    /// - 位置：XY 平面，Z = 中心点的 Z 坐标
    /// - 范围：根据参数 gridRange 对称分布在中心点周围 [-gridRange, gridRange]
    /// - 间距：0.1 单位固定间距
    /// - 颜色：浅灰色 (0.7, 0.7, 0.7)，透明度 30%
    /// - 更新频率：每帧调用，支持随摄像机缩放动态调整网格覆盖范围
    /// 
    /// 网格由平行于 X 轴和 Y 轴的直线组成。
    /// 中心位置通过 _pointCloudCenter 定义。
    /// 使用 DynamicDraw 缓冲用法以支持频繁更新。
    /// </summary>
    /// <param name="gridRange">网格范围，决定网格的最大延伸距离。</param>
    private unsafe void UpdateXYGrid(float gridRange)
    {
        if (_gl == null) return;

        // 创建 XY 平面网格，中心为点云中心
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
            gridVertices.Add(_pointCloudCenter.X - gridRange);
            gridVertices.Add(_pointCloudCenter.Y + y);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            // 终点
            gridVertices.Add(_pointCloudCenter.X + gridRange);
            gridVertices.Add(_pointCloudCenter.Y + y);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);
        }

        // Y 方向的线（平行于 Y 轴）
        for (float x = -gridRange; x <= gridRange; x += gridSpacing)
        {
            // 起点
            gridVertices.Add(_pointCloudCenter.X + x);
            gridVertices.Add(_pointCloudCenter.Y - gridRange);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            // 终点
            gridVertices.Add(_pointCloudCenter.X + x);
            gridVertices.Add(_pointCloudCenter.Y + gridRange);
            gridVertices.Add(_pointCloudCenter.Z);
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

        Win32Interop.wglMakeCurrent(_hDC, _hGLRC);

        var points = Points;
        int totalPoints = points.Count;
        
        // 获取坐标范围限制
        float minX = MinX;
        float maxX = MaxX;
        float minY = MinY;
        float maxY = MaxY;
        float minZ = MinZ;
        float maxZ = MaxZ;

        // 性能优化：计算 LOD 级别
        // 根据点数量决定抽稀比例
        int stride = 1;
        if (_useLod && totalPoints > LodThreshold)
        {
            // 计算需要的抽稀倍数，确保最终点数不超过 MaxDisplayPoints
            stride = (totalPoints + MaxDisplayPoints - 1) / MaxDisplayPoints;
            _currentLodLevel = (int)Math.Log2(stride);
        }
        else
        {
            _currentLodLevel = 0;
        }

        // 预计算目标数组大小以减少内存分配
        int estimatedCount = (totalPoints + stride - 1) / stride;
        var vertices = new float[estimatedCount * 7];
        int vertexIndex = 0;

        // 使用步长遍历点云实现抽稀
        for (int i = 0; i < totalPoints; i += stride)
        {
            var point = points[i];
            
            // 检查点是否在范围内
            if (point.Position.X >= minX && point.Position.X <= maxX &&
                point.Position.Y >= minY && point.Position.Y <= maxY &&
                point.Position.Z >= minZ && point.Position.Z <= maxZ)
            {
                vertices[vertexIndex++] = point.Position.X;
                vertices[vertexIndex++] = point.Position.Y;
                vertices[vertexIndex++] = point.Position.Z;
                vertices[vertexIndex++] = point.Color.X;
                vertices[vertexIndex++] = point.Color.Y;
                vertices[vertexIndex++] = point.Color.Z;
                vertices[vertexIndex++] = point.Color.W;
            }
        }

        _vertexCount = vertexIndex / 7;

        if (_vertexCount == 0) return;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexIndex * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        _needsRender = true;
    }

    #endregion

    #region 渲染

    /// <summary>
    /// 核心渲染循环，每帧调用一次以绘制完整的场景。
    /// 
    /// 渲染步骤：
    /// 1. 激活 OpenGL 上下文（wglMakeCurrent）
    /// 2. 设置视口和清除背景颜色
    /// 3. 启用深度测试和点大小设置
    /// 4. 计算模型、视图、投影矩阵并应用旋转变换
    /// 5. 按顺序绘制：
    ///    - 坐标系轴线（如果启用）
    ///    - XY 平面网格（如果启用）
    ///    - 点云数据
    ///    - ROI 矩形（如果正在绘制）
    ///    - 文本覆盖层（鼠标世界坐标）
    /// 6. 交换前后缓冲区（SwapBuffers）
    /// 
    /// 用于 60 FPS 渲染循环，由 DispatcherTimer 每 16ms 触发一次。
    /// </summary>
    private unsafe void Render()
    {
        if (_gl == null || !_isInitialized) return;

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

        // 设置矩阵（用于坐标系、网格和点云）
        var model = Matrix4x4.Identity;
        var view = CreateLookAtMatrix(_cameraPosition, _cameraTarget, _cameraUp);
        var projection = CreatePerspectiveMatrix(_fov * MathF.PI / 180f, (float)width / height, 0.1f, 1000f);

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

        // 绘制 XY 平面网格（仅在需要时更新）
        if (ShowGrid)
        {
            // 根据摄像机缩放计算网格范围
            float gridRange = _zoom * 0.5f;
            
            // 性能优化：仅在 zoom 变化或标记需要更新时才重建网格 VBO
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

        Win32Interop.SwapBuffers(_hDC);
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

        // 直接使用点的坐标值（单位：mm）
        float xMm = _currentMouseWorldPosition.X;
        float yMm = _currentMouseWorldPosition.Y;
        float zMm = _currentMouseWorldPosition.Z;

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

        // 性能优化：将所有坐标文本合并为一个纹理，避免每帧创建多个纹理
        const float fontSize = 16;
        const float marginRight = 10;
        const float marginTop = 10;
        
        float startX = width - overlayWidthPx + marginRight;
        float startY = marginTop;

        // 生成合并的文本内容
        string combinedText = $"X: {xMm:F3} mm\nY: {yMm:F3} mm\nZ: {zMm:F3} mm";
        DrawCachedText(combinedText, startX, startY, fontSize, width, height);
    }

    /// <summary>
    /// 绘制带缓存的文本（仅在内容变化时重新生成纹理）
    /// </summary>
    private unsafe void DrawCachedText(string text, float pixelX, float pixelY, float fontSize, int windowWidth, int windowHeight)
    {
        if (_gl == null || _textShaderProgram == 0)
            return;

        // 检查是否需要更新纹理
        if (_cachedTextTexture == 0 || _cachedTextContent != text)
        {
            // 删除旧纹理
            if (_cachedTextTexture != 0)
            {
                _gl.DeleteTexture(_cachedTextTexture);
            }
            
            // 创建新纹理
            _cachedTextTexture = CreateMultiLineTextTexture(text, (int)fontSize, out _cachedTextWidth, out _cachedTextHeight);
            _cachedTextContent = text;
        }

        // 使用缓存的纹理尺寸
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

    /// <summary>
    /// 创建多行文本纹理
    /// </summary>
    private unsafe uint CreateMultiLineTextTexture(string text, int fontSize, out int texWidth, out int texHeight)
    {
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

    /// <summary>
    /// 创建视图矩阵（LookAt 矩阵）。
    /// 
    /// 根据摄像机位置、目标位置和向上向量构造视图矩阵，将世界坐标转换到摄像机坐标系。
    /// 
    /// 算法：
    /// 1. 计算 Z 轴（摄像机到目标的反方向）：zAxis = normalize(eye - target)
    /// 2. 计算 X 轴（右向量）：xAxis = normalize(cross(up, zAxis))
    /// 3. 计算 Y 轴（真实向上向量）：yAxis = cross(zAxis, xAxis)
    /// 4. 构造 4x4 矩阵并设置平移分量
    /// 
    /// 这是标准的 OpenGL 视图矩阵构造方法。
    /// </summary>
    /// <param name="eye">摄像机位置。</param>
    /// <param name="target">摄像机看向的目标点。</param>
    /// <param name="up">摄像机的向上向量。</param>
    /// <returns>视图矩阵。</returns>
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

    /// <summary>
    /// 创建透视投影矩阵。
    /// 
    /// 根据视角、宽高比、近远平面距离构造透视投影矩阵，
    /// 将摄像机坐标转换为裁剪空间坐标用于光栅化。
    /// 
    /// 参数：
    /// - fov：视场角（弧度制），即摄像机竖直方向的可见角度
    /// - aspect：宽高比 (width/height)
    /// - near/far：近远平面距离，应 > 0 且 near < far
    /// 
    /// 这是标准的 OpenGL 透视矩阵构造方法。
    /// </summary>
    /// <param name="fov">视场角（弧度）。</param>
    /// <param name="aspect">宽高比（width/height）。</param>
    /// <param name="near">近平面距离。</param>
    /// <param name="far">远平面距离。</param>
    /// <returns>透视投影矩阵。</returns>
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

    /// <summary>
    /// 处理控件大小变化，调整 OpenGL 视口。
    /// </summary>
    /// <param name="width">新的宽度（像素）。</param>
    /// <param name="height">新的高度（像素）。</param>
    internal void OnResize(int width, int height)
    {
        if (_gl != null && _isInitialized && width > 0 && height > 0)
        {
            Win32Interop.wglMakeCurrent(_hDC, _hGLRC);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            Render();
        }
    }

    #endregion

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
        
        // 计算和显示鼠标的世界坐标
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        
        if (width > 0 && height > 0)
        {
            _currentMouseWorldPosition = ScreenToWorld(currentPos, width, height);
            // 性能优化：仅标记需要渲染，不直接调用 Render
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
            
            // 围绕视野中心旋转摄像机
            UpdateCameraPositionWithRotation();
            
            _lastMousePosition = currentPos;
            _needsRender = true;
        }
    }

    /// <summary>
    /// 根据旋转角度更新摄像机位置。
    /// 摄像机围绕 _cameraTarget（视野中心）进行旋转。
    /// 使用球面坐标：相对于目标点的距离、水平角、竖直角。
    /// </summary>
    private void UpdateCameraPositionWithRotation()
    {
        // 获取摄像机到目标的向量
        var direction = _cameraPosition - _cameraTarget;
        float distance = direction.Length();

        if (distance == 0) return;

        // 应用旋转矩阵来更新摄像机位置
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

            // 计算选中的点
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

            // 更新依赖属性
            SelectedIndices = selectedIndices;
            SelectedPoints = selectedPoints;

            // 触发事件
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

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom -= e.Delta * 0.005f;
        _zoom = Math.Clamp(_zoom, 1f, 50f);
        
        // 保持当前旋转状态，根据当前方向更新摄像机位置
        // 获取摄像机到目标点的当前方向（单位向量）
        var direction = _cameraPosition - _cameraTarget;
        float currentDistance = direction.Length();
        
        if (currentDistance > 0)
        {
            // 使用当前方向，只改变距离
            var normalizedDirection = Vector3.Normalize(direction);
            _cameraPosition = _cameraTarget + normalizedDirection * _zoom;
        }
        else
        {
            // 如果距离为0，使用默认Z轴方向
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

        // 转换到屏幕坐标 (注意 Y 轴翻转)
        double screenX = (ndcPos.X + 1) * 0.5 * width;
        double screenY = (1 - ndcPos.Y) * 0.5 * height;

        return new Point(screenX, screenY);
    }

    /// <summary>
    /// 将屏幕坐标转换为世界坐标（沿着摄像机看向平面投影）
    /// </summary>
    /// <summary>
    /// 将屏幕坐标转换为世界坐标。
    /// 
    /// 转换算法：
    /// 1. 屏幕坐标 → NDC 坐标：x' = x/width*2 - 1，y' = 1 - y/height*2
    /// 2. 计算 MVP 矩阵的逆矩阵
    /// 3. 在近平面 (z=-1) 处应用逆矩阵变换
    /// 4. 透视除法 (x/w, y/w, z/w) 得到世界坐标
    /// 
    /// 此方法用于获取鼠标在三维空间中的位置，用于显示鼠标坐标和交互。
    /// </summary>
    /// <param name="screenPos">屏幕上的鼠标位置（像素坐标）。</param>
    /// <param name="width">渲染窗口宽度（像素）。</param>
    /// <param name="height">渲染窗口高度（像素）。</param>
    /// <returns>世界坐标中的位置向量。矩阵不可逆时返回 Vector3.Zero。</returns>
    private Vector3 ScreenToWorld(Point screenPos, int width, int height)
    {
        // 将屏幕坐标转换为 NDC 坐标 (-1 到 1)
        float ndcX = (float)(screenPos.X / width * 2 - 1);
        float ndcY = -(float)(screenPos.Y / height * 2 - 1); // Y 轴翻转

        // 构建投影矩阵的逆矩阵
        var model = Matrix4x4.Identity;
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
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    /// <summary>
    /// 渲染定时器回调，使用脏标记控制是否需要实际渲染
    /// </summary>
    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        if (_needsRender)
        {
            _needsRender = false;
            Render();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 停止渲染定时器
        _renderTimer?.Stop();
        _renderTimer = null;

        // 解绑事件处理器，防止内存泄漏
        MouseLeftButtonDown -= OnMouseLeftButtonDown;
        MouseMove -= OnMouseMove;
        MouseLeftButtonUp -= OnMouseLeftButtonUp;
        MouseRightButtonDown -= OnMouseRightButtonDown;
        MouseRightButtonUp -= OnMouseRightButtonUp;
        MouseWheel -= OnMouseWheel;
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            viewer.UpdatePointCloudBuffer();
            
            // 计算点云中心并调整视野
            viewer._pointCloudCenter = viewer.CalculatePointCloudCenter();
            viewer._cameraTarget = viewer._pointCloudCenter;
            viewer._rotationX = 0;
            viewer._rotationY = 0;
            
            // 重新创建坐标轴和网格以适应新的中心位置
            if (viewer._isInitialized)
            {
                viewer.CreateCoordinateAxis();
                viewer._gridNeedsUpdate = true;
            }
            
            viewer._needsRender = true;
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
            viewer._needsRender = true;
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 计算点云的中心位置。
    /// 
    /// 算法：
    /// 1. 遍历所有点，计算 X、Y、Z 坐标的最大最小值
    /// 2. 计算中心点：((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2)
    /// 3. 返回中心点坐标
    /// 
    /// 如果点集为空则返回 Vector3.Zero。
    /// 
    /// 此方法在 Points 属性更新时调用。
    /// </summary>
    /// <returns>点云的中心坐标。</returns>
    private Vector3 CalculatePointCloudCenter()
    {
        if (Points == null || Points.Count == 0)
        {
            return Vector3.Zero;
        }

        // 获取坐标范围限制
        float rangeMinX = MinX;
        float rangeMaxX = MaxX;
        float rangeMinY = MinY;
        float rangeMaxY = MaxY;
        float rangeMinZ = MinZ;
        float rangeMaxZ = MaxZ;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var point in Points)
        {
            // 检查点是否在范围内
            if (point.Position.X < rangeMinX || point.Position.X > rangeMaxX ||
                point.Position.Y < rangeMinY || point.Position.Y > rangeMaxY ||
                point.Position.Z < rangeMinZ || point.Position.Z > rangeMaxZ)
            {
                continue;
            }

            minX = Math.Min(minX, point.Position.X);
            maxX = Math.Max(maxX, point.Position.X);
            minY = Math.Min(minY, point.Position.Y);
            maxY = Math.Max(maxY, point.Position.Y);
            minZ = Math.Min(minZ, point.Position.Z);
            maxZ = Math.Max(maxZ, point.Position.Z);
        }

        // 如果没有点在范围内，返回零向量
        if (minX == float.MaxValue)
        {
            return Vector3.Zero;
        }

        var center = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f
        );

        return center;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 从 float 数组加载点云数据
    /// </summary>
    /// <param name="positions">点的位置数组 (x, y, z, x, y, z, ...)</param>
    /// <param name="defaultColor">默认颜色</param>
    /// <summary>
    /// 从浮点数组加载点云数据。
    /// 
    /// 输入格式：浮点数数组，连续存储点的 (x, y, z) 坐标。
    /// 转换流程：每 3 个浮点数作为一个点的位置，使用 defaultColor 作为颜色。
    /// 
    /// 示例：
    /// <code>
    /// float[] positions = { 0, 0, 0,  1, 0, 0,  0, 1, 0 }; // 3 个点
    /// viewer.LoadFromFloatArray(positions, new Vector4(1, 1, 1, 1)); // 白色
    /// </code>
    /// 
    /// 调用此方法会触发 UpdatePointCloudBuffer 以更新 OpenGL 缓冲。
    /// </summary>
    /// <param name="positions">点位置的浮点数数组，格式为 [x1, y1, z1, x2, y2, z2, ...]。</param>
    /// <param name="defaultColor">所有点的颜色。如为 null 则使用白色 (1, 1, 1, 1)。</param>
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
    /// <summary>
    /// 从 Vector3 列表加载点云数据。
    /// 
    /// 输入为 Vector3 位置集合，所有点使用 defaultColor 作为颜色。
    /// 
    /// 示例：
    /// <code>
    /// var positions = new List&lt;Vector3&gt;
    /// {
    ///     new(0, 0, 0),
    ///     new(1, 0, 0),
    ///     new(0, 1, 0)
    /// };
    /// viewer.LoadFromVector3List(positions, new Vector4(1, 0, 0, 1)); // 红色
    /// </code>
    /// 
    /// 调用此方法会触发点云缓冲更新和重新渲染。
    /// </summary>
    /// <param name="positions">点位置的 Vector3 列表。</param>
    /// <param name="defaultColor">所有点的颜色。如为 null 则使用白色 (1, 1, 1, 1)。</param>
    public void LoadFromVector3List(List<Vector3> positions, Vector4? defaultColor = null)
    {
        var color = defaultColor ?? new Vector4(1, 1, 1, 1);
        var points = positions.Select(p => new PointCloudPoint(p, color)).ToList();
        Points = points;
    }

    /// <summary>
    /// 重置相机视图
    /// </summary>
    /// <summary>
    /// 重置摄像机视图到初始状态。
    /// 
    /// 重置参数：
    /// - 旋转：X 和 Y 轴旋转都重置为 0
    /// - 缩放：缩放因子 (zoom) 重置为 5.0
    /// - 位置：摄像机位置重置为 (0, 0, 5)
    /// - 目标：指向原点 (0, 0, 0)
    /// 
    /// 调用此方法后会立即触发重新渲染。
    /// </summary>
    public void ResetView()
    {
        _rotationX = 0;
        _rotationY = 0;
        _zoom = 5;
        _cameraTarget = Vector3.Zero;
        _cameraPosition = new Vector3(0, 0, _zoom);
        _needsRender = true;
    }

    /// <summary>
    /// 自动调整视图以适应所有点
    /// </summary>
    /// <summary>
    /// 自动调整摄像机以显示所有点。
    /// 
    /// 算法：
    /// 1. 计算点集的 AABB 包围盒（最小和最大坐标）
    /// 2. 计算中心点和包围盒对角线长度
    /// 3. 设置目标点为包围盒中心
    /// 4. 设置缩放因子为对角线长度的 2 倍
    /// 5. 重置旋转，摄像机沿 Z 轴方向观看
    /// 
    /// 调用此方法后会立即触发重新渲染。
    /// 
    /// 如果点集为空则不执行任何操作。
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

        _needsRender = true;
    }

    #endregion
}
