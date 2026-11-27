// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ReentrantFlag.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;

namespace AvalonDock.Controls;

internal class ReentrantFlag
{
    private bool flag = false;

    /// <summary>
    /// Gets a value indicating whether can Enter.
    /// </summary>
    public bool CanEnter
    {
        get
        {
            return !flag;
        }
    }

    public _ReentrantFlagHandler Enter()
    {
        if (flag)
        {
            throw new InvalidOperationException();
        }

        return new _ReentrantFlagHandler(this);
    }

    public class _ReentrantFlagHandler : IDisposable
    {
        private ReentrantFlag owner;

        public _ReentrantFlagHandler(ReentrantFlag owner)
        {
            this.owner = owner;
            this.owner.flag = true;
        }

        public void Dispose()
        {
            owner.flag = false;
        }
    }
}
