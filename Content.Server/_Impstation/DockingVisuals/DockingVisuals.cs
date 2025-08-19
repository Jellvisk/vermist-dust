using Content.Shared.Shuttles.Components;
using Robust.Shared.Serialization;

namespace Content.Server._Impstation.DockingVisuals;

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
