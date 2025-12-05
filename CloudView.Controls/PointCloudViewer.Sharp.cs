using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

/// <summary>
/// 几何对象基类，所有可渲染的几何类型均继承此类。
/// </summary>
public abstract class BaseSharp
{
    /// <summary>
    /// 对象标识。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 对象名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// 面片类型，使用一组顶点定义一个平面区域。
/// </summary>
public sealed class PanelSharp : BaseSharp
{
    public PanelSharp(IEnumerable<Vector3> vertices, Color? color = null, bool drawFill = true, bool drawOutline = false, float lineWidth = 1.0f)
    {
        Vertices = vertices?.ToList() ?? throw new ArgumentNullException(nameof(vertices));
        Color = color ?? Color.FromArgb(120, 0, 180, 255);
        DrawFill = drawFill;
        DrawOutline = drawOutline;
        LineWidth = lineWidth;
    }

    /// <summary>
    /// 按顺序定义的顶点列表，至少三个点。
    /// </summary>
    public IList<Vector3> Vertices { get; }

    /// <summary>
    /// 面片填充颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 是否绘制填充面。
    /// </summary>
    public bool DrawFill { get; set; }

    /// <summary>
    /// 是否绘制边框轮廓。
    /// </summary>
    public bool DrawOutline { get; set; }

    /// <summary>
    /// 轮廓线宽度（仅在 DrawOutline=true 时有效）。
    /// </summary>
    public float LineWidth { get; set; }
}

/// <summary>
/// 线段类型，连接一组顶点的折线。
/// </summary>
public sealed class LineSharp : BaseSharp
{
    public LineSharp(IEnumerable<Vector3> vertices, Color? color = null, float lineWidth = 2.0f, bool isClosed = false)
    {
        Vertices = vertices?.ToList() ?? throw new ArgumentNullException(nameof(vertices));
        Color = color ?? Color.FromArgb(255, 255, 255, 255);
        LineWidth = lineWidth;
        IsClosed = isClosed;
    }

    /// <summary>
    /// 线段顶点列表，至少两个点。
    /// </summary>
    public IList<Vector3> Vertices { get; }

    /// <summary>
    /// 线段颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 线段宽度。
    /// </summary>
    public float LineWidth { get; set; }

    /// <summary>
    /// 是否闭合（最后一个点连接到第一个点）。
    /// </summary>
    public bool IsClosed { get; set; }
}

/// <summary>
/// 体积类型，用多个三角形定义一个三维体积。
/// </summary>
public sealed class VolumeSharp : BaseSharp
{
    public VolumeSharp(IEnumerable<Vector3> vertices, IEnumerable<uint>? indices = null, Color? color = null, bool drawFill = true, bool drawOutline = false, float lineWidth = 1.0f)
    {
        Vertices = vertices?.ToList() ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices?.ToList() ?? GenerateDefaultIndices(Vertices.Count);
        Color = color ?? Color.FromArgb(100, 100, 150, 255);
        DrawFill = drawFill;
        DrawOutline = drawOutline;
        LineWidth = lineWidth;
    }

    /// <summary>
    /// 顶点列表。
    /// </summary>
    public IList<Vector3> Vertices { get; }

    /// <summary>
    /// 三角形索引（每三个索引定义一个三角形）。
    /// </summary>
    public IList<uint> Indices { get; }

    /// <summary>
    /// 体积填充颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 是否绘制填充体。
    /// </summary>
    public bool DrawFill { get; set; }

    /// <summary>
    /// 是否绘制边框轮廓。
    /// </summary>
    public bool DrawOutline { get; set; }

    /// <summary>
    /// 轮廓线宽度（仅在 DrawOutline=true 时有效）。
    /// </summary>
    public float LineWidth { get; set; }

    private static List<uint> GenerateDefaultIndices(int vertexCount)
    {
        var indices = new List<uint>();
        for (uint i = 0; i < (uint)vertexCount; i++)
        {
            indices.Add(i);
        }
        return indices;
    }
}

/// <summary>
/// 球体类型，用三角形网格定义一个球体。
/// </summary>
public sealed class SphereSharp : BaseSharp
{
    public SphereSharp(Vector3 center, float radius, Color? color = null, int stacks = 20, int slices = 20, bool drawFill = true, bool drawOutline = false, float lineWidth = 1.0f)
    {
        Center = center;
        Radius = radius;
        Color = color ?? Color.FromArgb(150, 100, 150, 200);
        Stacks = Math.Max(3, stacks);
        Slices = Math.Max(3, slices);
        DrawFill = drawFill;
        DrawOutline = drawOutline;
        LineWidth = lineWidth;
    }

    /// <summary>
    /// 球心。
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// 球的半径。
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// 球体颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 垂直分段数。
    /// </summary>
    public int Stacks { get; }

    /// <summary>
    /// 水平分段数。
    /// </summary>
    public int Slices { get; }

    /// <summary>
    /// 是否绘制填充面。
    /// </summary>
    public bool DrawFill { get; set; }

