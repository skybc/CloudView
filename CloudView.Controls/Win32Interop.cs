using System.Runtime.InteropServices;

namespace CloudView.Controls;

/// <summary>
/// Win32 API 互操作封装类，提供对 OpenGL 上下文管理所需的原生 Windows API 的访问。
/// 包括设备上下文、像素格式、OpenGL 上下文创建等操作。
/// </summary>
internal static class Win32Interop
{
    #region 设备上下文操作

    /// <summary>
    /// 获取指定窗口的设备上下文。
    /// </summary>
    /// <param name="hWnd">窗口句柄，如果为 IntPtr.Zero 则获取整个屏幕的设备上下文。</param>
    /// <returns>设备上下文句柄，失败返回 IntPtr.Zero。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// 释放设备上下文。
    /// </summary>
    /// <param name="hWnd">与设备上下文关联的窗口句柄。</param>
    /// <param name="hDC">要释放的设备上下文句柄。</param>
    /// <returns>成功返回 1，失败返回 0。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    #endregion

    #region 像素格式操作

    /// <summary>
    /// 根据指定的像素格式描述符选择最适匹配的像素格式。
    /// </summary>
    /// <param name="hDC">设备上下文句柄。</param>
    /// <param name="ppfd">像素格式描述符结构体引用。</param>
    /// <returns>最适配的像素格式索引，失败返回 0。</returns>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern int ChoosePixelFormat(IntPtr hDC, ref PIXELFORMATDESCRIPTOR ppfd);

    /// <summary>
    /// 为指定的设备上下文设置像素格式。
    /// </summary>
    /// <param name="hDC">设备上下文句柄。</param>
    /// <param name="iPixelFormat">要设置的像素格式索引。</param>
    /// <param name="ppfd">像素格式描述符结构体引用。</param>
    /// <returns>成功返回 true，失败返回 false。</returns>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool SetPixelFormat(IntPtr hDC, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);

    /// <summary>
    /// 交换前后缓冲区，显示渲染结果。
    /// </summary>
    /// <param name="hDC">设备上下文句柄。</param>
    /// <returns>成功返回 true，失败返回 false。</returns>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool SwapBuffers(IntPtr hDC);

    #endregion

    #region OpenGL 上下文操作

    /// <summary>
    /// 创建 OpenGL 渲染上下文。
    /// </summary>
    /// <param name="hDC">设备上下文句柄。</param>
    /// <returns>创建的 OpenGL 上下文句柄，失败返回 IntPtr.Zero。</returns>
    [DllImport("opengl32.dll", SetLastError = true)]
    public static extern IntPtr wglCreateContext(IntPtr hDC);

    /// <summary>
    /// 将 OpenGL 上下文与设备上下文关联。
    /// </summary>
    /// <param name="hDC">设备上下文句柄。</param>
    /// <param name="hGLRC">要激活的 OpenGL 上下文句柄。</param>
    /// <returns>成功返回 true，失败返回 false。</returns>
    [DllImport("opengl32.dll", SetLastError = true)]
    public static extern bool wglMakeCurrent(IntPtr hDC, IntPtr hGLRC);

    /// <summary>
    /// 删除 OpenGL 上下文。
    /// </summary>
    /// <param name="hGLRC">要删除的 OpenGL 上下文句柄。</param>
    /// <returns>成功返回 true，失败返回 false。</returns>
    [DllImport("opengl32.dll", SetLastError = true)]
    public static extern bool wglDeleteContext(IntPtr hGLRC);

    /// <summary>
    /// 获取 OpenGL 扩展函数的地址。
    /// </summary>
    /// <param name="lpszProc">函数名称字符串。</param>
    /// <returns>函数地址，不存在返回 IntPtr.Zero。</returns>
    [DllImport("opengl32.dll", SetLastError = true)]
    public static extern IntPtr wglGetProcAddress(string lpszProc);

    #endregion

    #region 模块和函数加载

    /// <summary>
    /// 在指定模块中查找导出函数的地址。
    /// </summary>
    /// <param name="hModule">模块句柄。</param>
    /// <param name="lpProcName">函数名称。</param>
    /// <returns>函数地址，不存在返回 IntPtr.Zero。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    /// <summary>
    /// 将指定的可执行模块加载到调用进程的地址空间中。
    /// </summary>
    /// <param name="lpLibFileName">模块文件名。</param>
    /// <returns>模块句柄，失败返回 IntPtr.Zero。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpLibFileName);

    #endregion

    #region 像素格式描述符结构

