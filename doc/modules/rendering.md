# 渲染模块 (PointCloudViewer)

## 概述
PointCloudViewer 是 CloudView.Controls 项目中的核心渲染控件，负责通过 OpenGL 和 Silk.NET 实现三维点云的实时可视化。

## 架构设计

### 模块分离
自版本 2.0 起，渲染模块采用关注点分离设计，分为以下三个核心类：

#### 1. **PointCloudViewer** (PointCloudViewer.cs)
- **职责**：WPF 控件类，处理 UI 集成、数据绑定、事件管理和渲染逻辑编排
- **主要功能**：
  - 依赖属性定义（数据绑定接口）
  - 着色器编译与链接
  - 缓冲区和顶点数组管理
  - 渲染循环协调（60 FPS 定时器）
  - 摄像机控制与矩阵计算
  - 鼠标交互处理（ROI 选择、旋转、缩放）
  - 公共 API 提供
- **关键字段**：
  - `_gl`: Silk.NET OpenGL 实例
  - `_hDC`, `_hGLRC`: 设备上下文和 OpenGL 上下文句柄
  - `_isInitialized`: 初始化标志
  - 着色器程序 ID：`_shaderProgram`, `_overlayShaderProgram`, `_textShaderProgram`
  - 缓冲区对象：`_vao`, `_vbo`（点云）、`_axisVao`, `_axisVbo`（坐标系）等

#### 2. **OpenGLHost** (OpenGLHost.cs) - 新增
- **职责**：Win32 原生窗口的 HwndHost 包装器，将 OpenGL 渲染目标集成到 WPF 控件树
- **主要功能**：
  - 创建原生 Win32 静态控制窗口作为 OpenGL 绘图表面
  - 与 PointCloudViewer 协作进行初始化和清理
  - 处理窗口大小变化事件并同步到 OpenGL 视口
- **关键方法**：
  - `BuildWindowCore()`: 创建原生窗口并调用 PointCloudViewer.InitializeOpenGL
  - `DestroyWindowCore()`: 销毁原生窗口并调用 PointCloudViewer.CleanupOpenGL
  - `OnRenderSizeChanged()`: 响应窗口大小变化

#### 3. **Win32Interop** (Win32Interop.cs) - 新增
- **职责**：集中管理所有 Win32 API P/Invoke 声明和相关常数
- **主要功能**：
  - 设备上下文操作：GetDC, ReleaseDC
  - 像素格式管理：ChoosePixelFormat, SetPixelFormat
  - OpenGL 上下文管理：wglCreateContext, wglMakeCurrent, wglDeleteContext, wglGetProcAddress
  - 窗口创建销毁：CreateWindowEx, DestroyWindow
  - 模块加载：LoadLibrary, GetProcAddress
- **关键常数**：
  - 像素格式标志：PFD_DRAW_TO_WINDOW, PFD_SUPPORT_OPENGL, PFD_DOUBLEBUFFER
  - 窗口样式：WS_CHILD, WS_VISIBLE, WS_CLIPSIBLINGS, WS_CLIPCHILDREN
  - 像素格式描述符结构体 (PIXELFORMATDESCRIPTOR)

#### 4. **PointCloudViewer 数据管理** ✨ 新增
- **_pointCloudCenter**：存储计算出的点云中心坐标
- **_cameraTarget**：摄像机看向的目标点，即视野中心
- **_rotationX, _rotationY**：摄像机绕目标点的旋转角度

### 架构优势
1. **关注点分离**：每个类职责明确，易于维护和测试
2. **可复用性**：Win32Interop 可被其他项目或组件使用
3. **可扩展性**：新的窗口托管方式可轻松扩展 OpenGLHost
4. **清晰的依赖关系**：PointCloudViewer ← OpenGLHost + Win32Interop

## 主要功能

### 1. 点云渲染
- 使用 VAO/VBO 管理大型点数据集
- 顶点数据格式：3 个浮点数（位置）+ 4 个浮点数（RGBA 颜色）
- 支持动态更新点云数据
- 可配置的点大小

