using Robust.Shared.Physics.Components;

namespace Content.Shared._VDS.MagnetStructure.Events;
/// <summary>
/// Raised when a magnet connects to another grid.
/// </summary>
public sealed class MagneticConnectEvent : EntityEventArgs
{

    public readonly (EntityUid SourceUid, EntityUid SourceGrid, TransformComponent SourceXForm, PhysicsComponent SourcePhysic) Source;

    public readonly (EntityUid TargetUid, EntityUid TargetGrid, TransformComponent TargetXForm, PhysicsComponent TargetPhysic) Target;

    public MagneticConnectEvent(
        (EntityUid sourceUid, EntityUid sourceGrid, TransformComponent sourceXForm, PhysicsComponent sourcePhysics) source,
        (EntityUid targetUid, EntityUid targetGrid, TransformComponent targetXForm, PhysicsComponent targetPhysics) target
        )
    {
        Source = source;
        Target = target;
    }
}
