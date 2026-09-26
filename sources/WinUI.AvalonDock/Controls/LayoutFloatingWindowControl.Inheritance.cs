using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace AvalonDock.Controls;

public abstract partial class LayoutFloatingWindowControl
{
    private DockingManager? inheritanceManager;
    private InheritedPresentation? inheritedPresentation;

    public static readonly DependencyProperty FontSizeProperty = Control.FontSizeProperty;
    public double FontSize
    {
        get => templateView.FontSize; set => SetValue(FontSizeProperty, value);
    }
    public static readonly DependencyProperty FontFamilyProperty = Control.FontFamilyProperty;
    public FontFamily FontFamily
    {
        get => templateView.FontFamily; set => SetValue(FontFamilyProperty, value);
    }
    public static readonly DependencyProperty ForegroundProperty = Control.ForegroundProperty;
    public Brush Foreground
    {
        get => templateView.Foreground; set => SetValue(ForegroundProperty, value);
    }
    public static readonly DependencyProperty FontWeightProperty = Control.FontWeightProperty;
    public FontWeight FontWeight
    {
        get => templateView.FontWeight; set => SetValue(FontWeightProperty, value);
    }
    public static readonly DependencyProperty FontStyleProperty = Control.FontStyleProperty;
    public FontStyle FontStyle
    {
        get => templateView.FontStyle; set => SetValue(FontStyleProperty, value);
    }
    public static readonly DependencyProperty FontStretchProperty = Control.FontStretchProperty;
    public FontStretch FontStretch
    {
        get => templateView.FontStretch; set => SetValue(FontStretchProperty, value);
    }
    public static readonly DependencyProperty FlowDirectionProperty = FrameworkElement.FlowDirectionProperty;
    public FlowDirection FlowDirection
    {
        get => templateView.FlowDirection; set => SetValue(FlowDirectionProperty, value);
    }
    public static readonly DependencyProperty LanguageProperty = FrameworkElement.LanguageProperty;
    public string Language
    {
        get => templateView.Language; set => SetValue(LanguageProperty, value);
    }

    internal void RefreshInheritedPresentation()
    {
        if (closed || templateClosed)
        {
            return;
        }

        if (!ReferenceEquals(inheritanceManager, Manager))
        {
            inheritedPresentation?.Dispose();
            inheritanceManager = Manager;
            inheritedPresentation = inheritanceManager == null ? null : new InheritedPresentation(inheritanceManager, templateView, includeContext: false);
        }
        inheritedPresentation?.Refresh();
    }
    private void ReleaseInheritedPresentation()
    {
        inheritedPresentation?.Dispose();
        inheritedPresentation = null;
        inheritanceManager = null;
    }
    private static bool IsInheritedTemplateProperty(DependencyProperty property) => property != FrameworkElement.DataContextProperty
        && InheritedPresentation.Properties.Contains(property);
}
