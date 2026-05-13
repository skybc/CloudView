using System.Numerics;
using CloudView.Controls.Model;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

internal sealed class CylinderSharpBuilder : ISharpRenderBuilder
{
    public Type TargetType => typeof(CylinderSharp);

    public SharpGeometry Build(BaseSharp shape)
    {
        if (shape is not CylinderSharp cylinder || cylinder.Radius <= 0 || cylinder.Height <= 0)
        {
            return SharpGeometry.Empty;
        }

        var vertices = new List<Vector3>();
        var indices = new List<uint>();

        int slices = cylinder.Slices;
        Vector3 top = cylinder.Center + Vector3.UnitY * cylinder.Height;

        // 底面圆心
        uint bottomCenterIdx = (uint)vertices.Count;
        vertices.Add(cylinder.Center);

        // 顶面圆心
        uint topCenterIdx = (uint)vertices.Count;
        vertices.Add(top);

        // 底面圆周顶点
        uint bottomCircleStart = (uint)vertices.Count;
        for (int i = 0; i < slices; i++)
        {
            float angle = 2 * MathF.PI * i / slices;
            float x = MathF.Cos(angle) * cylinder.Radius;
            float z = MathF.Sin(angle) * cylinder.Radius;
            vertices.Add(cylinder.Center + new Vector3(x, 0, z));
        }

        // 顶面圆周顶点
        uint topCircleStart = (uint)vertices.Count;
        for (int i = 0; i < slices; i++)
        {
            float angle = 2 * MathF.PI * i / slices;
            float x = MathF.Cos(angle) * cylinder.Radius;
            float z = MathF.Sin(angle) * cylinder.Radius;
            vertices.Add(top + new Vector3(x, 0, z));
        }

        // 侧面三角形（逆时针方向，从外部看）
        for (int i = 0; i < slices; i++)
        {
            uint bottomCur = bottomCircleStart + (uint)i;
            uint bottomNext = bottomCircleStart + (uint)((i + 1) % slices);
            uint topCur = topCircleStart + (uint)i;
            uint topNext = topCircleStart + (uint)((i + 1) % slices);

            // 第一个三角形（底面当前 -> 顶面当前 -> 底面下一个）
            indices.Add(bottomCur);
            indices.Add(topCur);
            indices.Add(bottomNext);

            // 第二个三角形（底面下一个 -> 顶面当前 -> 顶面下一个）
            indices.Add(bottomNext);
            indices.Add(topCur);
            indices.Add(topNext);
        }

        // 如果包含盖子
        if (cylinder.IncludeCaps)
        {
            // 底面（顶面朝下，逆时针看）
            for (int i = 0; i < slices; i++)
            {
                uint cur = bottomCircleStart + (uint)i;
                uint next = bottomCircleStart + (uint)((i + 1) % slices);
                indices.Add(bottomCenterIdx);
                indices.Add(cur);
                indices.Add(next);
            }

            // 顶面（顶面朝上，逆时针看）
            for (int i = 0; i < slices; i++)
            {
                uint cur = topCircleStart + (uint)i;
                uint next = topCircleStart + (uint)((i + 1) % slices);
                indices.Add(topCenterIdx);
                indices.Add(next);
                indices.Add(cur);
            }
        }

        var color = cylinder.Color;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        var data = new float[indices.Count * 7];

        for (int i = 0; i < indices.Count; i++)
        {
            var v = vertices[(int)indices[i]];
            int offset = i * 7;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
            data[offset + 3] = r;
            data[offset + 4] = g;
            data[offset + 5] = b;
            data[offset + 6] = a;
        }

        return new SharpGeometry(data, PrimitiveType.Triangles, indices.Count, enableBlend: a < 0.999f, lineWidth: cylinder.LineWidth, indices: indices.Cast<uint>().ToList());
    }
}
