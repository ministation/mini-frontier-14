[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class CrateLimitComponent : Component
{
    [DataField("ownedCrates"), ViewVariables(VVAccess.ReadWrite)]
    public string OwnedCrates;

    [DataField("maxOwnedCrates"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxOwnedCrates;
}
