namespace AvalonDock.Controls;

/// <summary>Upstream header sizing calculations for the native virtualizing host.</summary>
internal static class TabHeaderLayoutRules
{
    internal static int VisibleDocumentCount(IReadOnlyList<double> widths, double available)
    {
        double used = 0d;
        int count = 0;
        foreach (double width in widths)
        {
            if (used + width > available)
            {
                break;
            }

            used += width;
            count++;
        }
        return count;
    }

    internal static double[] ToolWidths(IReadOnlyList<double> desired, double available)
    {
        if (desired.Count == 0)
        {
            return [];
        }

        if (desired.Sum() < available)
        {
            return desired.ToArray();
        }

        return Enumerable.Repeat(available / desired.Count, desired.Count).ToArray();
    }
}
