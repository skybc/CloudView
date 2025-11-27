# 渲染模块架构设计（v2.0）

## 概述

自 v2.0 版本起，渲染模块采用关注点分离设计，将庞大的 PointCloudViewer 类分解为三个专职类，提高了代码的可维护性、可读性和可测试性。

## 模块结构

### 1. PointCloudViewer（PointCloudViewer.cs）

**职责**：核心渲染控制器与 WPF UI 集成

**主要功能**：
- WPF 控件基类实现和依赖属性定义
- 着色器程序编译与链接管理
- 顶点缓冲区（VAO/VBO）创建和数据管理
- 渲染循环协调（60 FPS DispatcherTimer）
- 摄像机系统（视图/投影矩阵计算）
- 鼠标交互处理（ROI 选择、3D 旋转、缩放）
- 公开 API 提供给客户端代码

**关键字段**：
```csharp
internal IntPtr _hDC;              // 设备上下文
internal IntPtr _hGLRC;            // OpenGL 上下文
internal GL? _gl;                  // Silk.NET OpenGL 实例
internal bool _isInitialized;      // 初始化标志

// 着色器程序 ID
private uint _shaderProgram;       // 3D 点云渲染
private uint _overlayShaderProgram; // ROI 矩形渲染
private uint _textShaderProgram;   // 文本覆盖层渲染

// 缓冲区对象
private uint _vao, _vbo;           // 点云顶点数组和缓冲
private uint _axisVao, _axisVbo;   // 坐标系轴线
private uint _gridVao, _gridVbo;   // XY 平面网格
```

**关键内部方法**：
- `InitializeOpenGL(IntPtr hwnd)`: 初始化 OpenGL 环境
- `CleanupOpenGL()`: 清理 OpenGL 资源
- `OnResize(int width, int height)`: 响应窗口大小变化
- `Render()`: 核心渲染循环
- `UpdatePointCloudBuffer()`: 更新点云数据到 GPU
- `CreateCoordinateAxis()`: 生成坐标系顶点
- `UpdateXYGrid(float gridRange)`: 生成网格顶点
- `ScreenToWorld(Point screenPos, int width, int height)`: 屏幕到世界坐标转换

### 2. OpenGLHost（OpenGLHost.cs）

**职责**：Win32 原生窗口托管与 WPF 集成

**继承**：HwndHost（WPF 提供的原生窗口托管基类）

**主要功能**：
- 创建原生 Win32 静态控制窗口作为 OpenGL 渲染表面
- 与 PointCloudViewer 协作进行初始化和清理
- 处理窗口大小变化事件并同步到 OpenGL 视口

**关键方法**：
```csharp
protected override HandleRef BuildWindowCore(HandleRef hwndParent)
{
    // 创建原生窗口，调用 PointCloudViewer.InitializeOpenGL
}

protected override void DestroyWindowCore(HandleRef hwnd)
{
    // 销毁原生窗口，调用 PointCloudViewer.CleanupOpenGL
}

protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
{
    // 响应大小变化，调用 PointCloudViewer.OnResize
}
```

**使用示例**（内部自动创建）：
```csharp
_glHost = new OpenGLHost(this);
_glHostGrid.Children.Add(_glHost);
```

### 3. Win32Interop（Win32Interop.cs）

**职责**：集中管理 Win32 API 互操作

**特点**：
- 所有 P/Invoke 声明集中在一个静态类中
- 为每个 API 提供详细的 XML 注释
- 包含相关常数和结构体定义
- 易于维护和版本管理

**主要 API 类别**：

#### 设备上下文操作
```csharp
IntPtr GetDC(IntPtr hWnd);           // 获取设备上下文
int ReleaseDC(IntPtr hWnd, IntPtr hDC); // 释放设备上下文
```

#### 像素格式管理
```csharp
int ChoosePixelFormat(IntPtr hDC, ref PIXELFORMATDESCRIPTOR ppfd);
bool SetPixelFormat(IntPtr hDC, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);
bool SwapBuffers(IntPtr hDC);  // 交换前后缓冲区
```

#### OpenGL 上下文管理
```csharp
IntPtr wglCreateContext(IntPtr hDC);
bool wglMakeCurrent(IntPtr hDC, IntPtr hGLRC);
bool wglDeleteContext(IntPtr hGLRC);
IntPtr wglGetProcAddress(string lpszProc);
```

#### 窗口创建
```csharp
IntPtr CreateWindowEx(...);  // 创建扩展窗口
bool DestroyWindow(IntPtr hWnd);  // 销毁窗口
```

**关键常数**：
```csharp
const uint PFD_DRAW_TO_WINDOW = 0x00000004;    // 窗口绘制
const uint PFD_SUPPORT_OPENGL = 0x00000020;    // OpenGL 支持
const uint PFD_DOUBLEBUFFER = 0x00000001;      // 双缓冲
const byte PFD_TYPE_RGBA = 0;                  // RGBA 色彩模式
const byte PFD_MAIN_PLANE = 0;                 // 主平面

const uint WS_CHILD = 0x40000000;              // 子窗口
const uint WS_VISIBLE = 0x10000000;            // 可见
const uint WS_CLIPSIBLINGS = 0x04000000;       // 裁剪兄弟窗口
const uint WS_CLIPCHILDREN = 0x02000000;       // 裁剪子窗口
```

