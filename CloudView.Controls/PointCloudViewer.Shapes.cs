using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    // 这条依赖属性是“普通几何对象”渲染通道的入口，与业务 ROI 的 Rois 依赖属性是分离的。
    public static readonly DependencyProperty ShapesProperty =
        DependencyProperty.Register(
            nameof(Shapes),
            typeof(IList<BaseSharp>),
            typeof(PointCloudViewer),
            new PropertyMetadata(null, OnShapesChanged));

    /// <summary>
    /// 需要渲染的几何对象集合。
    /// </summary>
    public IList<BaseSharp>? Shapes
    {
        get => (IList<BaseSharp>?)GetValue(ShapesProperty);
        set => SetValue(ShapesProperty, value);
    }

    private readonly Dictionary<Type, ISharpRenderBuilder> _sharpBuilders = new();
    private readonly List<SharpRenderItem> _sharpRenderItems = new();
    private bool _sharpNeedsRebuild;

    private void InitializeSharpSupport()
    {
        // 构建器注册表采用“类型 → builder”的方式，避免把几何生成逻辑硬编码到渲染循环里。
        RegisterSharpBuilder(new PanelSharpBuilder());
        RegisterSharpBuilder(new LineSharpBuilder());
        RegisterSharpBuilder(new VolumeSharpBuilder());
        RegisterSharpBuilder(new SphereSharpBuilder());
        RegisterSharpBuilder(new CylinderSharpBuilder());
    }

    private void RegisterSharpBuilder(ISharpRenderBuilder builder)
    {
        // 后注册的 builder 会覆盖同类型旧注册，便于扩展或替换实现。
        _sharpBuilders[builder.TargetType] = builder;
    }

    private static void OnShapesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
            // Shapes 变化后，需要重建缓冲并触发重绘。
            viewer._sharpNeedsRebuild = true;
            viewer.UpdateShapesBuffers();
            viewer._needsRender = true;
        }
    }

    private void UpdateShapesBuffers()
    {
        if (_gl == null || !_isInitialized)
        {
            return;
        }

        // 先清掉旧的几何缓冲，避免内存和 GPU 资源泄漏。
        ClearShapeBuffers();

        if (Shapes == null || Shapes.Count == 0)
        {
            _sharpNeedsRebuild = false;
            return;
        }

        foreach (var shape in Shapes)
        {
            // 通过运行时类型解析对应 builder，实现“几何对象类型”和“GPU 生成方式”的解耦。
            var builder = ResolveBuilder(shape.GetType());
            if (builder == null)
                continue;

            var geometry = builder.Build(shape);
            if (geometry.IsEmpty)
                continue;

            var renderItem = CreateShapeRenderItem(geometry);
            if (renderItem != null)
            {
                _sharpRenderItems.Add(renderItem.Value);
            }
        }

        _sharpNeedsRebuild = false;
    }

    private ISharpRenderBuilder? ResolveBuilder(Type shapeType)
    {
        // 允许从派生类型一路向上回退到基类，提升 builder 复用能力。
        Type? current = shapeType;
        while (current != null)
        {
            if (_sharpBuilders.TryGetValue(current, out var builder))
            {
                return builder;
            }
            current = current.BaseType;
        }
        return null;
    }

    private unsafe SharpRenderItem? CreateShapeRenderItem(SharpGeometry geometry)
    {
        if (_gl == null || geometry.Vertices.Length == 0 || geometry.VertexCount <= 0)
            return null;

        // 和点云缓冲一样，几何对象使用 7 float/顶点：位置 + 颜色。
        uint vao = _gl.GenVertexArray();
        uint vbo = _gl.GenBuffer();

        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        unsafe
        {
            fixed (float* data = geometry.Vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(geometry.Vertices.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
            }
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        return new SharpRenderItem(vao, vbo, geometry.VertexCount, geometry.PrimitiveType, geometry.EnableBlend, geometry.LineWidth);
    }

    private void ClearShapeBuffers()
    {
        if (_gl == null)
        {
            _sharpRenderItems.Clear();
            return;
        }

        // 删除前先检查句柄是否为 0，避免重复释放。
        foreach (var item in _sharpRenderItems)
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

        _sharpRenderItems.Clear();
    }

    private void RenderShapes()
    {
        if (_gl == null || _shaderProgram == 0 || _sharpRenderItems.Count == 0)
            return;

        // Shapes 的渲染顺序与输入集合顺序保持一致，方便宿主按层叠顺序控制显示效果。
        int idx = 0;
        foreach (var shape in Shapes ?? new List<BaseSharp>())
        {
            if (idx >= _sharpRenderItems.Count)
                break;

            var item = _sharpRenderItems[idx];

            // 处理填充渲染
            // 面片/体积类几何可以根据对象配置决定是否绘制填充。
            bool shouldRenderFill = true;
            if (shape is PanelSharp panel)
                shouldRenderFill = panel.DrawFill;
            else if (shape is VolumeSharp volume)
                shouldRenderFill = volume.DrawFill;

            if (shouldRenderFill && item.PrimitiveType != PrimitiveType.LineStrip)
            {
                if (item.EnableBlend)
                {
                    _gl.Enable(EnableCap.Blend);
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                }

                _gl.BindVertexArray(item.Vao);
                _gl.DrawArrays(item.PrimitiveType, 0, (uint)item.VertexCount);
                _gl.BindVertexArray(0);

                if (item.EnableBlend)
                {
                    _gl.Disable(EnableCap.Blend);
                }
            }

            // 处理轮廓渲染
            // 轮廓渲染一般作为第二遍叠加，以增强几何边界的可读性。
            bool shouldRenderOutline = false;
            if (shape is PanelSharp p)
                shouldRenderOutline = p.DrawOutline;
            else if (shape is VolumeSharp v)
                shouldRenderOutline = v.DrawOutline;

            if (shouldRenderOutline && item.PrimitiveType != PrimitiveType.LineStrip)
            {
                _gl.LineWidth(item.LineWidth);
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                _gl.BindVertexArray(item.Vao);
                _gl.DrawArrays(PrimitiveType.LineLoop, 0, (uint)item.VertexCount);
                _gl.BindVertexArray(0);

                _gl.LineWidth(1.0f);
                _gl.Disable(EnableCap.Blend);
            }

            // 处理线条渲染
            // 线条型几何单独走 LineStrip 分支，避免和面片/体积逻辑混杂。
            if (item.PrimitiveType == PrimitiveType.LineStrip)
            {
                _gl.LineWidth(item.LineWidth);
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                _gl.BindVertexArray(item.Vao);
                _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)item.VertexCount);
                _gl.BindVertexArray(0);

                _gl.LineWidth(1.0f);
                _gl.Disable(EnableCap.Blend);
            }

            idx++;
        }
    }

    private void CleanupSharpBuffers()
    {
        // 统一复用 ClearShapeBuffers，避免维护两套释放逻辑。
        ClearShapeBuffers();
    }

    private readonly struct SharpRenderItem
    {
        public SharpRenderItem(uint vao, uint vbo, int vertexCount, PrimitiveType primitiveType, bool enableBlend, float lineWidth = 1.0f)
        {
            Vao = vao;
            Vbo = vbo;
            VertexCount = vertexCount;
            PrimitiveType = primitiveType;
            EnableBlend = enableBlend;
            LineWidth = lineWidth;
        }

        public uint Vao { get; }
        public uint Vbo { get; }
        public int VertexCount { get; }
        public PrimitiveType PrimitiveType { get; }
        public bool EnableBlend { get; }
        public float LineWidth { get; }
    }
}
