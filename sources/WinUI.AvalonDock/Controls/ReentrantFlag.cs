// Ported from Dirkster.AvalonDock v5.0.0 (MS-PL), Controls/ReentrantFlag.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;

namespace AvalonDock.Controls;

/// <summary>
/// Represents the reentrant Flag.
/// </summary>
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

    /// <summary>
    /// Executes the enter operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public _ReentrantFlagHandler Enter()
    {
        if (flag)
        {
            throw new InvalidOperationException();
        }

        return new _ReentrantFlagHandler(this);
    }

    /// <summary>
    /// Represents the reentrant Flag Handler.
    /// </summary>
    public class _ReentrantFlagHandler : IDisposable
    {
        private ReentrantFlag owner;

        /// <summary>
        /// Initializes a new instance of the <see cref="_ReentrantFlagHandler"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        public _ReentrantFlagHandler(ReentrantFlag owner)
        {
            this.owner = owner;
            this.owner.flag = true;
        }

        /// <summary>
        /// Executes the dispose operation.
        /// </summary>
        public void Dispose()
        {
            owner.flag = false;
        }
    }
}
