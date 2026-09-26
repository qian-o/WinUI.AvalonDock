// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/Serialization/LayoutDtoMapper.cs
using System;
using System.Globalization;
using AvalonDock.Core.Serialization;
using AvalonDock.Core.Serialization.Dto;
using AvalonDock.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvalonDock.Serialization;

/// <summary>
/// Maps between the WinUI layout tree and the upstream serialization DTOs.
/// </summary>
public class LayoutDtoMapper : ILayoutDtoMapper
{

    /// <inheritdoc/>
    public LayoutRootDto ToDto(ISerializableLayoutRoot layout)
    {
        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        return MapRootToDto((LayoutRoot)layout);
    }

    /// <inheritdoc/>
    public ISerializableLayoutRoot FromDto(LayoutRootDto dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        return MapDtoToRoot(dto);
    }

    private LayoutRootDto MapRootToDto(LayoutRoot root)
    {
        LayoutRootDto dto = new()
        {
            RootPanel = root.RootPanel != null ? MapPanelToDto(root.RootPanel) : null,
            TopSide = MapAnchorSideToDto(root.TopSide),
            RightSide = MapAnchorSideToDto(root.RightSide),
            LeftSide = MapAnchorSideToDto(root.LeftSide),
            BottomSide = MapAnchorSideToDto(root.BottomSide),
        };

        foreach (LayoutFloatingWindow fw in root.FloatingWindows)
        {
            dto.FloatingWindows.Add(MapFloatingWindowToDto(fw));
        }

        foreach (LayoutAnchorable hidden in root.Hidden)
        {
            dto.Hidden.Add(MapAnchorableToDto(hidden));
        }

        return dto;
    }

    private LayoutPanelDto MapPanelToDto(LayoutPanel panel)
    {
        LayoutPanelDto dto = new()
        {
            Orientation = panel.Orientation.ToString(),
            CanDock = panel.CanDock,
        };
        CopyPositionableToDto(panel, dto);

        foreach (ILayoutPanelElement child in panel.Children)
        {
            dto.Children.Add(MapPanelChildToDto(child));
        }

        return dto;
    }

    private LayoutPositionableGroupDto MapPanelChildToDto(ILayoutPanelElement child)
    {
        switch (child)
        {
            case LayoutPanel p:
                return MapPanelToDto(p);
            case LayoutDocumentPaneGroup dpg:
                return MapDocumentPaneGroupToDto(dpg);
            case LayoutDocumentPane dp:
                return MapDocumentPaneToDto(dp);
            case LayoutAnchorablePaneGroup apg:
                return MapAnchorablePaneGroupToDto(apg);
            case LayoutAnchorablePane ap:
                return MapAnchorablePaneToDto(ap);
            default:
                throw new NotSupportedException($"Unknown panel child type: {child?.GetType().Name}");
        }
    }

    private LayoutDocumentPaneGroupDto MapDocumentPaneGroupToDto(LayoutDocumentPaneGroup group)
    {
        LayoutDocumentPaneGroupDto dto = new()
        {
            Orientation = group.Orientation.ToString(),
        };
        CopyPositionableToDto(group, dto);

        foreach (ILayoutDocumentPane child in group.Children)
        {
            switch (child)
            {
                case LayoutDocumentPane pane:
                    dto.Children.Add(MapDocumentPaneToDto(pane));
                    break;
                case LayoutDocumentPaneGroup nested:
                    dto.Children.Add(MapDocumentPaneGroupToDto(nested));
                    break;
            }
        }

        return dto;
    }

    private LayoutDocumentPaneDto MapDocumentPaneToDto(LayoutDocumentPane pane)
    {
        LayoutDocumentPaneDto dto = new()
        {
            Id = ((ILayoutPaneSerializable)pane).Id,
            ShowHeader = pane.ShowHeader,
        };
        CopyPositionableToDto(pane, dto);

        foreach (LayoutContent child in pane.Children)
        {
            switch (child)
            {
                case LayoutDocument doc:
                    dto.Children.Add(MapDocumentToDto(doc));
                    break;
                case LayoutAnchorable anch:
                    dto.Children.Add(MapAnchorableToDto(anch));
                    break;
            }
        }

        return dto;
    }

