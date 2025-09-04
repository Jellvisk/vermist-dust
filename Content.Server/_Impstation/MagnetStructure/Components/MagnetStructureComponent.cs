using Content.Shared._Impstation.MagnetStructure.Components;
using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Server._Impstation.MagnetStructure.Components;

/// <summary>
/// Allows the entity to recieve grid merge and unmerge events.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, AutoGenerateComponentState(true, fieldDeltas: true)]
public sealed partial class MagnetStructureComponent : SharedMagnetStructureComponent
{
    public WeldJoint? MagnetJoint = null;
    public string? MagnetJointId = null;

    /// <summary>
    /// When the next proximity check will trigger.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Next time the proximity alert will update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(2);
}
