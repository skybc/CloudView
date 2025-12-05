# CloudView 文档

## 项目概述
CloudView 是一个基于 WPF 的三维点云可视化应用程序，采用 .NET 8 构建。使用 Silk.NET 通过 OpenGL 渲染大型点数据集，支持感兴趣区域（ROI）选择、摄像机操纵，以及空间坐标系和网格显示。

## 模块文档

### [渲染模块 (PointCloudViewer)](./modules/rendering.md)
核心 OpenGL 渲染引擎，负责：
- 三维点云渲染
- 坐标系轴线显示
- XY 平面网格显示
- ROI 区域选择
- 摄像机系统和交互（旋转、缩放、平移）

### [架构设计 (v2.0)](./modules/architecture.md)
渲染模块的架构改进和设计模式：
- Win32Interop 类设计
- OpenGLHost 窗口托管
- PointCloudViewer 控制逻辑
- 模块依赖关系

## 项目结构

```
CloudView/
├── CloudView/               # WPF 主应用
│   ├── App.xaml            # 应用程序资源和启动配置
│   ├── MainWindow.xaml     # 主窗口 UI
│   └── ViewModels/         # MVVM ViewModel
│
├── CloudView.Controls/     # 可重用 WPF 控件库
│   ├── PointCloudViewer.cs # 核心渲染控件（UI 与逻辑编排）
│   ├── OpenGLHost.cs       # Win32 原生窗口托管类（新增）
│   ├── Win32Interop.cs     # Win32 API 互操作封装（新增）
│   ├── PointCloudData.cs   # 数据结构定义
│   └── PointCloudViewer.xaml
│
└── doc/                    # 文档
    ├── readme.md           # 本文件
    └── modules/            # 模块文档
        └── rendering.md    # 渲染模块文档
```

## 技术栈

- **框架**：.NET 8.0
- **UI**：WPF (Windows Presentation Foundation)
- **渲染**：Silk.NET (OpenGL 绑定)
- **工具包**：MVVM Toolkit (源生成器)
- **数学库**：System.Numerics (Vector3, Matrix4x4 等)

## 快速开始

### 1. 在 XAML 中引用控件
```xml
<Window 
    xmlns:controls="clr-namespace:CloudView.Controls;assembly=CloudView.Controls">
    <controls:PointCloudViewer x:Name="viewer" />
</Window>
```

### 2. 加载点云数据
```csharp
var positions = new[] { /* 浮点数数组 */ };
viewer.LoadFromFloatArray(positions, defaultColor: new Vector4(1, 1, 1, 1));
```

### 3. 响应用户交互
```csharp
viewer.RoiSelected += (sender, args) => 
{
    var selectedPoints = args.SelectedPoints;
    // 处理选中的点
};
```

## 关键特性

✅ **高效渲染**：使用 VAO/VBO 管理顶点数据，支持数百万个点的实时渲染  
✅ **空间参考**：内置坐标系轴线和 XY 平面网格  
✅ **灵活交互**：支持旋转、缩放和区域选择  
✅ **可扩展架构**：基于接口的设计，易于扩展新功能  

## 常见问题

### 如何调整网格大小和间距？
修改 `CloudView.Controls/PointCloudViewer.cs` 中 `CreateXYGrid()` 方法内的常数：
- `gridRange`：网格范围
- `gridSpacing`：网格线间距

### 如何修改坐标系轴线长度？
修改 `CreateCoordinateAxis()` 方法中的 `axisLength` 常数。

### 性能优化建议
- 使用 `PointSize` 属性调整点渲染大小
- 对大量数据集使用 `FitToView()` 自动调整视图
- 禁用不需要的功能（`ShowCoordinateAxis=False`, `ShowGrid=False`）

## 许可证
Copyright 2025 - 保留所有权利