    private LayoutAnchorablePaneGroupDto MapAnchorablePaneGroupToDto(LayoutAnchorablePaneGroup group)
    {
        LayoutAnchorablePaneGroupDto dto = new()
        {
            Orientation = group.Orientation.ToString(),
        };
        CopyPositionableToDto(group, dto);

        foreach (ILayoutAnchorablePane child in group.Children)
        {
            switch (child)
            {
                case LayoutAnchorablePane pane:
                    dto.Children.Add(MapAnchorablePaneToDto(pane));
                    break;
                case LayoutAnchorablePaneGroup nested:
                    dto.Children.Add(MapAnchorablePaneGroupToDto(nested));
                    break;
            }
        }

        return dto;
    }

    private LayoutAnchorablePaneDto MapAnchorablePaneToDto(LayoutAnchorablePane pane)
    {
        LayoutAnchorablePaneDto dto = new()
        {
            Id = ((ILayoutPaneSerializable)pane).Id,
            Name = pane.Name,
        };
        CopyPositionableToDto(pane, dto);

        foreach (LayoutAnchorable child in pane.Children)
        {
            dto.Children.Add(MapAnchorableToDto(child));
        }

        return dto;
    }

    private LayoutDocumentDto MapDocumentToDto(LayoutDocument doc)
    {
        LayoutDocumentDto dto = new()
        {
            Description = doc.Description,
            CanMove = doc.CanMove,
        };
        CopyContentToDto(doc, dto);
        return dto;
    }

    private LayoutAnchorableDto MapAnchorableToDto(LayoutAnchorable anch)
    {
        LayoutAnchorableDto dto = new()
        {
            CanHide = anch.CanHide,
            CanAutoHide = anch.CanAutoHide,
            AutoHideWidth = anch.AutoHideWidth,
            AutoHideHeight = anch.AutoHideHeight,
            AutoHideMinWidth = anch.AutoHideMinWidth,
            AutoHideMinHeight = anch.AutoHideMinHeight,
            CanDockAsTabbedDocument = anch.CanDockAsTabbedDocument,
            CanMove = anch.CanMove,
            IsDetached = anch.IsDetached,
        };
        CopyContentToDto(anch, dto);
        return dto;
    }

    private void CopyContentToDto(LayoutContent content, LayoutContentDto dto)
    {
        dto.Title = content.Title;
        dto.ContentId = content.ContentId;
        dto.IsSelected = content.IsSelected;
        dto.IsLastFocusedDocument = content.IsLastFocusedDocument;
        dto.FloatingLeft = content.FloatingLeft;
        dto.FloatingTop = content.FloatingTop;
        dto.FloatingWidth = content.FloatingWidth;
        dto.FloatingHeight = content.FloatingHeight;
        dto.IsMaximized = content.IsMaximized;
        dto.CanClose = content.CanClose;
        dto.CanCloseDefault = content.canCloseDefault;
        dto.CanFloat = content.CanFloat;
        dto.CanShowOnHover = content.CanShowOnHover;

        if (content.LastActivationTimeStamp != null)
        {
            dto.LastActivationTimeStamp = content.LastActivationTimeStamp.Value.ToString(CultureInfo.InvariantCulture);
        }

        // PreviousContainer info
        ILayoutPreviousContainer prevContainer = content as ILayoutPreviousContainer;
        if (prevContainer?.PreviousContainer is ILayoutPaneSerializable paneSerializable)
        {
            dto.PreviousContainerId = paneSerializable.Id;
            dto.PreviousContainerIndex = content.PreviousContainerIndex;
        }
        else
        {
            string? prevContainerId = ((ILayoutPreviousContainer)content).PreviousContainerId;
            if (prevContainerId != null)
            {
                dto.PreviousContainerId = prevContainerId;
                dto.PreviousContainerIndex = content.PreviousContainerIndex;
            }
        }
    }

