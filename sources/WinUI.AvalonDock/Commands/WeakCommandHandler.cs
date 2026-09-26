// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Commands/WeakAction.cs and Commands/WeakFunc.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;
using System.Reflection;

namespace AvalonDock.Commands;

/// <summary>视图保留命令时，不阻止命令目标被回收。</summary>
internal sealed class WeakCommandHandler<TDelegate> where TDelegate : Delegate
{
    private readonly TDelegate? staticHandler;
    private readonly WeakReference<object>? target;
    private readonly MethodInfo? method;

    internal WeakCommandHandler(TDelegate handler)
    {
        if (handler.Method.IsStatic)
        {
            staticHandler = handler;
        }
        else
        {
            target = new WeakReference<object>(handler.Target
                ?? throw new ArgumentException("Instance handler requires a target.", nameof(handler)));
            method = handler.Method;
        }
    }

    internal bool IsStatic => staticHandler != null;

    internal TDelegate? GetHandler()
    {
        if (staticHandler != null)
        {
            return staticHandler;
        }

        return target is not null && method is not null && target.TryGetTarget(out object? owner)
            ? (TDelegate)method.CreateDelegate(typeof(TDelegate), owner)
            : null;
    }
}
