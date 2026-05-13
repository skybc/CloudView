using Silk.NET.OpenGL;

namespace CloudView.Controls;

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
