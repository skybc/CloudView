using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

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
