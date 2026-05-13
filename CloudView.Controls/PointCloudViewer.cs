using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
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
        RegisterSharpBuilder(new PanelSharpBuilder());
        RegisterSharpBuilder(new LineSharpBuilder());
        RegisterSharpBuilder(new VolumeSharpBuilder());
        RegisterSharpBuilder(new SphereSharpBuilder());
        RegisterSharpBuilder(new CylinderSharpBuilder());
    }

    private void RegisterSharpBuilder(ISharpRenderBuilder builder)
    {
        _sharpBuilders[builder.TargetType] = builder;
    }

    private static void OnShapesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PointCloudViewer viewer)
        {
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

        ClearShapeBuffers();

        if (Shapes == null || Shapes.Count == 0)
        {
            _sharpNeedsRebuild = false;
            return;
        }

        foreach (var shape in Shapes)
        {
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
        // 直接匹配或寻找最近的基类匹配
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

        int idx = 0;
        foreach (var shape in Shapes ?? new List<BaseSharp>())
        {
            if (idx >= _sharpRenderItems.Count)
                break;

            var item = _sharpRenderItems[idx];

            // 处理填充渲染
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
            bool shouldRenderOutline = false;
            if (shape is PanelSharp p)
                shouldRenderOutline = p.DrawOutline;
            else if (shape is VolumeSharp v)
                shouldRenderOutline = v.DrawOutline;

            if (shouldRenderOutline && item.PrimitiveType != PrimitiveType.LineStrip)
            {
                // Render outline using line strip along polygon edges
                _gl.LineWidth(item.LineWidth);
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                // For panels/volumes, we approximate outline by drawing as line loop
                _gl.BindVertexArray(item.Vao);
                _gl.DrawArrays(PrimitiveType.LineLoop, 0, (uint)item.VertexCount);
                _gl.BindVertexArray(0);

                _gl.LineWidth(1.0f);
                _gl.Disable(EnableCap.Blend);
            }

            // 处理线条渲染
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
