using Windows.Foundation;

namespace AvalonDock.Platforms;

/// <summary>浮动窗口边界与屏幕工作区的坐标适配。</summary>
internal interface IWindowGeometryService
{
    /// <summary>将物理像素边界限制在可用工作区内。</summary>
    Rect KeepVisible(Rect bounds);

    /// <summary>返回布局模型使用的系统 DPI 逻辑坐标虚拟屏幕边界。</summary>
    Rect GetVirtualScreenBounds();
}
