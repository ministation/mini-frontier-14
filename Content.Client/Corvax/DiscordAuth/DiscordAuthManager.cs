using Content.Client._Jerry.DiscordAuth;
using Content.Shared.Corvax.DiscordAuth;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client.Corvax.DiscordAuth;

public sealed class DiscordAuthManager
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IStateManager _state = default!;

    public string AuthLink = default!;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);
    }

    public void OnDiscordAuthRequired(MsgDiscordAuthRequired args)
    {
        AuthLink = args.Link;
        _state.RequestStateChange<DiscordAuthState>();
    }
}
