using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.DockingVisuals;

[Serializable, NetSerializable]
public enum DockingVisualState : byte
{
    Docked,
    Undocked,
}
[Serializable, NetSerializable]
public enum DockingVisualLayers : byte
{
    Base,
    Lights,
}