### 1.1 对象渲染（BaseSharp） ✨ 新增
- 依赖属性 `Shapes`：`IList<BaseSharp>`，默认 null
- `BaseSharp` 公共属性：`Id`、`Name`
- 已内置类型：
  - `PanelSharp`（面片）：使用顶点顺序绘制 TriangleFan，支持填充和轮廓渲染
  - `LineSharp`（线条）：连接顶点的折线，支持闭合，可配置线宽
  - `VolumeSharp`（体积）：三角形索引定义的多面体，支持填充和轮廓
- 渲染管线：`Shapes` 更新 → 构建对应 VAO/VBO → 在主着色器下绘制（位置+颜色布局复用点云管线）
- 构建器模式：每个派生类型需提供 `ISharpRenderBuilder`，已注册 `PanelSharpBuilder`、`LineSharpBuilder`、`VolumeSharpBuilder`，后续类型可通过注册新 builder 扩展
- 渲染选项：
  - `PanelSharp.DrawFill`：是否绘制填充面（默认 true）
  - `PanelSharp.DrawOutline`：是否绘制边框轮廓（默认 false）
  - `PanelSharp.LineWidth`：轮廓线宽度
  - `LineSharp.LineWidth`：线条宽度（默认 2.0）
  - `LineSharp.IsClosed`：是否闭合线条
  - `VolumeSharp.DrawFill`、`VolumeSharp.DrawOutline`、`VolumeSharp.LineWidth`：同面片
  - `VolumeSharp.Indices`：三角形索引列表，每三个索引定义一个三角形

### 2. 坐标系显示 ✨ 新增
显示三维空间中点云中心处的坐标系轴线：
- **X 轴**：红色，长度 1.0，从点云中心出发
- **Y 轴**：绿色，长度 1.0，从点云中心出发
- **Z 轴**：蓝色，长度 1.0，从点云中心出发

**依赖属性**：`ShowCoordinateAxis` (默认: true)

**实现细节**：
- 在 `CreateCoordinateAxis()` 方法中生成 6 个顶点（3 条线段）
- 坐标轴的中心点为 `_pointCloudCenter`，即点云的 AABB 中心
- 使用线段模式 (GL_LINES) 绘制
- 线宽设置为 2.0，提高可见性
- 当点云数据更新时，自动重新创建坐标轴以适应新的中心位置

### 3. XY 平面网格 ✨ 新增
显示 XY 平面（平行于点云中心的 XY 平面）的网格，用于空间参考：
- **范围**：动态调整，始终覆盖显示区域，以点云中心为中心对称展开
- **间距**：0.1 单位（固定）
- **颜色**：浅灰色 (RGB: 0.7, 0.7, 0.7)
- **透明度**：30%
- **Z 坐标**：等于点云中心的 Z 坐标

**依赖属性**：`ShowGrid` (默认: true)

**实现细节**：
- 在 `UpdateXYGrid()` 方法中根据摄像机缩放参数 `_zoom` 动态生成网格顶点
- 网格范围公式：`gridRange = _zoom * 0.5f`
- 网格中心为 `_pointCloudCenter`，网格范围在 X、Y 方向上以中心对称展开
- 每帧在 Render() 中重新生成，确保网格始终覆盖整个显示区域
- X 方向线（平行于 X 轴）和 Y 方向线（平行于 Y 轴）
- 使用 alpha 混合实现透明效果
- 启用 GL_BLEND 并设置混合函数为 SrcAlpha + OneMinusSrcAlpha
- 使用 DynamicDraw 缓冲用法以支持频繁更新

### 4. ROI（感兴趣区域）选择
- 支持左鼠标拖动进行矩形选择
- 可自定义 ROI 填充颜色和边框颜色
- 触发 RoiSelected 事件，返回选中的点索引和点集合

### 5. 摄像机系统
- 支持 3D 旋转（右鼠标拖动），围绕视野中心旋转
- 支持缩放（鼠标滚轮）
- 支持平移（中鼠标拖动），左右上下移动视野 ✨ 新增
- 视图/投影矩阵自动管理
- 支持自动调整视图以适应所有点 (FitToView)
- **自动视野中心定位**：Points 更新后，自动计算点云中心并将视野中心设置到该位置

**摄像机中心系统** ✨ 新增
- 当 Points 数据更新时，系统自动计算点云中心（基于所有点的 X、Y、Z 坐标的最大最小值）
- 将 `_cameraTarget` 设置为计算出的点云中心
- 摄像机旋转围绕 `_cameraTarget` 进行，实现以点云中心为旋转轴的交互体验
- 重置摄像机旋转角度为 0，使摄像机沿 Z 轴方向观看