    /// <summary>
    /// 是否绘制边框轮廓。
    /// </summary>
    public bool DrawOutline { get; set; }

    /// <summary>
    /// 轮廓线宽度（仅在 DrawOutline=true 时有效）。
    /// </summary>
    public float LineWidth { get; set; }
}

/// <summary>
/// 圆柱体类型，用三角形网格定义一个圆柱体。
/// </summary>
public sealed class CylinderSharp : BaseSharp
{
    public CylinderSharp(Vector3 center, float radius, float height, Color? color = null, int slices = 20, bool drawFill = true, bool drawOutline = false, float lineWidth = 1.0f, bool includeCaps = true)
    {
        Center = center;
        Radius = radius;
        Height = height;
        Color = color ?? Color.FromArgb(150, 200, 100, 100);
        Slices = Math.Max(3, slices);
        DrawFill = drawFill;
        DrawOutline = drawOutline;
        LineWidth = lineWidth;
        IncludeCaps = includeCaps;
    }

    /// <summary>
    /// 圆柱中心点（底面中心）。
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// 圆柱半径。
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// 圆柱高度。
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// 圆柱体颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 周围分段数。
    /// </summary>
    public int Slices { get; }

    /// <summary>
    /// 是否绘制填充面。
    /// </summary>
    public bool DrawFill { get; set; }

    /// <summary>
    /// 是否绘制边框轮廓。
    /// </summary>
    public bool DrawOutline { get; set; }

    /// <summary>
    /// 轮廓线宽度（仅在 DrawOutline=true 时有效）。
    /// </summary>
    public float LineWidth { get; set; }

    /// <summary>
    /// 是否包含顶部和底部盖子。
    /// </summary>
    public bool IncludeCaps { get; }
}

/// <summary>
/// 统一的几何渲染数据。
/// </summary>
internal readonly struct SharpGeometry
{
    public SharpGeometry(float[] vertices, PrimitiveType primitiveType, int vertexCount, bool enableBlend = true, float lineWidth = 1.0f, IList<uint>? indices = null)
    {
        Vertices = vertices;
        PrimitiveType = primitiveType;
        VertexCount = vertexCount;
        EnableBlend = enableBlend;
        LineWidth = lineWidth;
        Indices = indices;
    }

    public float[] Vertices { get; }
    public PrimitiveType PrimitiveType { get; }
    public int VertexCount { get; }
    public bool EnableBlend { get; }
    public float LineWidth { get; }
    public IList<uint>? Indices { get; }

    public bool IsEmpty => Vertices == null || VertexCount <= 0;

    public static SharpGeometry Empty => new(Array.Empty<float>(), PrimitiveType.Triangles, 0, false);
}

internal interface ISharpRenderBuilder
{
    Type TargetType { get; }
    SharpGeometry Build(BaseSharp shape);
}

internal sealed class PanelSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(PanelSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not PanelSharp panel || panel.Vertices.Count < 3)
        {
            return SharpGeometry.Empty;
        }

        int count = panel.Vertices.Count;
        var data = new float[count * 7];
        var color = panel.Color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        for (int i = 0; i < count; i++)
        {
            var v = panel.Vertices[i];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        return new SharpGeometry(data, PrimitiveType.TriangleFan, count, enableBlend: a < 0.999f, lineWidth: panel.LineWidth);
    }
}

internal sealed class LineSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(LineSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not LineSharp line || line.Vertices.Count < 2)
        {
            return SharpGeometry.Empty;
        }

        int count = line.Vertices.Count;
        if (line.IsClosed)
            count++;

        var data = new float[count * 7];
        var color = line.Color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        for (int i = 0; i < line.Vertices.Count; i++)
        {
            var v = line.Vertices[i];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        // 如果闭合，复制第一个顶点到最后
        if (line.IsClosed)
        {
            var v = line.Vertices[0];
            int offset = (line.Vertices.Count) * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        return new SharpGeometry(data, PrimitiveType.LineStrip, count, enableBlend: a < 0.999f, lineWidth: line.LineWidth);
    }
}

internal sealed class VolumeSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(VolumeSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not VolumeSharp volume || volume.Vertices.Count < 3 || volume.Indices.Count < 3)
        {
            return SharpGeometry.Empty;
        }

        var color = volume.Color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        // 为每个索引对应的顶点生成数据
        var data = new float[volume.Indices.Count * 7];

        for (int i = 0; i < volume.Indices.Count; i++)
        {
            var v = volume.Vertices[(int)volume.Indices[i]];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        return new SharpGeometry(data, PrimitiveType.Triangles, volume.Indices.Count, enableBlend: a < 0.999f, lineWidth: volume.LineWidth, indices: volume.Indices);
    }
}

