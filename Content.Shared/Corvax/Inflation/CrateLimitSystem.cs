using Robust.Shared.Timing;

namespace Content.Shared.Corvax.Inflation;

public sealed class CrateLimitSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private TimeSpan? Timer = TimeSpan.FromMinutes(1);
    private TimeSpan? NextTimeToCheck = TimeSpan.FromSeconds(5);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (NextTimeToCheck < _gameTiming.CurTime)
        {
            var query = EntityQueryEnumerator<CrateLimitComponent>();
            while (query.MoveNext(out var uid, out var crateLimitComponent))
            {
                if (crateLimitComponent.OwnedCrates <= crateLimitComponent.MaxOwnedCrates)
                    crateLimitComponent.OwnedCrates--;
            }
            NextTimeToCheck = NextTimeToCheck + Timer;
        }
    }
}
