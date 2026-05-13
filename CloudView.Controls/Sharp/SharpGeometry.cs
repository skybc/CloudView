using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

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
