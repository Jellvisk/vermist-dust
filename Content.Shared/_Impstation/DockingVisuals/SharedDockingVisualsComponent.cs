using Content.Shared.Shuttles.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.DockingVisuals;

public sealed partial class SharedDockingVisualsComponent : Component
{
    [NetSerializable, Serializable]
    public enum DockingVisualState
    {
        Undocked,
        Docked,
        InRange,
    }
    [Serializable, NetSerializable]
    public enum DockingVisualLayers : byte
    {
        Base,
        Lights,
    }
}
