# 第三方代码与资源声明

以下版权与许可原文按上游要求保留。列出的改编文件如在后续重构中删除，应同步更新本声明。

## Microsoft WinUI 控件模板

`sources/WinUI.AvalonDock/Themes/TabControlEx.xaml` 改编了 Microsoft WinUI 的默认 TabView 模板。[固定源码](https://github.com/microsoft/microsoft-ui-xaml/blob/8463f45162149de0ec3ad7df752596893fe3e13e/controls/dev/TabView/TabView.xaml)。

`sources/WinUI.AvalonDock/Themes/MenuItemEx.xaml` 改编了 Microsoft WinUI 的 `DefaultMenuFlyoutItemStyle`。这些改编部分适用下列 MIT 许可，与本仓库主许可分别计算。

MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## .NET WPF 快捷键解析规则

`sources/WinUI.AvalonDock/Controls/ShortcutGestureParser.cs` 改编了 [dotnet/wpf v10.0.0](https://github.com/dotnet/wpf/tree/v10.0.0/src/Microsoft.DotNet.Wpf/src) 的 `KeyGestureConverter`、`KeyGesture`、`KeyConverter` 和 `ModifierKeysConverter` 规则。`ShortcutKeyNames.g.cs` 保存相应按键名称与虚拟键映射；产品不引用 WPF 程序集。

这些改编部分适用下列 MIT 许可。
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## AvalonDock.Themes.WPFUI 外观

`sources/WinUI.AvalonDock/Themes/` 下的停靠资源、窗格和标签模板、自动隐藏、导航器、浮动窗口与覆盖层，以及 `Controls/DockPaneSurface.cs` 和 `Controls/ToggleDockDragOverlay.cs`，改编了 [固定版本的 AvalonDock.Themes.WPFUI](https://github.com/qian-o/AvalonDock.Themes.WPFUI/tree/ffff79aefd2139c34a3a87a0f1059bb036f1a0dc) 的视觉结构、尺寸和部分图形路径。WinUI 原生状态和控件机制替代 WPF 专用的触发器与绘制接口。改编部分适用下列 MIT 许可。

MIT License

Copyright (c) 2024 qian-o

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


## Fluent System Icons 图标路径

浮动窗口、工具标题和文档窗格使用 [Microsoft Fluent System Icons 固定版本](https://github.com/microsoft/fluentui-system-icons/tree/8512d0121f6abd6c8c40f0bc4eb502ccd66ce6e7) 中的最大化、还原、关闭、固定、下拉等图标路径。改编部分适用下列 MIT 许可。

MIT License

Copyright (c) 2020 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

