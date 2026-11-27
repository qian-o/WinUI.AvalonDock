// Adapted from Dirkster.AvalonDock v5.0.0; distributed under the MS-PL.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792 / source/Components/AvalonDock/MathHelper.cs
using System;

namespace AvalonDock;

internal static class MathHelper
{
    public static double MinMax(double value, double min, double max)
    {
        if (min > max)
        {
            throw new ArgumentException("The minimum should not be greater then the maximum", nameof(min));
        }

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static void AssertIsPositiveOrZero(double value)
    {
        if (value < 0.0)
        {
            throw new ArgumentException("Invalid value, must be a positive number or equal to zero", nameof(value));
        }
    }
}