    private void CopyPositionableToDto<T>(LayoutPositionableGroup<T> source, LayoutPositionableGroupDto dto)
        where T : class, ILayoutElement
    {
        GridLength dockWidth = source.DockWidth;
        if (dockWidth.Value != 1.0 || !dockWidth.IsStar)
        {
            dto.DockWidth = FormatGridLength(dockWidth.IsAbsolute ? new GridLength(source.FixedDockWidth) : dockWidth);
        }

        GridLength dockHeight = source.DockHeight;
        if (dockHeight.Value != 1.0 || !dockHeight.IsStar)
        {
            dto.DockHeight = FormatGridLength(dockHeight.IsAbsolute ? new GridLength(source.FixedDockHeight) : dockHeight);
        }

        dto.DockMinWidth = source.DockMinWidth;
        dto.DockMinHeight = source.DockMinHeight;
        dto.FloatingWidth = source.FloatingWidth;
        dto.FloatingHeight = source.FloatingHeight;
        dto.FloatingLeft = source.FloatingLeft;
        dto.FloatingTop = source.FloatingTop;
        dto.IsMaximized = source.IsMaximized;
    }

    private LayoutAnchorSideDto MapAnchorSideToDto(LayoutAnchorSide? side)
    {
        LayoutAnchorSideDto dto = new();
        if (side == null)
        {
            return dto;
        }

        foreach (LayoutAnchorGroup group in side.Children)
        {
            dto.Children.Add(MapAnchorGroupToDto(group));
        }

        return dto;
    }

    private LayoutAnchorGroupDto MapAnchorGroupToDto(LayoutAnchorGroup group)
    {
        LayoutAnchorGroupDto dto = new()
        {
            Id = ((ILayoutPaneSerializable)group).Id,
        };

        if (((ILayoutPreviousContainer)group).PreviousContainer is ILayoutPaneSerializable pane)
        {
            dto.PreviousContainerId = pane.Id;
        }

        foreach (LayoutAnchorable child in group.Children)
        {
            dto.Children.Add(MapAnchorableToDto(child));
        }

        return dto;
    }

    private LayoutFloatingWindowDto MapFloatingWindowToDto(LayoutFloatingWindow fw)
    {
        switch (fw)
        {
            case LayoutDocumentFloatingWindow dfw:
                return new LayoutDocumentFloatingWindowDto
                {
                    RootPanel = dfw.RootPanel != null ? MapDocumentPaneGroupToDto(dfw.RootPanel) : null,
                };
            case LayoutAnchorableFloatingWindow afw:
                return new LayoutAnchorableFloatingWindowDto
                {
                    RootPanel = afw.RootPanel != null ? MapAnchorablePaneGroupToDto(afw.RootPanel) : null,
                };
            default:
                throw new NotSupportedException($"Unknown floating window type: {fw?.GetType().Name}");
        }
    }

    private LayoutRoot MapDtoToRoot(LayoutRootDto dto)
    {
        LayoutRoot root = new();

        if (dto.RootPanel != null)
        {
            root.RootPanel = MapDtoToPanel(dto.RootPanel);
        }

        if (dto.TopSide != null)
        {
            root.TopSide = MapDtoToAnchorSide(dto.TopSide);
        }

        if (dto.RightSide != null)
        {
            root.RightSide = MapDtoToAnchorSide(dto.RightSide);
        }

        if (dto.LeftSide != null)
        {
            root.LeftSide = MapDtoToAnchorSide(dto.LeftSide);
        }

        if (dto.BottomSide != null)
        {
            root.BottomSide = MapDtoToAnchorSide(dto.BottomSide);
        }

        root.FloatingWindows.Clear();
        if (dto.FloatingWindows != null)
        {
            foreach (LayoutFloatingWindowDto? fwDto in dto.FloatingWindows)
            {
                root.FloatingWindows.Add(MapDtoToFloatingWindow(fwDto));
            }
        }

        root.Hidden.Clear();
        if (dto.Hidden != null)
        {
            foreach (LayoutAnchorableDto? hiddenDto in dto.Hidden)
            {
                root.Hidden.Add(MapDtoToAnchorable(hiddenDto));
            }
        }

        return root;
    }

