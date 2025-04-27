using Robust.Shared.Serialization;

namespace Content.Shared._Corvax.BSDash;

[Serializable, NetSerializable]
public sealed class BSDashEvent : EntityEventArgs
{
    public NetEntity User { get; }
    public BSDashEvent(NetEntity user)
    {
        User = user;
    }
}