**摄像机平移系统** ✨ 新增
- 中键拖动实现视野平移，左右上下移动
- 平移速度与当前缩放（摄像机到目标点的距离）成正比，提供自然的交互体验
- 平移后，摄像机目标点（缩放中心）也会随之移动
- 平移过程中保持摄像机到目标点的距离不变，确保视野不会出现意外变化

**平移实现细节**：
- `_isPanning`: 标志中键是否被按下
- `_panOffset`: 记录累积的平移偏移量（用于重置视图等操作的参考）
- 平移公式：根据鼠标移动距离、缩放距离和摄像机坐标系计算平移向量
- 摄像机坐标系使用三个基向量计算：
  - `forward`: 摄像机看向方向
  - `right`: 摄像机右向量（用于左右平移）
  - `up`: 摄像机向上向量（用于上下平移）
- 平移速度系数：`panSpeed = _zoom * 0.01f`，可在 `OnMouseMove()` 中调整

**实现细节**：
- `CalculatePointCloudCenter()` 方法：遍历所有点，计算 AABB 的中心点
- `UpdateCameraPositionWithRotation()` 方法：根据旋转角度更新摄像机位置，保持相对于目标点的距离不变
- 旋转通过应用旋转矩阵到初始方向向量 (0, 0, distance) 来实现
- 摄像机位置 = 目标点 + 旋转后的方向向量

### 6. 坐标范围限制 ✨ 新增
支持通过设定坐标范围限制点云的显示范围，仅显示在指定范围内的点：
- **默认范围**：X、Y、Z 轴的最小值为 `float.MinValue`，最大值为 `float.MaxValue`（无限制）
- **范围外处理**：超出指定范围的点不会被渲染，也不会参与点云中心计算

**依赖属性**：
- `MinX`, `MaxX`: X 轴坐标范围
- `MinY`, `MaxY`: Y 轴坐标范围
- `MinZ`, `MaxZ`: Z 轴坐标范围

**功能说明**：
- 在 `UpdatePointCloudBuffer()` 中，只有在指定坐标范围内的点才会被添加到顶点缓冲中
- 在 `CalculatePointCloudCenter()` 中，只有在指定坐标范围内的点才会参与中心点计算
- 范围限制对摄像机焦点和旋转中心的计算也有影响
- 当没有任何点在指定范围内时，摄像机焦点默认回到原点 (0, 0, 0)

**使用示例**：
```csharp
// 设置显示范围，仅显示 X: [-10, 10], Y: [-20, 20], Z: [-5, 5] 范围内的点
viewer.MinX = -10;
viewer.MaxX = 10;
viewer.MinY = -20;
viewer.MaxY = 20;
viewer.MinZ = -5;
viewer.MaxZ = 5;
```

### 7. 着色器管理
- 顶点着色器：处理位置和颜色的变换
- 片段着色器：输出点的颜色
- 使用 uniform 矩阵传递模型、视图和投影变换

### 7. 文本覆盖层渲染
在右上角显示鼠标世界坐标信息（X、Y、Z，单位 mm）：
- **固定像素大小**：文字大小不随窗口缩放变化
- **字体大小**：16 像素（可配置）
- **颜色**：绿色 (Lime)

**实现细节**：
- 使用正交投影矩阵 (Orthographic Projection) 渲染文本
- 顶点坐标使用像素坐标而非 NDC 坐标
- 正交投影矩阵通过 `Matrix4x4.CreateOrthographicOffCenter(0, windowWidth, windowHeight, 0, -1, 1)` 创建
- 文本通过 WPF `FormattedText` 生成纹理，再使用 OpenGL 绘制
- 着色器通过 `uProjection` uniform 接收正交投影矩阵

## 渲染流程