    private LayoutPanel MapDtoToPanel(LayoutPanelDto dto)
    {
        LayoutPanel panel = new()
        {
            Orientation = ParseOrientation(dto.Orientation),
            CanDock = dto.CanDock,
        };
        ApplyPositionableFromDto(dto, panel);

        if (dto.Children != null)
        {
            foreach (LayoutPositionableGroupDto? child in dto.Children)
            {
                panel.Children.Add(MapDtoToPanelChild(child));
            }
        }

        return panel;
    }

    private ILayoutPanelElement MapDtoToPanelChild(LayoutPositionableGroupDto child)
    {
        switch (child)
        {
            case LayoutPanelDto p:
                return MapDtoToPanel(p);
            case LayoutDocumentPaneGroupDto dpg:
                return MapDtoToDocumentPaneGroup(dpg);
            case LayoutDocumentPaneDto dp:
                return MapDtoToDocumentPane(dp);
            case LayoutAnchorablePaneGroupDto apg:
                return MapDtoToAnchorablePaneGroup(apg);
            case LayoutAnchorablePaneDto ap:
                return MapDtoToAnchorablePane(ap);
            default:
                throw new NotSupportedException($"Unknown DTO panel child type: {child?.GetType().Name}");
        }
    }

    private LayoutDocumentPaneGroup MapDtoToDocumentPaneGroup(LayoutDocumentPaneGroupDto dto)
    {
        LayoutDocumentPaneGroup group = new()
        {
            Orientation = ParseOrientation(dto.Orientation),
        };
        ApplyPositionableFromDto(dto, group);

        if (dto.Children != null)
        {
            foreach (LayoutPositionableGroupDto? child in dto.Children)
            {
                switch (child)
                {
                    case LayoutDocumentPaneDto paneDto:
                        group.Children.Add(MapDtoToDocumentPane(paneDto));
                        break;
                    case LayoutDocumentPaneGroupDto nestedDto:
                        group.Children.Add(MapDtoToDocumentPaneGroup(nestedDto));
                        break;
                }
            }
        }

        return group;
    }

    private LayoutDocumentPane MapDtoToDocumentPane(LayoutDocumentPaneDto dto)
    {
        LayoutDocumentPane pane = new()
        {
            ShowHeader = dto.ShowHeader,
        };
        ((ILayoutPaneSerializable)pane).Id = dto.Id;
        ApplyPositionableFromDto(dto, pane);

        if (dto.Children != null)
        {
            foreach (LayoutContentDto? child in dto.Children)
            {
                switch (child)
                {
                    case LayoutDocumentDto docDto:
                        pane.Children.Add(MapDtoToDocument(docDto));
                        break;
                    case LayoutAnchorableDto anchDto:
                        pane.Children.Add(MapDtoToAnchorable(anchDto));
                        break;
                }
            }
        }

        return pane;
    }

    private LayoutAnchorablePaneGroup MapDtoToAnchorablePaneGroup(LayoutAnchorablePaneGroupDto dto)
    {
        LayoutAnchorablePaneGroup group = new()
        {
            Orientation = ParseOrientation(dto.Orientation),
        };
        ApplyPositionableFromDto(dto, group);

        if (dto.Children != null)
        {
            foreach (LayoutPositionableGroupDto? child in dto.Children)
            {
                switch (child)
                {
                    case LayoutAnchorablePaneDto paneDto:
                        group.Children.Add(MapDtoToAnchorablePane(paneDto));
                        break;
                    case LayoutAnchorablePaneGroupDto nestedDto:
                        group.Children.Add(MapDtoToAnchorablePaneGroup(nestedDto));
                        break;
                }
            }
        }

        return group;
    }

    private LayoutAnchorablePane MapDtoToAnchorablePane(LayoutAnchorablePaneDto dto)
    {
        LayoutAnchorablePane pane = new()
        {
            Name = dto.Name,
        };
        ((ILayoutPaneSerializable)pane).Id = dto.Id;
        ApplyPositionableFromDto(dto, pane);

        if (dto.Children != null)
        {
            foreach (LayoutAnchorableDto? child in dto.Children)
            {
                pane.Children.Add(MapDtoToAnchorable(child));
            }
        }

        return pane;
    }

