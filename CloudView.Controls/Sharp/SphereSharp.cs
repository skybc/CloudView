using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

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
