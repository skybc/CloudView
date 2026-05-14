using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Silk.NET.OpenGL;
using PixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace CloudView.Controls;

/// <summary>
/// 点云查看器的控制层。
/// <para>
/// 该类本身不直接承担所有 OpenGL 细节，而是负责把 WPF 依赖属性、事件、
/// 摄像机状态、ROI 交互状态和各个 partial 文件中的渲染逻辑串起来。
/// </para>
/// </summary>
public partial class PointCloudViewer : Control, IDisposable
{
    /// <summary>
    /// ROI 交互的内部状态机。
    /// </summary>
    private enum RoiInteractionMode
    {
        None,
        Move,
        Resize,
        Rotate,
    }

    static PointCloudViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PointCloudViewer),
            new FrameworkPropertyMetadata(typeof(PointCloudViewer)));
    }

    #region 依赖属性

    // 这些依赖属性就是外部宿主和 XAML 的主要入口。
    // 它们一旦变化，就会触发缓冲区重建、ROI 重算或重绘标记，保证 UI 状态与 GPU 状态同步。
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

    public static readonly DependencyProperty RoisProperty =
        DependencyProperty.Register(
            nameof(Rois),
            typeof(IList<RoiBase>),
            typeof(PointCloudViewer),
            new PropertyMetadata(null, OnRoisChanged));

    /// <summary>
    /// 外部传入的点云数据集合。
    /// <para>
    /// 当该值变化时，会重新计算可见点、点云中心、坐标轴与网格的参考中心。
    /// </para>
    /// </summary>
    public IList<PointCloudPoint>? Points
    {
        get => (IList<PointCloudPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>
    /// 点在屏幕上的像素大小。
    /// </summary>
    public float PointSize
    {
        get => (float)GetValue(PointSizeProperty);
        set => SetValue(PointSizeProperty, value);
    }

    /// <summary>
    /// 渲染背景色。
    /// </summary>
    public Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// 当前 ROI 筛选得到的点集合。
    /// </summary>
    public IReadOnlyList<PointCloudPoint>? SelectedPoints
    {
        get => (IReadOnlyList<PointCloudPoint>?)GetValue(SelectedPointsProperty);
        set => SetValue(SelectedPointsProperty, value);
    }

    /// <summary>
    /// 当前 ROI 筛选得到的点索引集合。
    /// </summary>
    public IReadOnlyList<int>? SelectedIndices
    {
        get => (IReadOnlyList<int>?)GetValue(SelectedIndicesProperty);
        set => SetValue(SelectedIndicesProperty, value);
    }

    /// <summary>
    /// 是否显示点云中心的三维坐标轴。
    /// </summary>
    public bool ShowCoordinateAxis
    {
        get => (bool)GetValue(ShowCoordinateAxisProperty);
        set => SetValue(ShowCoordinateAxisProperty, value);
    }

    /// <summary>
    /// 是否显示 XY 平面网格。
    /// </summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>
    /// 可见点的 X 轴下界。
    /// </summary>
    public float MinX
    {
        get => (float)GetValue(MinXProperty);
        set => SetValue(MinXProperty, value);
    }

    /// <summary>
    /// 可见点的 X 轴上界。
    /// </summary>
    public float MaxX
    {
        get => (float)GetValue(MaxXProperty);
        set => SetValue(MaxXProperty, value);
    }

    /// <summary>
    /// 可见点的 Y 轴下界。
    /// </summary>
    public float MinY
    {
        get => (float)GetValue(MinYProperty);
        set => SetValue(MinYProperty, value);
    }

    /// <summary>
    /// 可见点的 Y 轴上界。
    /// </summary>
    public float MaxY
    {
        get => (float)GetValue(MaxYProperty);
        set => SetValue(MaxYProperty, value);
    }

    /// <summary>
    /// 可见点的 Z 轴下界。
    /// </summary>
    public float MinZ
    {
        get => (float)GetValue(MinZProperty);
        set => SetValue(MinZProperty, value);
    }

    /// <summary>
    /// 可见点的 Z 轴上界。
    /// </summary>
    public float MaxZ
    {
        get => (float)GetValue(MaxZProperty);
        set => SetValue(MaxZProperty, value);
    }

    /// <summary>
    /// 外部传入的 ROI 集合。
    /// </summary>
    public IList<RoiBase>? Rois
    {
        get => (IList<RoiBase>?)GetValue(RoisProperty);
        set => SetValue(RoisProperty, value);
    }

    /// <summary>
    /// 当前活动 ROI。
    /// </summary>
    public RoiBase? ActiveRoi => _activeRoi;

    /// <summary>
    /// 当前活动 ROI 的筛选结果。
    /// </summary>
    public RoiFilterResult ActiveRoiFilterResult => _activeRoiFilterResult;

    /// <summary>
    /// 当前活动 ROI 的统计结果。
    /// </summary>
    public RoiStatisticsResult ActiveRoiStatisticsResult => _activeRoiStatisticsResult;

    #endregion

    #region 事件

    public static readonly RoutedEvent MousePositionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(MousePositionChanged),
            RoutingStrategy.Bubble,
            typeof(EventHandler<MousePositionEventArgs>),
            typeof(PointCloudViewer));

    public static readonly RoutedEvent RoiSelectedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(RoiSelected),
            RoutingStrategy.Bubble,
            typeof(EventHandler<RoiSelectionEventArgs>),
            typeof(PointCloudViewer));

    public event EventHandler<MousePositionEventArgs>? MousePositionChanged
    {
        add => AddHandler(MousePositionChangedEvent, value);
        remove => RemoveHandler(MousePositionChangedEvent, value);
    }

    /// <summary>
    /// ROI 筛选结果更新时触发。
    /// </summary>
    public event EventHandler<RoiSelectionEventArgs>? RoiSelected
    {
        add => AddHandler(RoiSelectedEvent, value);
        remove => RemoveHandler(RoiSelectedEvent, value);
    }

    /// <summary>
    /// ROI 结果发生变化时触发。
    /// </summary>
    public event EventHandler<RoiResultsChangedEventArgs>? RoiResultsChanged;

    /// <summary>
    /// 活动 ROI 变化时触发。
    /// </summary>
    public event EventHandler<ActiveRoiChangedEventArgs>? ActiveRoiChanged;

    /// <summary>
    /// ROI 被编辑后触发。
    /// </summary>
    public event EventHandler<RoiEditedEventArgs>? RoiEdited;

    #endregion

    #region 私有字段

    // 下面这组字段都是“渲染设备与 GPU 资源句柄”，由 OpenGL 初始化/清理流程统一管理。
    internal IntPtr _hDC;
    internal IntPtr _hGLRC;
    internal GL? _gl;
    internal bool _isInitialized;

    // 主场景、覆盖层、文本和辅助图元分别使用独立的着色器/VAO/VBO，
    // 这样可以把不同原语的渲染状态隔离开，避免状态污染。
    private uint _shaderProgram;
    private uint _overlayShaderProgram;
    private uint _overlayVao;
    private uint _overlayVbo;
    private uint _textShaderProgram;
    private uint _textVao;
    private uint _textVbo;
    private uint _vao;
    private uint _vbo;
    private int _vertexCount;

    // 坐标轴与网格是随点云中心变化的辅助参考系。
    private uint _axisVao;
    private uint _axisVbo;
    private int _axisVertexCount;

    private uint _gridVao;
    private uint _gridVbo;
    private int _gridVertexCount;

    // 摄像机使用“目标点 + 距离 + 欧拉角”方式描述：
    // - _cameraTarget 是观察中心
    // - _cameraPosition 是眼睛位置
    // - _rotationX/_rotationY 控制 Orbit 旋转
    // - _zoom 决定眼睛到目标的距离
    private Vector3 _cameraPosition = new(0, 0, 5);
    private Vector3 _cameraTarget = Vector3.Zero;
    private Vector3 _cameraUp = Vector3.UnitY;
    private float _fov = 45.0f;
    private float _rotationX;
    private float _rotationY;
    private float _zoom = 5.0f;

    private Vector3 _pointCloudCenter = Vector3.Zero;

    // 鼠标交互状态：旋转、平移和 ROI 编辑互斥或半互斥，由这里统一协调。
    private bool _isRotating;
    private bool _isPanning;
    private Point _lastMousePosition;
    private Vector3 _panOffset = Vector3.Zero;

    private Grid? _glHostGrid;
    private HwndHost? _glHost;
    private Vector3 _currentMouseWorldPosition = Vector3.Zero;

    private DispatcherTimer? _renderTimer;

    // 网格不必每一帧都重建，只有缩放变化明显或首次显示时才刷新。
    private float _lastGridZoom = -1f;
    private bool _gridNeedsUpdate = true;

    // 通过脏标记把频繁输入事件和实际渲染解耦，避免鼠标一动就重绘。
    private bool _needsRender = true;

    // 文本纹理和内容做缓存，减少每帧创建/销毁纹理带来的开销。
    private uint _cachedTextTexture;
    private string _cachedTextContent = string.Empty;
    private int _cachedTextWidth;
    private int _cachedTextHeight;

    private uint _gizmoVao;
    private uint _gizmoVbo;
    private readonly Dictionary<string, (uint texture, int width, int height)> _gizmoTextCache = new();

    // ROI 的可视化分成三层：
    // 1. _roiRenderItems：最终上传 GPU 的渲染项
    // 2. _roiVisualShapes：先以通用几何对象描述 ROI 的线框/实体
    // 3. _roiScreenSegments：用于屏幕命中测试的线段集合
    private readonly List<SharpRenderItem> _roiRenderItems = new();
    private readonly List<BaseSharp> _roiVisualShapes = new();
    private readonly List<RoiScreenSegment> _roiScreenSegments = new();
    private readonly List<RoiHandleVisual> _roiHandleVisuals = new();
    private bool _roiNeedsRebuild;
    private RoiBase? _activeRoi;
    private RoiHandleVisual? _activeRoiHandle;
    private RoiHandleVisual? _hoveredHandle;
    private RoiInteractionMode _roiInteractionMode;
    private bool _isLeftGesturePending;
    private bool _leftGestureMoved;
    private Point _leftGestureStartPoint;
    private RoiBase? _pendingSelectionRoi;
    private RoiBase? _pendingMoveRoi;
    private RoiHandleVisual? _pendingHandle;
    private Vector3 _interactionPlanePoint;
    private Vector3 _interactionPlaneNormal = Vector3.UnitZ;
    private Vector3 _lastInteractionWorldPoint;
    private Point _lastInteractionScreenPoint;
    private RoiFilterResult _activeRoiFilterResult = RoiFilterResult.Empty;
    private RoiStatisticsResult _activeRoiStatisticsResult = RoiStatisticsResult.Empty;
    private const double RoiClickMoveThreshold = 4.0;

    private int _currentLodLevel;
    private bool _useLod = true;
    private const int LodThreshold = 100000;
    private const int MaxDisplayPoints = 2000000;

    private bool _disposed;
    private IntPtr _opengl32Handle;
    private IntPtr _hwnd;

    #endregion

    public PointCloudViewer()
    {
        // 初始化共享的几何构建器注册表，并订阅 WPF 生命周期事件。
        InitializeSharpSupport();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _renderTimer?.Stop();
            _renderTimer = null;

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            MouseLeftButtonDown -= OnMouseLeftButtonDown;
            MouseMove -= OnMouseMove;
            MouseLeftButtonUp -= OnMouseLeftButtonUp;
            MouseRightButtonDown -= OnMouseRightButtonDown;
            MouseRightButtonUp -= OnMouseRightButtonUp;
            MouseWheel -= OnMouseWheel;

            if (_glHost != null)
            {
                _glHostGrid?.Children.Remove(_glHost);
                _glHost.Dispose();
                _glHost = null;
            }
        }

        CleanupOpenGL();

        _disposed = true;
    }

    ~PointCloudViewer()
    {
        Dispose(false);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 模板里的 PART_GLHost 是承载原生 OpenGL 窗口的锚点。
        _glHostGrid = (Grid)GetTemplateChild("PART_GLHost");

        if (_glHostGrid == null)
        {
            throw new InvalidOperationException("Required template element PART_GLHost not found.");
        }

        _glHost = new OpenGLHost(this);
        _glHostGrid.Children.Add(_glHost);
    }

    #region 事件处理

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 渲染循环由 DispatcherTimer 驱动，约 60 FPS。
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseWheel += OnMouseWheel;
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        // 只有在有脏标记时才真正渲染，避免空转。
        if (_needsRender)
        {
            _needsRender = false;
            Render();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderTimer?.Stop();
        _renderTimer = null;

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
            // 点云变化后，GPU 缓冲、中心点、坐标轴和 ROI 都要重新对齐。
            viewer.UpdatePointCloudBuffer();

            viewer._pointCloudCenter = viewer.CalculatePointCloudCenter();
            viewer._cameraTarget = viewer._pointCloudCenter;
            viewer._rotationX = 0;
            viewer._rotationY = 0;

            if (viewer._isInitialized)
            {
                viewer.CreateCoordinateAxis();
                viewer._gridNeedsUpdate = true;
            }

            viewer.OnRoiInputsChanged();

            viewer._needsRender = true;
        }
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            viewer._needsRender = true;
        }
    }

    #endregion

    #region 私有方法

    private Vector3 CalculatePointCloudCenter()
    {
        if (Points == null || Points.Count == 0)
        {
            return Vector3.Zero;
        }

        // 只用当前“可见范围内”的点来求 AABB 中心，保证视角、坐标轴和网格
        // 都围绕实际显示内容，而不是被范围外的数据拉偏。
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

        if (minX == float.MaxValue)
        {
            return Vector3.Zero;
        }

        // AABB 中心 = (min + max) / 2。
        var center = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f
        );

        return center;
    }

    #endregion

    private partial bool TryBeginRoiInteraction(Point position);

    private partial bool TryPromotePendingLeftGestureToAction(Point currentPosition);

    private partial void CompletePendingLeftGestureSelection();

    private partial void ClearPendingLeftGestureState();

    private partial void UpdateRoiInteraction(Point currentPosition);

    private partial void CompleteRoiInteraction(bool raiseEditedEvent);

    #region 公共方法

    public void LoadFromFloatArray(float[] positions, Vector4? defaultColor = null)
    {
        // 每三个浮点数构成一个点坐标，颜色则统一使用默认值。
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

    public void LoadFromVector3List(List<Vector3> positions, Vector4? defaultColor = null)
    {
        // 这里直接把 Vector3 序列映射成 PointCloudPoint，减少宿主侧转换负担。
        var color = defaultColor ?? new Vector4(1, 1, 1, 1);
        var points = positions.Select(p => new PointCloudPoint(p, color)).ToList();
        Points = points;
    }

    public void ResetView()
    {
        // 重置视图时，恢复到一个最朴素的“看向原点”的状态。
        _rotationX = 0;
        _rotationY = 0;
        _zoom = 5;
        _cameraTarget = Vector3.Zero;
        _cameraPosition = new Vector3(0, 0, _zoom);
        _roiNeedsRebuild = true;
        _needsRender = true;
    }

    public void FitToView()
    {
        if (Points == null || Points.Count == 0) return;

        // 通过整个点集的包围盒估算观察中心和推荐距离。
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

        _roiNeedsRebuild = true;
        _needsRender = true;
    }

    #endregion
}
