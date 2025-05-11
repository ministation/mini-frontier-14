using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.GameTicking;

namespace Content.Server.Corvax.Elzuosa
{
    public sealed class ElzuosaColorSystem : EntitySystem
    {
        [Dependency] private readonly PointLightSystem _pointLightSystem = default!;
        [Dependency] private readonly SharedRgbLightControllerSystem _rgbSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly StaminaSystem _stamina = default!;
        [Dependency] private readonly HungerSystem _hunger = default!;

        public bool SelfEmagged;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ElzuosaColorComponent, GotEmaggedEvent>(OnEmagged);
            SubscribeLocalEvent<ElzuosaColorComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ElzuosaColorComponent, HungerComponent>();
            while (query.MoveNext(out var uid, out var elzuosa, out var hungerComp))
            {
                var hunger = _hunger.GetHunger(hungerComp);

                if (TryComp<PointLightComponent>(uid, out var _))
                {
                    var radius = GetRadiusByHunger(hunger);
                    if (radius != null)
                        _pointLightSystem.SetRadius(uid, radius.Value);
                }

                if (elzuosa.StannedByEmp)
                {
                    _stamina.TakeStaminaDamage(uid, 120);
                    elzuosa.StannedByEmp = false;
                }
            }
        }

        private float? GetRadiusByHunger(float hunger)
        {
            return hunger switch
            {
                <= 50 => 0.5f,
                <= 55 => 1.05f,
                <= 60 => 1.1f,
                <= 65 => 1.2f,
                <= 70 => 1.4f,
                <= 75 => 1.6f,
                <= 80 => 1.8f,
                <= 85 => 2f,
                > 90 => 2.3f,
                _ => null
            };
        }

        private void OnEmagged(EntityUid uid, ElzuosaColorComponent comp, ref GotEmaggedEvent args)
        {
            SelfEmagged = args.UserUid == uid;
            comp.Hacked = !comp.Hacked;

            var user = args.UserUid;
            var target = uid;

            if (SelfEmagged)
            {
                HandleEmagLocal(target, comp.Hacked, comp.CycleRate);
            }
            else
            {
                HandleEmagRemote(user, target, comp.Hacked, comp.CycleRate);
            }
        }

        private void HandleEmagLocal(EntityUid uid, bool hacked, float cycleRate)
        {
            if (hacked)
            {
                _popupSystem.PopupEntity(Loc.GetString("elzuosa-selfemag-success"), uid);
                var rgb = EnsureComp<RgbLightControllerComponent>(uid);
                _rgbSystem.SetCycleRate(uid, cycleRate, rgb);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("elzuosa-selfdeemag-success"), uid);
                RemComp<RgbLightControllerComponent>(uid);
            }
        }

        private void HandleEmagRemote(EntityUid user, EntityUid target, bool hacked, float cycleRate)
        {
            if (hacked)
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("elzuosa-emag-success", ("target", Identity.Entity(target, EntityManager))),
                    target, user);

                _popupSystem.PopupEntity(
                    Loc.GetString("elzuosa-emagged-success", ("user", Identity.Entity(user, EntityManager))),
                    user, target);

                var rgb = EnsureComp<RgbLightControllerComponent>(target);
                _rgbSystem.SetCycleRate(target, cycleRate, rgb);
            }
            else
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("elzuosa-deemag-success", ("target", Identity.Entity(target, EntityManager))),
                    target, user);

                _popupSystem.PopupEntity(
                    Loc.GetString("elzuosa-deemagged-success", ("user", Identity.Entity(user, EntityManager))),
                    user, target);

                RemComp<RgbLightControllerComponent>(target);
            }
        }

        private void OnPlayerSpawn(EntityUid uid, ElzuosaColorComponent comp, PlayerSpawnCompleteEvent args)
        {
            if (!HasComp<HumanoidAppearanceComponent>(uid))
                return;

            var profile = args.Profile;
            if (profile != null)
                SetEntityPointLightColor(uid, profile);
        }

        public void SetEntityPointLightColor(EntityUid uid, HumanoidCharacterProfile profile)
        {
            var color = profile.Appearance.SkinColor;
            _pointLightSystem.SetColor(uid, color);
        }
    }
}
