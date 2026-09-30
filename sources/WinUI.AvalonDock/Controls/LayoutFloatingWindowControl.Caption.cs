using AvalonDock.Layout;
using AvalonDock.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private FrameworkElement? caption;
    private ContentPresenter? captionTitle;
    private DropDownControlArea? captionContext;
    private DockingManager? captionManager;
    private readonly List<(DependencyProperty Property, long Token)> captionTokens = [];
    private WindowCaptionLayout? captionLayout;
    private LayoutContent? captionModel;
    private DataTemplate? captionDataTemplate;
    private DataTemplateSelector? captionSelector;
    private bool updatingCaption;
    private bool captionUpdateQueued;
    private bool referenceCaptionFont;

    private void AttachCaptionTemplate()
    {
        caption = templateView.Part("PART_Caption") as FrameworkElement;
        captionTitle = templateView.Part("PART_CaptionTitle") as ContentPresenter;
        referenceCaptionFont = false;
        captionContext = templateView.Part("PART_CaptionContext") as DropDownControlArea;
        captionModel = null;
        captionDataTemplate = null;
        captionSelector = null;
        captionLayout = null;
        UpdateCaptionPresentation();
    }

    private void UpdateCaptionPresentation()
    {
        if (updatingCaption || templateClosed || closed)
        {
            return;
        }

        updatingCaption = true;
        try
        {
            if (!ReferenceEquals(captionManager, Manager))
            {
                ReleaseCaptionObservers();
                captionManager = Manager;
                if (captionManager != null)
                {
                    foreach (DependencyProperty? property in new[] { DockingManager.DocumentTitleTemplateProperty, DockingManager.DocumentTitleTemplateSelectorProperty,
                        DockingManager.AnchorableTitleTemplateProperty, DockingManager.AnchorableTitleTemplateSelectorProperty, FrameworkElement.FlowDirectionProperty })
                    {
                        captionTokens.Add((property, captionManager.RegisterPropertyChangedCallback(property, (_, _) => UpdateCaptionPresentation())));
                    }
                }
            }
            if (caption == null)
            {
                QueueCaptionLayout();
                return;
            }
            // System caption-button insets are physical left/right. Keep their
            // layout stable while the consumer's title follows text direction.
            caption.FlowDirection = FlowDirection.LeftToRight;
            if (captionTitle != null)
            {
                captionTitle.FlowDirection = captionManager?.FlowDirection ?? FlowDirection.LeftToRight;
            }

            LayoutContent? model = (Model ?? constructorModel) switch
            {
                LayoutDocumentFloatingWindow { IsSinglePane: true } documents => documents.SinglePane?.SelectedContent,
                LayoutAnchorableFloatingWindow { IsSinglePane: true } tools => (tools.SinglePane as ILayoutContentSelector)?.SelectedContent,
                _ => null
            };
            DataTemplate? template = Model is LayoutDocumentFloatingWindow ? captionManager?.DocumentTitleTemplate : captionManager?.AnchorableTitleTemplate;
            DataTemplateSelector? selector = Model is LayoutDocumentFloatingWindow ? captionManager?.DocumentTitleTemplateSelector : captionManager?.AnchorableTitleTemplateSelector;
            bool referenceDefaultTitle = Equals(templateView.Part("PART_CaptionTitle") is ContentPresenter part ? part.Tag : null, "AvalonDock.ReferenceTitle")
                && selector == null && FontSize == 12 && Model is LayoutDocumentFloatingWindow
                && captionManager?.ReadLocalValue(DockingManager.DocumentTitleTemplateProperty) == DependencyProperty.UnsetValue;
            if (captionTitle != null)
            {
                object localFont = captionTitle.ReadLocalValue(ContentPresenter.FontSizeProperty);
                if (referenceCaptionFont && !Equals(localFont, 14d))
                {
                    referenceCaptionFont = false;
                }
                // WPFUI's default title TextBlock uses its 14-unit default when the
                // mirrored WPF Control font is the 12-unit default. Nondefault window
                // fonts still inherit (verified against the original rendered template).
                bool useReferenceDefault = referenceDefaultTitle;
                if (useReferenceDefault && (referenceCaptionFont || localFont == DependencyProperty.UnsetValue))
                {
                    if (captionTitle.FontSize != 14)
                    {
                        captionTitle.FontSize = 14;
                    }

                    referenceCaptionFont = true;
                }
                else if (referenceCaptionFont)
                {
                    captionTitle.ClearValue(ContentPresenter.FontSizeProperty);
                    referenceCaptionFont = false;
                }
            }
            if (captionTitle != null && (!ReferenceEquals(model, captionModel) || !ReferenceEquals(template, captionDataTemplate) || !ReferenceEquals(selector, captionSelector)))
            {
                captionTitle.ContentTemplateSelector = null;
                captionTitle.ClearValue(ContentPresenter.ContentProperty);
                captionTitle.ContentTemplate = template;
                captionTitle.HorizontalContentAlignment = referenceDefaultTitle
                    ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                if (template != null || selector != null)
                {
                    captionTitle.Content = model;
                }
                else if (model != null)
                {
                    captionTitle.SetBinding(ContentPresenter.ContentProperty, new Binding { Source = model, Path = new PropertyPath(nameof(LayoutContent.Title)) });
                }

                captionTitle.ContentTemplateSelector = selector;
                captionTitle.Visibility = model == null ? Visibility.Collapsed : Visibility.Visible;
                captionModel = model;
                captionDataTemplate = template;
                captionSelector = selector;
            }
            if (captionContext != null)
            {
                if (templateView.SingleContentLayoutItem is { } layoutItem)
                {
                    captionContext.DropDownContextMenuDataContext = layoutItem;
                }
                else
                {
                    captionContext.ClearValue(DropDownControlArea.DropDownContextMenuDataContextProperty);
                }

                captionContext.Visibility = model == null ? Visibility.Collapsed : Visibility.Visible;
            }
            VisualStateManager.GoToState(templateView, Model is LayoutAnchorableFloatingWindow { IsSinglePane: true } ? "ToolCaption" : "DocumentCaption", false);
            VisualStateManager.GoToState(templateView, IsMaximized ? "Maximized" : "Restored", false);
            VisualStateManager.GoToState(templateView, Model is LayoutAnchorableFloatingWindow && model is not LayoutAnchorable { CanClose: true } ? "HideTool" : "CloseWindow", false);
            if (host == null || !caption.IsLoaded || caption.XamlRoot == null)
            {
                return;
            }

            WindowCaptionMetrics metrics = host.GetCaptionMetrics();
            bool clientButtons = templateView.Part("PART_MaximizeButton") is Button && templateView.Part("PART_CloseButton") is Button;
            if (!clientButtons && caption.MinHeight != metrics.Height)
            {
                caption.MinHeight = metrics.Height;
            }

            if (captionContext != null)
            {
                Thickness margin = clientButtons ? new Thickness(0) : new Thickness(metrics.LeftInset, 0, metrics.RightInset, 0);
                if (captionContext.Margin != margin)
                {
                    captionContext.Margin = margin;
                }
            }
            Rect region = caption.TransformToVisual(null).TransformBounds(new Rect(0, 0, caption.ActualWidth, caption.ActualHeight));
            Rect[] interactive = InteractiveCaptionControls(caption)
                .Select(element => element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight)))
                .Where(rectangle => rectangle.Width > 0 && rectangle.Height > 0).ToArray();
            WindowCaptionLayout layout = new(region, interactive, captionManager?.ActualTheme == ElementTheme.Dark, clientButtons);
            if (captionLayout?.DragRegion == layout.DragRegion && captionLayout.IsDark == layout.IsDark && captionLayout.HasClientButtons == clientButtons && captionLayout.InteractiveRegions.SequenceEqual(layout.InteractiveRegions))
            {
                return;
            }

            captionLayout = layout;
            QueueCaptionLayout();
        }
        finally { updatingCaption = false; }
    }

    private void QueueCaptionLayout()
    {
        if (captionUpdateQueued || host == null)
        {
            return;
        }

        captionUpdateQueued = DispatcherQueue.TryEnqueue(() =>
        {
            captionUpdateQueued = false;
            if (closed || templateClosed || host == null)
            {
                return;
            }

            host.SetCaptionLayout(captionLayout);
        });
    }

    private static IEnumerable<FrameworkElement> InteractiveCaptionControls(DependencyObject element)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, index);
            if (child is FrameworkElement { Visibility: Visibility.Collapsed })
            {
                continue;
            }

            if (child is Control control && ((control.IsTabStop && control.IsEnabled) || control is ButtonBase))
            {
                yield return control;
            }
            else
            {
                foreach (FrameworkElement descendant in InteractiveCaptionControls(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private void OnCaptionContextRequested(object? sender, WindowCaptionContextRequest request)
    {
        DockingManager? manager = Manager;
        request.ShowSystemMenu = manager?.ShowSystemMenu == true;
        FrameworkElement target = captionContext?.IsLoaded == true ? (FrameworkElement)captionContext : templateView;
        if (!target.IsLoaded || manager == null || templateView.SingleContentLayoutItem is not { } item)
        {
            return;
        }

        MenuFlyout? menu = Model is LayoutDocumentFloatingWindow ? manager.DocumentContextMenu : manager.AnchorableContextMenu;
        if (menu == null)
        {
            return;
        }

        Point local = PlatformServices.Coordinates.ToElementLocal(target, new Point(request.Position.X, request.Position.Y));
        MenuFlyoutContext.Show(menu, target, item, new FlyoutShowOptions { Position = local });
        request.Handled = true;
    }
    private void ReleaseCaptionObservers()
    {
        if (captionManager != null)
        {
            foreach ((DependencyProperty? property, long token) in captionTokens)
            {
                captionManager.UnregisterPropertyChangedCallback(property, token);
            }
        }

        captionTokens.Clear();
        captionManager = null;
    }
}
