using System.Numerics;
using System.Windows.Media;

namespace CloudView.Controls;

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