    /// <summary>
    /// 像素格式描述符结构体。
    /// 用于描述 OpenGL 窗口的像素格式属性，包括颜色深度、深度缓冲、模板缓冲等。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PIXELFORMATDESCRIPTOR
    {
        /// <summary>
        /// 结构体大小，以字节为单位。
        /// </summary>
        public ushort nSize;

        /// <summary>
        /// 结构体版本号。
        /// </summary>
        public ushort nVersion;

        /// <summary>
        /// 像素格式标志位。
        /// </summary>
        public uint dwFlags;

        /// <summary>
        /// 像素类型：PFD_TYPE_RGBA 或 PFD_TYPE_COLORINDEX。
        /// </summary>
        public byte iPixelType;

        /// <summary>
        /// 颜色缓冲区位深度。
        /// </summary>
        public byte cColorBits;

        /// <summary>
        /// 红色通道位数。
        /// </summary>
        public byte cRedBits;

        /// <summary>
        /// 红色通道位移。
        /// </summary>
        public byte cRedShift;

        /// <summary>
        /// 绿色通道位数。
        /// </summary>
        public byte cGreenBits;

        /// <summary>
        /// 绿色通道位移。
        /// </summary>
        public byte cGreenShift;

        /// <summary>
        /// 蓝色通道位数。
        /// </summary>
        public byte cBlueBits;

        /// <summary>
        /// 蓝色通道位移。
        /// </summary>
        public byte cBlueShift;

        /// <summary>
        /// Alpha 通道位数。
        /// </summary>
        public byte cAlphaBits;

        /// <summary>
        /// Alpha 通道位移。
        /// </summary>
        public byte cAlphaShift;

        /// <summary>
        /// 累积缓冲区位深度。
        /// </summary>
        public byte cAccumBits;

        /// <summary>
        /// 累积缓冲区红色通道位数。
        /// </summary>
        public byte cAccumRedBits;

        /// <summary>
        /// 累积缓冲区绿色通道位数。
        /// </summary>
        public byte cAccumGreenBits;

        /// <summary>
        /// 累积缓冲区蓝色通道位数。
        /// </summary>
        public byte cAccumBlueBits;

        /// <summary>
        /// 累积缓冲区 Alpha 通道位数。
        /// </summary>
        public byte cAccumAlphaBits;

        /// <summary>
        /// 深度缓冲区位数。
        /// </summary>
        public byte cDepthBits;

        /// <summary>
        /// 模板缓冲区位数。
        /// </summary>
        public byte cStencilBits;

        /// <summary>
        /// 辅助缓冲区数量。
        /// </summary>
        public byte cAuxBuffers;

        /// <summary>
        /// 图层类型：PFD_MAIN_PLANE 或 PFD_OVERLAY_PLANE。
        /// </summary>
        public byte iLayerType;

        /// <summary>
        /// 保留字节。
        /// </summary>
        public byte bReserved;

        /// <summary>
        /// 图层掩码。
        /// </summary>
        public uint dwLayerMask;

        /// <summary>
        /// 可见掩码。
        /// </summary>
        public uint dwVisibleMask;

        /// <summary>
        /// 损伤掩码。
        /// </summary>
        public uint dwDamageMask;
    }

    #endregion

    #region 像素格式标志常量

    /// <summary>
    /// 标志位：像素格式用于窗口绘制。
    /// </summary>
    public const uint PFD_DRAW_TO_WINDOW = 0x00000004;

    /// <summary>
    /// 标志位：像素格式支持 OpenGL。
    /// </summary>
    public const uint PFD_SUPPORT_OPENGL = 0x00000020;

    /// <summary>
    /// 标志位：启用双缓冲。
    /// </summary>
    public const uint PFD_DOUBLEBUFFER = 0x00000001;

    /// <summary>
    /// 像素类型：RGBA 颜色模式。
    /// </summary>
    public const byte PFD_TYPE_RGBA = 0;

    /// <summary>
    /// 图层类型：主平面。
    /// </summary>
    public const byte PFD_MAIN_PLANE = 0;

    #endregion

    #region 窗口样式常量

    /// <summary>
    /// 窗口样式：子窗口。
    /// </summary>
    public const uint WS_CHILD = 0x40000000;

    /// <summary>
    /// 窗口样式：窗口可见。
    /// </summary>
    public const uint WS_VISIBLE = 0x10000000;

    /// <summary>
    /// 窗口样式：裁剪相邻的兄弟窗口。
    /// </summary>
    public const uint WS_CLIPSIBLINGS = 0x04000000;

    /// <summary>
    /// 窗口样式：裁剪子窗口相对于父窗口的区域。
    /// </summary>
    public const uint WS_CLIPCHILDREN = 0x02000000;

    /// <summary>
    /// 创建窗口的扩展样式标志。
    /// </summary>
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    #endregion

    #region 窗口创建

    /// <summary>
    /// 创建扩展窗口。
    /// </summary>
    /// <param name="dwExStyle">扩展窗口样式。</param>
    /// <param name="lpClassName">窗口类名。</param>
    /// <param name="lpWindowName">窗口标题。</param>
    /// <param name="dwStyle">窗口样式。</param>
    /// <param name="x">窗口左上角 X 坐标。</param>
    /// <param name="y">窗口左上角 Y 坐标。</param>
    /// <param name="nWidth">窗口宽度（像素）。</param>
    /// <param name="nHeight">窗口高度（像素）。</param>
    /// <param name="hWndParent">父窗口句柄。</param>
    /// <param name="hMenu">菜单或子窗口标识符句柄。</param>
    /// <param name="hInstance">应用程序实例句柄。</param>
    /// <param name="lpParam">指向创建数据的指针。</param>
    /// <returns>创建的窗口句柄，失败返回 IntPtr.Zero。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    /// <summary>
    /// 销毁窗口。
    /// </summary>
    /// <param name="hWnd">要销毁的窗口句柄。</param>
    /// <returns>成功返回 true，失败返回 false。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    #endregion
}
