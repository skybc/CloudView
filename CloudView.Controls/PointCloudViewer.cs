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

public partial class PointCloudViewer : Control, IDisposable
{
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

    public IList<PointCloudPoint>? Points
    {
        get => (IList<PointCloudPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public float PointSize
    {
        get => (float)GetValue(PointSizeProperty);
        set => SetValue(PointSizeProperty, value);
    }

    public Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public IReadOnlyList<PointCloudPoint>? SelectedPoints
    {
        get => (IReadOnlyList<PointCloudPoint>?)GetValue(SelectedPointsProperty);
        set => SetValue(SelectedPointsProperty, value);
    }

    public IReadOnlyList<int>? SelectedIndices
    {
        get => (IReadOnlyList<int>?)GetValue(SelectedIndicesProperty);
        set => SetValue(SelectedIndicesProperty, value);
    }

    public bool ShowCoordinateAxis
    {
        get => (bool)GetValue(ShowCoordinateAxisProperty);
        set => SetValue(ShowCoordinateAxisProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public float MinX
    {
        get => (float)GetValue(MinXProperty);
        set => SetValue(MinXProperty, value);
    }

    public float MaxX
    {
        get => (float)GetValue(MaxXProperty);
        set => SetValue(MaxXProperty, value);
    }

    public float MinY
    {
        get => (float)GetValue(MinYProperty);
        set => SetValue(MinYProperty, value);
    }

    public float MaxY
    {
        get => (float)GetValue(MaxYProperty);
        set => SetValue(MaxYProperty, value);
    }

    public float MinZ
    {
        get => (float)GetValue(MinZProperty);
        set => SetValue(MinZProperty, value);
    }

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

    internal IntPtr _hDC;
    internal IntPtr _hGLRC;
    internal GL? _gl;
    internal bool _isInitialized;

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

    private uint _axisVao;
    private uint _axisVbo;
    private int _axisVertexCount;

    private uint _gridVao;
    private uint _gridVbo;
    private int _gridVertexCount;

    private Vector3 _cameraPosition = new(0, 0, 5);
    private Vector3 _cameraTarget = Vector3.Zero;
    private Vector3 _cameraUp = Vector3.UnitY;
    private float _fov = 45.0f;
    private float _rotationX;
    private float _rotationY;
    private float _zoom = 5.0f;

    private Vector3 _pointCloudCenter = Vector3.Zero;

    private bool _isRotating;
    private bool _isPanning;
    private Point _lastMousePosition;
    private Vector3 _panOffset = Vector3.Zero;

    private Grid? _glHostGrid;
    private HwndHost? _glHost;
    private Vector3 _currentMouseWorldPosition = Vector3.Zero;

    private DispatcherTimer? _renderTimer;

    private float _lastGridZoom = -1f;
    private bool _gridNeedsUpdate = true;

    private bool _needsRender = true;

    private uint _cachedTextTexture;
    private string _cachedTextContent = string.Empty;
    private int _cachedTextWidth;
    private int _cachedTextHeight;

    private uint _gizmoVao;
    private uint _gizmoVbo;
    private readonly Dictionary<string, (uint texture, int width, int height)> _gizmoTextCache = new();

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
        var color = defaultColor ?? new Vector4(1, 1, 1, 1);
        var points = positions.Select(p => new PointCloudPoint(p, color)).ToList();
        Points = points;
    }

    public void ResetView()
    {
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
