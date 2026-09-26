using Windows.Foundation;

namespace AvalonDock.Platforms;

/// <summary>Supplies system pointer gesture policy without exposing native messages or handles.</summary>
internal interface IPointerGestureService
{
    /// <summary>Records a primary-button press and returns its consecutive click count for this origin.</summary>
    int RegisterPrimaryPress(object origin);

    /// <summary>Returns horizontal and vertical drag distances in physical desktop pixels for the origin.</summary>
    Size GetDragThreshold(object origin);
}
