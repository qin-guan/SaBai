using FastEndpoints;
using FluentValidation;
using SaBai.API.Push;

namespace SaBai.API.Endpoints.Push.Subscribe.Put;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Keys).NotNull();
        When(
            x => x.Keys is not null,
            () =>
            {
                // An uncompressed P-256 point and a 16-byte secret, both base64url.
                RuleFor(x => x.Keys.P256dh)
                    .Must(x => x is not null && PushSettings.HasLength(x, 65))
                    .WithMessage("keys.p256dh is not a push subscription key.");
                RuleFor(x => x.Keys.Auth)
                    .Must(x => x is not null && PushSettings.HasLength(x, 16))
                    .WithMessage("keys.auth is not a push subscription secret.");
            }
        );
    }
}
