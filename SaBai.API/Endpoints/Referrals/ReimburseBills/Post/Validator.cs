using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.ReimburseBills.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ReferralIds)
            .NotEmpty()
            .WithMessage("Select at least one referral bill.");

        RuleFor(x => x.ReimbursementNotes)
            .MaximumLength(500)
            .When(x => x.ReimbursementNotes is not null);
    }
}
