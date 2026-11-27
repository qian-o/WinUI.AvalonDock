namespace AvalonDock.Platforms;

/// <summary>A remembered native child focus target with platform-owned lifetime validation.</summary>
internal interface INativeFocusTarget
{
    bool Focus();
}