    private LayoutDocument MapDtoToDocument(LayoutDocumentDto dto)
    {
        LayoutDocument doc = new()
        {
            Description = dto.Description,
            CanMove = dto.CanMove,
        };
        ApplyContentFromDto(dto, doc);
        return doc;
    }

    private LayoutAnchorable MapDtoToAnchorable(LayoutAnchorableDto dto)
    {
        LayoutAnchorable anch = new()
        {
            CanHide = dto.CanHide,
            CanAutoHide = dto.CanAutoHide,
            AutoHideWidth = dto.AutoHideWidth,
            AutoHideHeight = dto.AutoHideHeight,
            AutoHideMinWidth = dto.AutoHideMinWidth,
            AutoHideMinHeight = dto.AutoHideMinHeight,
            CanDockAsTabbedDocument = dto.CanDockAsTabbedDocument,
            CanMove = dto.CanMove,
            IsDetached = dto.IsDetached,
        };
        ApplyContentFromDto(dto, anch);
        return anch;
    }

    private void ApplyContentFromDto(LayoutContentDto dto, LayoutContent content)
    {
        if (dto.Title != null)
        {
            content.Title = dto.Title;
        }

        if (dto.ContentId != null)
        {
            content.ContentId = dto.ContentId;
        }

        content.IsSelected = dto.IsSelected;
        content.IsLastFocusedDocument = dto.IsLastFocusedDocument;
        content.FloatingLeft = dto.FloatingLeft;
        content.FloatingTop = dto.FloatingTop;
        content.FloatingWidth = dto.FloatingWidth;
        content.FloatingHeight = dto.FloatingHeight;
        content.IsMaximized = dto.IsMaximized;
        content.CanClose = dto.CanClose;
        content.CanFloat = dto.CanFloat;
        content.CanShowOnHover = dto.CanShowOnHover;

        // A timestamp that cannot be read is dropped rather than thrown on. It only orders the
        // document navigator, and losing the whole layout over one unreadable attribute - a file
        // written by an older version under a different culture, or edited by hand - is not a
        // trade the user would make.
        if (dto.LastActivationTimeStamp != null && TryParseTimeStamp(dto.LastActivationTimeStamp, out DateTime timeStamp))
        {
            content.LastActivationTimeStamp = timeStamp;
        }

        if (dto.PreviousContainerId != null)
        {
            ((ILayoutPreviousContainer)content).PreviousContainerId = dto.PreviousContainerId;
            content.PreviousContainerIndex = dto.PreviousContainerIndex;
        }
    }

    private void ApplyPositionableFromDto<T>(LayoutPositionableGroupDto dto, LayoutPositionableGroup<T> target)
        where T : class, ILayoutElement
    {
        // An unreadable length falls back to the default of the target instead of aborting the
        // restore of the whole layout.
        if (TryParseGridLength(dto.DockWidth, out GridLength dockWidth))
        {
            target.DockWidth = dockWidth;
        }

        if (TryParseGridLength(dto.DockHeight, out GridLength dockHeight))
        {
            target.DockHeight = dockHeight;
        }

        target.DockMinWidth = dto.DockMinWidth;
        target.DockMinHeight = dto.DockMinHeight;
        target.FloatingWidth = dto.FloatingWidth;
        target.FloatingHeight = dto.FloatingHeight;
        target.FloatingLeft = dto.FloatingLeft;
        target.FloatingTop = dto.FloatingTop;
        target.IsMaximized = dto.IsMaximized;
    }

    private LayoutAnchorSide MapDtoToAnchorSide(LayoutAnchorSideDto dto)
    {
        LayoutAnchorSide side = new();
        if (dto.Children != null)
        {
            foreach (LayoutAnchorGroupDto? groupDto in dto.Children)
            {
                side.Children.Add(MapDtoToAnchorGroup(groupDto));
            }
        }

        return side;
    }

