// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), UpdateWindowsSizeBasedOnMinSize.
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private bool initialContentSizeApplied;
    private bool initialContentSizeQueued;
    private double initialContentMinWidth;
    private double initialContentMinHeight;

    private void QueueInitialContentSize()
    {
        if (initialContentSizeApplied || initialContentSizeQueued || host?.IsVisible != true || templateClosed || closed
            || windowContent is not FrameworkElement { IsLoaded: true, ActualWidth: > 0, ActualHeight: > 0 } || captionUpdateQueued)
        {
            return;
        }

        initialContentSizeQueued = DispatcherQueue.TryEnqueue(() =>
        {
            initialContentSizeQueued = false;
            if (initialContentSizeApplied || templateClosed || closed || host?.IsVisible != true || HostedRoot?.IsLoaded != true)
            {
                return;
            }
            // The source scans ContentPresenters in the outer Window template. Its
            // HwndHost content is a separate visual root and is not part of that scan.
            // The native caption presents a title string by default, so map that one
            // presenter back to the selected LayoutContent it represents.
            LayoutContent[] layoutContents = templateView.FindVisualChildren<ContentPresenter>()
                .Select(presenter => ReferenceEquals(presenter, captionTitle) ? captionModel : presenter.Content as LayoutContent)
                .OfType<LayoutContent>().ToArray();
            if (layoutContents.Length == 0)
            {
                return;
            }

            FrameworkElement? first = layoutContents[0].Content is ILayoutContentElement firstWrapper ? firstWrapper.Content
                : layoutContents[0].Content as FrameworkElement;
            if (first == null || !UpdateTotalMargin(first))
            {
                return;
            }

            initialContentSizeApplied = true;
            double horizontal = TotalMargin.Left + TotalMargin.Right;
            double vertical = TotalMargin.Top + TotalMargin.Bottom;
            foreach (FrameworkElement content in layoutContents.Select(model => model.Content is ILayoutContentElement wrapped ? wrapped.Content
                : model.Content as FrameworkElement).OfType<FrameworkElement>())
            {
                initialContentMinWidth = Math.Max(initialContentMinWidth, content.MinWidth);
                initialContentMinHeight = Math.Max(initialContentMinHeight, content.MinHeight);
                ContentMinWidth = Math.Max(ContentMinWidth, content.MinWidth);
                ContentMinHeight = Math.Max(ContentMinHeight, content.MinHeight);
                if (Manager?.AutoWindowSizeWhenOpened != true)
                {
                    continue;
                }

                FrameworkElement? parent = VisualTreeHelper.GetParent(content) as FrameworkElement;
                // Preserve the upstream first-render rules, including its second
                // actual-size clamp; this does not enable ongoing SizeToContent.
                if (content.ActualHeight < content.MinHeight || parent != null && parent.ActualHeight < content.MinHeight)
                {
                    Height = content.MinHeight + vertical;
                }

                if (content.ActualWidth < content.MinWidth || parent != null && parent.ActualWidth < content.MinWidth)
                {
                    Width = content.MinWidth + horizontal;
                }

                if (Height > content.ActualHeight)
                {
                    Height = content.ActualHeight + vertical;
                }

                if (Width > content.ActualWidth)
                {
                    Width = content.ActualWidth + horizontal;
                }
            }
        });
    }
}
