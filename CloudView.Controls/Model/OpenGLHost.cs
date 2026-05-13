using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CloudView.Controls;

/// <summary>
/// OpenGL 宿主窗口类，负责为 WPF 控件提供原生 Win32 窗口作为 OpenGL 渲染目标。
/// 继承自 HwndHost，将 OpenGL 上下文集成到 WPF 控件树中。
/// </summary>
internal class OpenGLHost : HwndHost
{
    private readonly PointCloudViewer _parent;
    private IntPtr _hwnd;

    /// <summary>
    /// 初始化 OpenGLHost 实例。
    /// </summary>
    /// <param name="parent">关联的 PointCloudViewer 控件。</param>
    /// <exception cref="ArgumentNullException">当 parent 为 null 时抛出。</exception>
    public OpenGLHost(PointCloudViewer parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    /// <summary>
    /// 构建窗口核心。在此方法中创建原生 Win32 窗口并初始化 OpenGL。
    /// </summary>
    /// <param name="hwndParent">父窗口句柄。</param>
    /// <returns>新创建的原生窗口的句柄引用。</returns>
    /// <exception cref="Exception">当窗口创建失败时抛出。</exception>
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwnd = Win32Interop.CreateWindowEx(
            0,
            "static",
            "",
            Win32Interop.WS_CHILD | Win32Interop.WS_VISIBLE | Win32Interop.WS_CLIPSIBLINGS | Win32Interop.WS_CLIPCHILDREN,
            0, 0,
            (int)_parent.ActualWidth,
            (int)_parent.ActualHeight,
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new Exception("Failed to create native window for OpenGL rendering");
        }

        _parent.InitializeOpenGL(_hwnd);
        return new HandleRef(this, _hwnd);
    }

    /// <summary>
    /// 销毁窗口核心。清理 OpenGL 资源并销毁原生窗口。
    /// </summary>
    /// <param name="hwnd">要销毁的窗口句柄引用。</param>
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        _parent.CleanupOpenGL();
        Win32Interop.DestroyWindow(hwnd.Handle);
    }

    /// <summary>
    /// 处理渲染大小变化事件。当窗口大小改变时，通知父控件更新视口。
    /// </summary>
    /// <param name="sizeInfo">大小变化信息。</param>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _parent.OnResize((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
    }
}
