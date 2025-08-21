namespace Content.Shared._Impstation.DockingVisuals.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[RegisterComponent]
public sealed partial class DetectGridMergeComponent : Component
{
    [DataField(required: false)]
    public bool DetectUnmerge = true;
}