```
OnPointsChanged() → UpdatePointCloudBuffer() → CalculatePointCloudCenter() → 
  设置 _cameraTarget = 点云中心, 重置旋转
  ↓
Render() → Clear背景 → 设置矩阵和着色器 → 
  ├─ 绘制坐标系 (if ShowCoordinateAxis)
  ├─ 绘制网格 (if ShowGrid)
  ├─ 绘制点云
  ├─ 绘制ROI矩形 (if 正在绘制)
  └─ 绘制文本覆盖层 (鼠标世界坐标)
  → SwapBuffers

鼠标右键拖动旋转：
  OnMouseRightButtonDown() → _isRotating=true
    ↓
  OnMouseMove() → 更新 _rotationX/_rotationY → UpdateCameraPositionWithRotation() → Render()
    ↓
  OnMouseRightButtonUp() → _isRotating=false

鼠标中键拖动平移：
  OnPreviewMouseDown(MiddleButton) → OnMouseMiddleButtonDown() → _isPanning=true
    ↓
  OnMouseMove() → 计算平移向量 → 更新 _cameraTarget/_cameraPosition/panOffset → Render()
    ↓
  OnPreviewMouseUp(MiddleButton) → OnMouseMiddleButtonUp() → _isPanning=false

鼠标滚轮缩放：
  OnMouseWheel() → 更新 _zoom → 保持摄像机方向，仅改变距离 → Render()
```

## 公共 API

### 依赖属性
- `Points`: 点云数据列表
- `PointSize`: 点大小（默认: 3.0f）
- `BackgroundColor`: 背景颜色
- `ShowCoordinateAxis`: 是否显示坐标系（默认: true）
- `ShowGrid`: 是否显示网格（默认: true）
- `RoiColor`: ROI 区域填充颜色
- `RoiBorderColor`: ROI 区域边框颜色
- `SelectedPoints`: 当前选中的点集合
- `SelectedIndices`: 当前选中的点索引列表
- `MinX`, `MaxX`: X 轴坐标显示范围（默认: float.MinValue ~ float.MaxValue）
- `MinY`, `MaxY`: Y 轴坐标显示范围（默认: float.MinValue ~ float.MaxValue）
- `MinZ`, `MaxZ`: Z 轴坐标显示范围（默认: float.MinValue ~ float.MaxValue）

### 公共方法
- `LoadFromFloatArray()`: 从浮点数组加载点云
- `LoadFromVector3List()`: 从 Vector3 列表加载点云
- `ResetView()`: 重置相机视图
- `FitToView()`: 自动调整视图以适应所有点

### 私有方法 ✨ 新增
- `CalculatePointCloudCenter()`: 计算并返回点云中心坐标，在 Points 更新时调用
- `UpdateCameraPositionWithRotation()`: 根据旋转角度更新摄像机位置，使摄像机围绕目标点旋转

### 事件
- `RoiSelected`: ROI 选择完成时触发

## 技术细节

### Win32 互操作
使用 P/Invoke 与 Win32 API 交互以管理 OpenGL 上下文：
- `GetDC()`: 获取设备上下文
- `wglCreateContext()`: 创建 OpenGL 上下文
- `wglMakeCurrent()`: 激活 OpenGL 上下文
- `SwapBuffers()`: 交换前后缓冲区

### 60 FPS 渲染循环
使用 DispatcherTimer 在 UI 线程上调度渲染，时间间隔 16ms

### 内存管理
- 动态 VBO 用于点云数据（支持实时更新）
- 动态 VBO 用于网格数据（每帧更新以适应缩放）
- 静态 VBO 用于坐标系（一次性生成）
- 正确的资源清理在 CleanupOpenGL() 中实现

## 使用示例

```csharp
// 在 XAML 中使用
<Controls:PointCloudViewer 
    x:Name="viewer"
    Points="{Binding PointData}"
    PointSize="2.5"
    ShowCoordinateAxis="True"
    ShowGrid="True"
    RoiSelected="Viewer_RoiSelected"/>

// 在代码中加载数据
var positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, /* ... */ };
viewer.LoadFromFloatArray(positions, new Vector4(1, 1, 1, 1));

// 订阅 ROI 选择事件
viewer.RoiSelected += (sender, args) => 
{
    Console.WriteLine($"选中 {args.SelectedPoints.Count} 个点");
};
```

## 已知限制
- 坐标系轴线长度固定为 1.0，可在 `CreateCoordinateAxis()` 中修改
- 网格线间距固定为 0.1 单位，可在 `UpdateXYGrid()` 方法中调整 `gridSpacing` 常数
- 不支持 OpenGL 版本低于 3.3 的硬件

