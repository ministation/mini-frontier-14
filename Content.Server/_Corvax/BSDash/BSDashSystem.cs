using System.Numerics;
using Content.Shared._Corvax.BSDash;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;

public sealed class BSDashClassSystem : EntitySystem
{
    [Dependency] private readonly PhysicsSystem _physics = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BSDashEvent>(Uwi);
    }
    private void BSDash(EntityUid uid)
    {
        var xform = Transform(uid);
        Vector2 direction = xform.LocalRotation.ToWorldVec();

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            float forceMagnitude = 1000000f; // Настройка силы
            Vector2 force = direction * forceMagnitude;
            _physics.ApplyLinearImpulse(uid, force, body: physics);
        }
    }
    private void Uwi(BSDashEvent mew)
    {
        if (!TryComp(GetEntity(mew.User), out TransformComponent? transform) || transform.GridUid == null)
            return;
        BSDash(transform.GridUid.Value);
    }
}