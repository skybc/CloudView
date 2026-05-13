using System.Numerics;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

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