## 最近更新 ✨

### v2.6 - 摄像机平移功能
- **功能新增**：
  - 实现中键拖动进行视野平移
  - 支持左右、上下任意方向平移
  - 平移后，缩放中心点（摄像机目标点）也相应移动

- **实现细节**：
  - 新增 `_isPanning` 标志：表示中键是否被按下
  - 新增 `_panOffset` 字段：记录累积的平移偏移量
  - 新增 `OnMouseMiddleButtonDown()` 和 `OnMouseMiddleButtonUp()` 方法：处理中键按下和释放
  - 新增 `OnPreviewMouseDown()` 和 `OnPreviewMouseUp()` 方法：在预览阶段捕获中键事件
  - 修改 `OnMouseMove()` 方法：添加平移处理逻辑
  - 修改 `OnApplyTemplate()` 方法：注册预览鼠标事件处理器
  - 修改 `Dispose()` 和 `OnUnloaded()` 方法：注销预览鼠标事件处理器

- **平移算法**：
  1. 获取摄像机的三个基向量（forward、right、up）
  2. 计算平移速度系数：`panSpeed = _zoom * 0.01f`
  3. 根据鼠标移动距离计算平移向量：`panDelta = right * deltaX * panSpeed + up * deltaY * panSpeed`
  4. 更新摄像机目标点：`_cameraTarget += panDelta`
  5. 保持摄像机到目标点的距离：重新计算摄像机位置以维持相同距离

- **交互流程**：
  ```
  OnPreviewMouseDown(MiddleButton) → OnMouseMiddleButtonDown → _isPanning=true
    ↓
  OnMouseMove → 计算平移向量 → 更新 _cameraTarget 和 _cameraPosition → Render()
    ↓
  OnPreviewMouseUp(MiddleButton) → OnMouseMiddleButtonUp → _isPanning=false
  ```

- **效果**：
  - 用户按住中键，拖动鼠标可以在屏幕空间中任意平移视野
  - 平移不会改变视角旋转状态，只改变视野中心
  - 平移不会改变缩放距离，保持一致的视野大小

### v2.5 - 大规模点云渲染性能优化
- **性能问题**：
  - 千万级点云渲染卡顿
  - GPU 使用率过高
  - 鼠标交互响应迟缓

- **优化方案**：

#### 1. LOD（Level of Detail）点云抽稀
- **阈值**：点数超过 100,000 时启用 LOD
- **最大显示点数**：2,000,000 点
- **抽稀算法**：均匀采样，根据点数自动计算采样步长
  ```
  stride = (totalPoints + MaxDisplayPoints - 1) / MaxDisplayPoints
  ```
- **效果**：千万级点云自动抽稀到 200 万点显示

#### 2. 脏标记渲染机制
- **原问题**：鼠标移动、滚轮缩放等操作直接调用 Render()，导致过度渲染
- **优化**：引入 `_needsRender` 脏标记
  - 交互操作只设置脏标记
  - DispatcherTimer（16ms/60FPS）检查脏标记，仅在需要时渲染
- **效果**：减少 50%+ 的无效渲染调用

#### 3. 网格缓存优化
- **原问题**：`UpdateXYGrid()` 每帧调用，每帧重建 VBO
- **优化**：仅在缩放变化（`_zoom` 变化超过 0.01）时更新网格
- **实现**：`_lastGridZoom` 和 `_gridNeedsUpdate` 字段控制更新时机

#### 4. 文本纹理缓存
- **原问题**：每帧创建 3 个纹理（X/Y/Z 坐标），每帧删除
- **优化**：
  - 合并 3 行文本为 1 个纹理
  - 缓存纹理，仅在内容变化时重新生成
- **实现**：`_cachedTextTexture`、`_cachedTextContent` 缓存机制

#### 5. 点云缓冲优化
- **原问题**：使用 `List<float>` 动态添加，再转数组
- **优化**：
  - 预计算数组大小，一次性分配
  - 使用 `StaticDraw` 替代 `DynamicDraw`（数据更新频率低）