    private LayoutAnchorGroup MapDtoToAnchorGroup(LayoutAnchorGroupDto dto)
    {
        LayoutAnchorGroup group = new();
        ((ILayoutPaneSerializable)group).Id = dto.Id;

        if (dto.PreviousContainerId != null)
        {
            ((ILayoutPreviousContainer)group).PreviousContainerId = dto.PreviousContainerId;
        }

        if (dto.Children != null)
        {
            foreach (LayoutAnchorableDto? child in dto.Children)
            {
                group.Children.Add(MapDtoToAnchorable(child));
            }
        }

        return group;
    }

    private LayoutFloatingWindow MapDtoToFloatingWindow(LayoutFloatingWindowDto dto)
    {
        switch (dto)
        {
            case LayoutDocumentFloatingWindowDto dfw:
                LayoutDocumentFloatingWindow docFw = new();
                if (dfw.RootPanel != null)
                {
                    docFw.RootPanel = MapDtoToDocumentPaneGroup(dfw.RootPanel);
                }

                return docFw;

            case LayoutAnchorableFloatingWindowDto afw:
                LayoutAnchorableFloatingWindow anchFw = new();
                if (afw.RootPanel != null)
                {
                    anchFw.RootPanel = MapDtoToAnchorablePaneGroup(afw.RootPanel);
                }

                return anchFw;

            default:
                throw new NotSupportedException($"Unknown floating window DTO type: {dto?.GetType().Name}");
        }
    }

    /// <summary>
    /// Parses a stored orientation, falling back to <see cref="Orientation.Horizontal"/> for a
    /// value that cannot be read rather than aborting the restore of the whole layout.
    /// </summary>
    /// <param name="value">The stored orientation.</param>
    /// <returns>The orientation.</returns>
    private static Orientation ParseOrientation(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Orientation.Horizontal;
        }

        return Enum.TryParse<Orientation>(value, true, out Orientation orientation)
            && Enum.IsDefined(typeof(Orientation), orientation)
                ? orientation
                : Orientation.Horizontal;
    }

    /// <summary>Reads a stored activation timestamp, tolerating a non-invariant culture.</summary>
    /// <param name="value">The stored timestamp.</param>
    /// <param name="timeStamp">The parsed timestamp.</param>
    /// <returns><see langword="true"/> when the value could be read.</returns>
    private static bool TryParseTimeStamp(string value, out DateTime timeStamp) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeStamp)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out timeStamp);

    /// <summary>Reads a stored dock length.</summary>
    /// <param name="value">The stored length, e.g. "1*" or "200".</param>
    /// <param name="length">The parsed length.</param>
    /// <returns><see langword="true"/> when the value could be read.</returns>
    private static bool TryParseGridLength(string value, out GridLength length)
    {
        length = default;
        if (value == null)
        {
            return false;
        }

        string text = value.Trim();
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            length = GridLength.Auto;
            return true;
        }

        GridUnitType unit = GridUnitType.Pixel;
        double factor = 1.0;
        if (text.EndsWith('*'))
        {
            unit = GridUnitType.Star;
            text = text[..^1];
            if (text.Length == 0)
            {
                text = "1";
            }
        }
        else if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }
        else if (text.EndsWith("in", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
            factor = 96.0;
        }
        else if (text.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
            factor = 96.0 / 2.54;
        }
        else if (text.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
            factor = 96.0 / 72.0;
        }

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out double amount))
        {
            return false;
        }

        amount *= factor;
        if (!double.IsFinite(amount) || amount < 0)
        {
            return false;
        }

        length = new GridLength(amount, unit);
        return true;
    }

    private static string FormatGridLength(GridLength length)
    {
        if (length.IsAuto)
        {
            return "Auto";
        }

        if (!length.IsStar)
        {
            return length.Value.ToString(CultureInfo.InvariantCulture);
        }
        // Match the unit-star shorthand used by WPF's invariant GridLength converter.
        return Math.Abs(length.Value - 1.0) < 2.2204460492503131e-15
            ? "*"
            : length.Value.ToString(CultureInfo.InvariantCulture) + "*";
    }
}
