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
            if (count > 0 && used + width > available)
            {
                break;
            }

            used += width;
            count++;
        }
        return count;
    }

    internal static void FillToolWidths(IReadOnlyList<double> desired, double available, List<double> output)
    {
        output.Clear();
        double sum = 0;
        for (int index = 0; index < desired.Count; index++)
        {
            sum += desired[index];
        }

        bool fit = sum <= available;
        double compressedWidth = desired.Count == 0 ? 0 : available / desired.Count;
        for (int index = 0; index < desired.Count; index++)
        {
            output.Add(fit ? desired[index] : compressedWidth);
        }
    }
}