- **受影响的字段**：
  - 新增 `_needsRender`：脏标记
  - 新增 `_lastGridZoom`、`_gridNeedsUpdate`：网格缓存
  - 新增 `_cachedTextTexture`、`_cachedTextContent`：文本缓存
  - 新增 `_currentLodLevel`、`_useLod`：LOD 控制
  - 新增常量 `LodThreshold`、`MaxDisplayPoints`：LOD 阈值

- **受影响的方法**：
  - `UpdatePointCloudBuffer()`: 添加 LOD 抽稀逻辑
  - `Render()`: 添加网格缓存检查
  - `OnMouseMove()`: 使用脏标记替代直接渲染
  - `OnMouseWheel()`: 使用脏标记替代直接渲染
  - `OnRenderTimerTick()`: 新增，处理脏标记和渲染
  - `DrawCachedText()`: 新增，带缓存的文本渲染
  - `CreateMultiLineTextTexture()`: 新增，创建多行文本纹理

### v2.4 - 坐标范围限制功能
- **功能新增**：
  - 实现坐标范围限制，支持仅显示指定坐标范围内的点
  - 超出范围的点不被渲染，也不参与中心点计算

- **实现细节**：
  - 新增 6 个依赖属性：`MinX`, `MaxX`, `MinY`, `MaxY`, `MinZ`, `MaxZ`
  - 默认值为 `float.MinValue` 和 `float.MaxValue`，表示无限制
  - 修改 `UpdatePointCloudBuffer()` 方法，在构建顶点缓冲时过滤出范围内的点
  - 修改 `CalculatePointCloudCenter()` 方法，在计算中心点时仅考虑范围内的点
  - 依赖属性变化时自动触发 `OnPointsChanged` 回调，重新更新缓冲和中心点

- **受影响的方法**：
  - `UpdatePointCloudBuffer()`: 添加坐标范围检查，过滤点数据
  - `CalculatePointCloudCenter()`: 添加坐标范围检查，过滤点数据用于中心计算

### v2.3 - 坐标系和网格中心对齐改进
- **功能改进**：
  - 坐标轴中心点从原点 (0,0,0) 移至点云中心 (`_pointCloudCenter`)
  - XY 平面网格中心从原点 (0,0,0) 移至点云中心 (`_pointCloudCenter`)
  - 网格、坐标轴和摄像机中心完全对齐，提供一致的空间参考

- **实现细节**：
  - 修改 `CreateCoordinateAxis()` 方法，使三条轴线从 `_pointCloudCenter` 出发
  - 修改 `UpdateXYGrid()` 方法，使网格以 `_pointCloudCenter` 为中心对称展开
  - 在 `OnPointsChanged()` 中添加重新创建坐标轴的调用，确保每次点云更新时轴线位置同步更新

- **受影响的方法**：
  - `CreateCoordinateAxis()`: 使用 `_pointCloudCenter` 作为坐标轴原点
  - `UpdateXYGrid()`: 使用 `_pointCloudCenter` 作为网格中心
  - `OnPointsChanged()`: 添加 `CreateCoordinateAxis()` 调用以同步更新坐标轴位置

### v2.2 - 摄像机缩放与旋转交互修复
- **问题修复**：
  - 修复右键旋转后使用滚轮缩放时视角跳动的问题
  - 修复滚轮缩放后使用右键旋转时视角跳动的问题
  - 修复缩放操作导致图像意外旋转的问题

- **问题原因分析**：
  - 原 `OnMouseWheel()` 方法直接将摄像机位置设置为 `(0, 0, _zoom)`
  - 这会忽略当前的旋转状态和目标点位置
  - 导致缩放时摄像机被强制重置到 Z 轴上，与旋转状态不一致

- **解决方案**：
  - 修改 `OnMouseWheel()` 方法，在缩放时保持当前的摄像机方向
  - 获取摄像机到目标点的当前方向向量并归一化
  - 使用归一化方向向量乘以新的距离值更新摄像机位置
  - 确保缩放操作只改变距离，不改变观察方向

- **受影响的方法**：
  - `OnMouseWheel()`: 重写缩放逻辑，保持摄像机观察方向不变

### v2.1 - 点云自动中心定位与旋转中心改进
- **功能新增**：
  - 实现点云自动中心定位：Points 更新后自动计算点云中心
  - 摄像机旋转中心设置为视野中心（点云中心）
  - 改进摄像机交互体验，旋转更加直观

