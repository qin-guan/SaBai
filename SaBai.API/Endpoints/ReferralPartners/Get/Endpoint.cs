using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ReferralPartners.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/referral-partners/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Get a referral partner");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var partner = await db
            .ReferralPartners.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Referral partner was not found.", ct);

        await Send.OkAsync(
            new Response(
                partner.Id,
                partner.Name,
                partner.Remarks,
                partner.IsActive,
                partner.CreatedAt,
                partner.UpdatedAt
            ),
            ct
        );
    }
}