internal sealed class SphereSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(SphereSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not SphereSharp sphere || sphere.Radius <= 0)
        {
            return SharpGeometry.Empty;
        }

        var vertices = new List<Vector3>();
        var indices = new List<uint>();

        int stacks = sphere.Stacks;
        int slices = sphere.Slices;

        // 生成球体顶点
        for (int i = 0; i <= stacks; i++)
        {
            float phi = MathF.PI * i / stacks;
            for (int j = 0; j <= slices; j++)
            {
                float theta = 2 * MathF.PI * j / slices;

                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);

                vertices.Add(sphere.Center + new Vector3(x, y, z) * sphere.Radius);
            }
        }

        // 生成球体面索引
        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                uint vertexA = (uint)(i * (slices + 1) + j);
                uint vertexB = (uint)(vertexA + slices + 1);

                indices.Add(vertexA);
                indices.Add(vertexB);
                indices.Add((uint)(vertexA + 1));

                indices.Add((uint)(vertexA + 1));
                indices.Add(vertexB);
                indices.Add((uint)(vertexB + 1));
            }
        }

        var color = sphere.Color;
        float rComp = color.R / 255f;
        float gComp = color.G / 255f;
        float bComp = color.B / 255f;
        float aComp = color.A / 255f;

        var data = new float[indices.Count * 7];

        for (int i = 0; i < indices.Count; i++)
        {
            var v = vertices[(int)indices[i]];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = rComp;
            data[offset + 4] = gComp;
            data[offset + 5] = bComp;
            data[offset + 6] = aComp;
        }

        return new SharpGeometry(data, PrimitiveType.Triangles, indices.Count, enableBlend: aComp < 0.999f, lineWidth: sphere.LineWidth, indices: indices.Cast<uint>().ToList());
    }
}

internal sealed class CylinderSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(CylinderSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not CylinderSharp cylinder || cylinder.Radius <= 0 || cylinder.Height <= 0)
        {
            return SharpGeometry.Empty;
        }

        var vertices = new List<Vector3>();
        var indices = new List<uint>();

        int slices = cylinder.Slices;
        Vector3 top = cylinder.Center + Vector3.UnitY * cylinder.Height;

        // 底面圆心
        uint bottomCenterIdx = (uint)vertices.Count;
        vertices.Add(cylinder.Center);

        // 顶面圆心
        uint topCenterIdx = (uint)vertices.Count;
        vertices.Add(top);

        // 底面圆周顶点
        uint bottomCircleStart = (uint)vertices.Count;
        for (int i = 0; i < slices; i++)
        {
            float angle = 2 * MathF.PI * i / slices;
            float x = MathF.Cos(angle) * cylinder.Radius;
            float z = MathF.Sin(angle) * cylinder.Radius;
            vertices.Add(cylinder.Center + new Vector3(x, 0, z));
        }

        // 顶面圆周顶点
        uint topCircleStart = (uint)vertices.Count;
        for (int i = 0; i < slices; i++)
        {
            float angle = 2 * MathF.PI * i / slices;
            float x = MathF.Cos(angle) * cylinder.Radius;
            float z = MathF.Sin(angle) * cylinder.Radius;
            vertices.Add(top + new Vector3(x, 0, z));
        }

        // 侧面三角形（逆时针方向，从外部看）
        for (int i = 0; i < slices; i++)
        {
            uint bottomCur = bottomCircleStart + (uint)i;
            uint bottomNext = bottomCircleStart + (uint)((i + 1) % slices);
            uint topCur = topCircleStart + (uint)i;
            uint topNext = topCircleStart + (uint)((i + 1) % slices);

            // 第一个三角形（底面当前 -> 顶面当前 -> 底面下一个）
            indices.Add(bottomCur);
            indices.Add(topCur);
            indices.Add(bottomNext);

            // 第二个三角形（底面下一个 -> 顶面当前 -> 顶面下一个）
            indices.Add(bottomNext);
            indices.Add(topCur);
            indices.Add(topNext);
        }

        // 如果包含盖子
        if (cylinder.IncludeCaps)
        {
            // 底面（顶面朝下，逆时针看）
            for (int i = 0; i < slices; i++)
            {
                uint cur = bottomCircleStart + (uint)i;
                uint next = bottomCircleStart + (uint)((i + 1) % slices);
                indices.Add(bottomCenterIdx);
                indices.Add(cur);
                indices.Add(next);
            }

            // 顶面（顶面朝上，逆时针看）
            for (int i = 0; i < slices; i++)
            {
                uint cur = topCircleStart + (uint)i;
                uint next = topCircleStart + (uint)((i + 1) % slices);
                indices.Add(topCenterIdx);
                indices.Add(next);
                indices.Add(cur);
            }
        }

        var color = cylinder.Color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        var data = new float[indices.Count * 7];

        for (int i = 0; i < indices.Count; i++)
        {
            var v = vertices[(int)indices[i]];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        return new SharpGeometry(data, PrimitiveType.Triangles, indices.Count, enableBlend: a < 0.999f, lineWidth: cylinder.LineWidth, indices: indices.Cast<uint>().ToList());
    }
}

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
