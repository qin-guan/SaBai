using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.ReferralPartners.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/referral-partners/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a referral partner");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var partner = await db.ReferralPartners.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral partner was not found.",
            ct
        );

        if (await db.Referrals.AnyAsync(x => x.ReferredToId == partner.Id, ct))
        {
            throw new ConflictException(
                "A referral still references this partner. Deactivate it instead."
            );
        }

        db.ReferralPartners.Remove(partner);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
