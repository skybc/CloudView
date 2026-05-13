using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

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
