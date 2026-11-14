using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.UpdateReimbursement.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ReimbursementNotes)
            .MaximumLength(500)
            .When(x => x.ReimbursementNotes is not null);
    }
}