- **实现细节**：
  - 新增 `_pointCloudCenter` 字段存储计算出的点云中心
  - 新增 `CalculatePointCloudCenter()` 方法计算点云的 AABB 中心
  - 新增 `UpdateCameraPositionWithRotation()` 方法实现围绕目标点的旋转
  - 修改 `OnPointsChanged()` 回调在数据更新时自动调整视野中心和重置旋转
  - 移除模型矩阵中的旋转变换，改为在摄像机位置计算中应用旋转

- **受影响的方法**：
  - `OnPointsChanged()`: 添加自动中心计算和摄像机目标设置
  - `OnMouseMove()`: 添加 `UpdateCameraPositionWithRotation()` 调用实现围绕中心旋转
  - `Render()`: 移除模型矩阵旋转，使用恒等矩阵
  - `OnMouseLeftButtonUp()`: 更新矩阵计算，使用恒等模型矩阵
  - `ScreenToWorld()`: 更新矩阵计算，使用恒等模型矩阵
  - `ResetView()`: 添加 `_cameraTarget` 重置

## 已知限制

## 常见问题

### 如何调整网格覆盖范围？
修改 `Render()` 方法中的网格范围计算公式：
```csharp
float gridRange = _zoom * 0.5f;  // 调整系数来改变覆盖范围
```
- 增大系数 (例如 0.6f) 可以扩大网格范围
- 减小系数 (例如 0.3f) 可以缩小网格范围

### 如何调整网格线间距？
修改 `UpdateXYGrid()` 方法中的 `gridSpacing` 常数：
```csharp
const float gridSpacing = 0.1f;  // 改变此值调整网格线间距
```

## 资源管理

### IDisposable 模式
`PointCloudViewer` 实现 `IDisposable` 接口，确保资源安全释放：

```csharp
public partial class PointCloudViewer : Control, IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { ... }
    
    ~PointCloudViewer() { Dispose(false); }
}
```

### 资源清理清单
`CleanupOpenGL()` 方法负责清理以下资源：

| 资源类型 | 字段名 | 释放方法 |
|---------|-------|---------|
| 点云 VAO/VBO | `_vao`, `_vbo` | `DeleteVertexArray`, `DeleteBuffer` |
| 坐标轴 VAO/VBO | `_axisVao`, `_axisVbo` | `DeleteVertexArray`, `DeleteBuffer` |
| 网格 VAO/VBO | `_gridVao`, `_gridVbo` | `DeleteVertexArray`, `DeleteBuffer` |
| ROI VAO/VBO | `_roiVao`, `_roiVbo` | `DeleteVertexArray`, `DeleteBuffer` |
| 覆盖层 VAO/VBO | `_overlayVao`, `_overlayVbo` | `DeleteVertexArray`, `DeleteBuffer` |
| 文本 VAO/VBO | `_textVao`, `_textVbo` | `DeleteVertexArray`, `DeleteBuffer` |
| 主着色器 | `_shaderProgram` | `DeleteProgram` |
| 覆盖层着色器 | `_overlayShaderProgram` | `DeleteProgram` |
| 文本着色器 | `_textShaderProgram` | `DeleteProgram` |
| 文本纹理 | `_cachedTextTexture` | `DeleteTexture` |
| OpenGL 上下文 | `_hGLRC` | `wglDeleteContext` |
| 设备上下文 | `_hDC` | `ReleaseDC` |
| OpenGL 库句柄 | `_opengl32Handle` | `FreeLibrary` |

### 事件处理器解绑
在 `OnUnloaded()` 和 `Dispose()` 中解绑所有鼠标事件处理器：
- `MouseLeftButtonDown`
- `MouseMove`
- `MouseLeftButtonUp`
- `MouseRightButtonDown`
- `MouseRightButtonUp`
- `MouseWheel`

### 资源管理最佳实践
1. **检查后删除**：所有 OpenGL 资源删除前检查句柄是否为 0
2. **删除后置零**：资源删除后立即将句柄设为 0，防止重复删除
3. **句柄缓存**：`opengl32.dll` 句柄缓存在 `_opengl32Handle`，避免重复加载
4. **窗口句柄保存**：保存 `_hwnd` 以便正确调用 `ReleaseDC`
