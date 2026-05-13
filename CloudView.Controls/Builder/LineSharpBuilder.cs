using CloudView.Controls.Model;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

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
