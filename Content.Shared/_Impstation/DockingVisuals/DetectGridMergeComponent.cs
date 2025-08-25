using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.DockingVisuals.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DetectGridMergeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Connected = false;
}

