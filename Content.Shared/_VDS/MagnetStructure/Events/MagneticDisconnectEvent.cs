using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Shared._VDS.MagnetStructure.Events;
/// <summary>
/// Raised when a magnet disconnects from another grid.
/// </summary>
public sealed class MagneticDisconnectEvent : EntityEventArgs
{
    /// <summary>
    /// Magnetic structure that caused the disconnection.
    /// </summary>
    public readonly EntityUid Source;

    /// <summary>
    /// The joint being deleted.
    /// </summary>
    public readonly WeldJoint Joint;

    public MagneticDisconnectEvent(EntityUid source, WeldJoint joint)
    {
        Source = source;
        Joint = joint;
    }
}
