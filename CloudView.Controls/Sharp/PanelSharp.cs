using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

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
