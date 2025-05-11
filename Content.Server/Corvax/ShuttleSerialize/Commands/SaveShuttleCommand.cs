using System.IO;
using Content.Server.Administration;
using Content.Server.Corvax.ShuttleSerialize.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Players;
using NetSerializer;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.Corvax.ShuttleSerialize.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class SaveShuttleCommand : IConsoleCommand
{
    [Dependency] private readonly EntityManager _entity = default!;
    [Dependency] private readonly IEntitySystemManager _manager = default!;

    public string Command => "saveshuttle";

    public string Description => "Saves shuttle.";

    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string arg, string[] args)
    {
        if (!_entity.TryGetComponent<TransformComponent>(shell.Player?.AttachedEntity, out var transform))
            return;

        if (transform.GridUid is null)
            return;

        using FileStream stream = new($"{shell.Player.Name}.sht", FileMode.Create, FileAccess.Write);

        var time = System.Diagnostics.Stopwatch.GetTimestamp();

        _manager.GetEntitySystem<GridSerializationSystem>().Serialize(stream, transform.GridUid.Value);

        shell.WriteLine(stream.Name);
        shell.WriteLine(((double) (System.Diagnostics.Stopwatch.GetTimestamp() - time) / System.Diagnostics.Stopwatch.Frequency).ToString());
    }
}
