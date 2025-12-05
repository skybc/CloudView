# 几何对象渲染系统 (Shape Rendering)

## 概述
从版本 2.1 起，CloudView 支持渲染任意几何对象，超越仅点云显示的限制。该系统采用**构建器模式**实现，各类型对象独立注册及渲染。

## 核心类型

### **BaseSharp** (基类)
所有可渲染几何对象的抽象基类：
- `Id`: 全局唯一标识符（自动生成 GUID）
- `Name`: 对象名称（便于调试）

### **具体几何类型**

| 类型 | 用途 | 特性 |
|------|------|------|
| **PanelSharp** | 平面区域、多边形 | 顶点列表、TriangleFan 渲染、支持填充/轮廓、可调线宽 |
| **LineSharp** | 线段、轨迹、网格线 | 顶点列表、LineStrip 渲染、支持闭合、可调线宽 |
| **VolumeSharp** | 任意多面体、网格 | 顶点+索引列表、三角形索引渲染、支持填充/轮廓 |
| **SphereSharp** | 球体 | 球心 + 半径、自动网格分段（Stacks×Slices）、支持填充/轮廓 |
| **CylinderSharp** | 圆柱体 | 中心 + 半径 + 高度、周向分段、支持顶底盖、支持填充/轮廓 |

## 构建器模式

每种几何类型对应一个 `ISharpRenderBuilder` 实现，负责将对象转换为 GPU 渲染数据：

```csharp
public interface ISharpRenderBuilder
{
    Type TargetType { get; }
    SharpGeometry Build(BaseSharp shape);
}
```

**五个内置构建器**：
1. `PanelSharpBuilder` → 转换多边形顶点为 TriangleFan 数据
2. `LineSharpBuilder` → 转换线段顶点为 LineStrip 数据
3. `VolumeSharpBuilder` → 转换索引几何为三角形数据
4. `SphereSharpBuilder` → 生成球面网格（UV 球参数化）
5. `CylinderSharpBuilder` → 生成圆柱侧面+顶底盖网格

## 渲染数据流

```
BaseSharp 对象 → 选择对应 Builder → SharpGeometry（顶点+颜色数据） 
→ VAO/VBO 创建 → RenderShapes() 执行 → 屏幕显示
```

**SharpGeometry 结构**：
```csharp
struct SharpGeometry
{
    float[] Vertices;              // 顶点数据（3D + RGBA，7 float/顶点）
    PrimitiveType PrimitiveType;   // 渲染原语类型
    int VertexCount;               // 顶点计数
    bool EnableBlend;              // 是否启用 Alpha 混合
    float LineWidth;               // 线宽（LineStrip 使用）
    IList<uint>? Indices;          // 三角形索引（可选）
}
```

## 使用示例

### **添加球体**
```csharp
var sphere = new SphereSharp(
    center: new Vector3(0, 0, 0),
    radius: 1.0f,
    color: Color.FromArgb(150, 100, 150, 200),
    stacks: 20,
    slices: 20,
    drawFill: true,
    drawOutline: false
) { Name = "MySphere" };

PointCloudViewer.Shapes = new List<BaseSharp> { sphere };
```

### **添加圆柱**
```csharp
var cylinder = new CylinderSharp(
    center: new Vector3(0, 0, 0),
    radius: 0.8f,
    height: 2.0f,
    color: Color.FromArgb(150, 200, 100, 100),
    slices: 24,
    drawFill: true,
    drawOutline: false,
    includeCaps: true
) { Name = "MyCylinder" };

PointCloudViewer.Shapes = new List<BaseSharp> { cylinder };
```

### **参数说明**

#### SphereSharp
- `center`: 球心坐标
- `radius`: 球的半径
- `stacks`: 垂直分段数（建议 15-30）
- `slices`: 水平分段数（建议 15-30）
- `drawFill`: 是否填充球面
- `drawOutline`: 是否绘制轮廓线

#### CylinderSharp
- `center`: 圆柱底面中心
- `radius`: 圆柱半径
- `height`: 圆柱高度
- `slices`: 周向分段数（建议 12-32）
- `drawFill`: 是否填充侧面
- `drawOutline`: 是否绘制轮廓线
- `includeCaps`: 是否包含顶底盖面

## 性能特性
- **动态更新**：修改 `Shapes` 依赖属性时自动触发缓冲区重建
- **高效渲染**：所有对象共享单一 ShaderProgram，批量绑定 VAO/VBO
- **选择性渲染**：每种类型独立处理填充、轮廓、线条三种渲染模式
- **Alpha 混合**：自动检测颜色透明度并启用混合
- **线宽支持**：通过 `glLineWidth` 动态调整线条粗细

## 实现细节

### 球体网格生成 (SphereSharpBuilder)
使用球面坐标参数化生成标准 UV 球网格：
- 竖向参数 φ (phi)：从 0 到 π，按 stacks 均分
- 横向参数 θ (theta)：从 0 到 2π，按 slices 均分
- 每个网格单元生成 2 个三角形
- 总顶点数 ≈ (stacks+1) × (slices+1)

### 圆柱网格生成 (CylinderSharpBuilder)
分三部分构建：
1. **侧面**：周向分段，底面和顶面配对生成三角形
2. **底盖**：以底面中心为轮毂的扇形三角形网格
3. **顶盖**：以顶面中心为轮毂的扇形三角形网格
- 总顶点数 ≈ 2 (顶点) + 2×slices (圆周)

## 集成指南

### 添加新形状类型步骤
1. 在 `PointCloudViewer.Sharp.cs` 中定义 `public sealed class MySharp : BaseSharp`
2. 实现 `ISharpRenderBuilder` 的 `MySharpBuilder` 类
3. 在 `InitializeSharpSupport()` 中调用 `RegisterSharpBuilder(new MySharpBuilder())`
4. 在 MainWindow 中添加测试按钮和事件处理器

### 常见问题

**Q: 如何改变对象颜色?**  
A: 创建对象后，修改 `Color` 属性，然后重新赋值给 `PointCloudViewer.Shapes`。

**Q: 如何同时显示多个对象?**  
A: 将多个对象添加到同一个 List 中：
```csharp
PointCloudViewer.Shapes = new List<BaseSharp> { sphere, cylinder, panel };
```

**Q: 为什么透明物体显示有问题?**  
A: 需要从后往前排序（背到腹），或确保 Alpha 值低于 1.0。
