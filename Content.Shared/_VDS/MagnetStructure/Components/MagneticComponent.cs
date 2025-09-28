namespace Content.Shared._VDS.MagnetStructure.Components;

[RegisterComponent]
public sealed partial class MagneticComponent : Component
{
    [DataField]
    public EntityUid? ConnectedTo = null;

    [DataField]
    public float Magnetism = 1f;

}