**结构体**：
```csharp
public struct PIXELFORMATDESCRIPTOR
{
    public ushort nSize;      // 结构体大小
    public ushort nVersion;   // 版本号
    public uint dwFlags;      // 标志位
    public byte iPixelType;   // 像素类型
    public byte cColorBits;   // 颜色深度
    // ... 其他字段
}
```

## 代码流程

### 初始化流程

```
WPF Control.OnApplyTemplate()
    ↓
创建 OpenGLHost 实例
    ↓
OpenGLHost.BuildWindowCore()
    ├─ Win32Interop.CreateWindowEx() → 创建原生窗口
    └─ PointCloudViewer.InitializeOpenGL()
        ├─ Win32Interop.GetDC() → 获取设备上下文
        ├─ Win32Interop.ChoosePixelFormat()
        ├─ Win32Interop.SetPixelFormat()
        ├─ Win32Interop.wglCreateContext() → 创建 OpenGL 上下文
        ├─ Win32Interop.wglMakeCurrent() → 激活上下文
        ├─ GL.GetApi() → 初始化 Silk.NET
        ├─ InitializeShaders()
        ├─ InitializeBuffers()
        └─ CreateCoordinateAxisAndGrid()
```

### 渲染流程

```
DispatcherTimer.Tick (每 16ms)
    ↓
PointCloudViewer.Render()
    ├─ Win32Interop.wglMakeCurrent() → 激活上下文
    ├─ _gl.Clear() → 清除颜色和深度缓冲
    ├─ 绘制坐标系轴线 (_axisVao)
    ├─ 更新和绘制网格 (_gridVao)
    ├─ 绘制点云 (_vao)
    ├─ 绘制 ROI 矩形 (if _isDrawingRoi)
    ├─ 绘制文本覆盖层
    └─ Win32Interop.SwapBuffers() → 交换缓冲区
```

### 清理流程

```
WPF Control.OnUnloaded()
    ↓
OpenGLHost.DestroyWindowCore()
    ├─ PointCloudViewer.CleanupOpenGL()
    │   ├─ Win32Interop.wglMakeCurrent()
    │   ├─ _gl.DeleteVertexArray() × 4
    │   ├─ _gl.DeleteBuffer() × 4
    │   ├─ _gl.DeleteProgram() × 3
    │   ├─ Win32Interop.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero)
    │   └─ Win32Interop.wglDeleteContext()
    └─ Win32Interop.DestroyWindow()
```

## 依赖关系

```
┌─────────────────────────────────────┐
│      PointCloudViewer (WPF)         │
│  - 控制流程和业务逻辑              │
│  - 60 FPS 渲染循环                 │
│  - 鼠标交互处理                    │
└──────────┬──────────────────────────┘
           │
           ├─→ OpenGLHost (Win32 宿主)
           │   - 原生窗口创建销毁
           │   - 生命周期管理
           │
           └─→ Win32Interop (API 封装)
               - P/Invoke 声明
               - 常数定义
               - 结构体定义
```

**依赖方向**：
- PointCloudViewer 依赖于 OpenGLHost 和 Win32Interop
- OpenGLHost 依赖于 Win32Interop
- Win32Interop 独立，无其他依赖

## 改进优势

### 1. 代码组织
- 单一职责原则：每个类只负责一个特定方面
- 关注点分离：UI、窗口托管、API 互操作完全分离
- 易于定位：问题快速定位到相应的类

### 2. 可维护性
- Win32 API 变更只需修改 Win32Interop
- OpenGL 初始化逻辑集中在 InitializeOpenGL
- 渲染逻辑独立在 Render 方法中

### 3. 可测试性
- OpenGLHost 可单独测试窗口创建逻辑
- Win32Interop 可 mock 以测试其他模块
- 每个类职责明确，易于单元测试

### 4. 代码复用
- Win32Interop 可被其他项目或模块使用
- OpenGLHost 模式可应用于其他 HwndHost 场景

### 5. 文档性
- 详细的 XML 注释改进智能感知
- 架构文档清晰说明各部分职责

## 向后兼容性

✅ **完全向后兼容**：
- 所有公开 API（属性、方法、事件）保持不变
- 现有客户端代码无需修改
- 仅内部结构优化，外部行为完全相同

## 文件映射

| 文件 | 行数 | 职责 |
|------|------|------|
| Win32Interop.cs | 245 | Win32 API 互操作 |
| OpenGLHost.cs | 75 | 原生窗口托管 |
| PointCloudViewer.cs | 1732 | 渲染控制与逻辑 |
| **总计** | **2052** | **完整渲染系统** |

## 编译验证

✅ **编译成功**：无错误，仅有 3 个未使用字段的 CS0169 警告（无需修复）

**构建命令**：
```
dotnet build --no-restore
```

**输出**：
```
CloudView.Controls -> bin/Debug/net8.0-windows/CloudView.Controls.dll
CloudView -> bin/Debug/net8.0-windows/CloudView.dll

已成功生成。
```
