using Silk.NET.OpenGL;

namespace CloudView.Controls;

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
