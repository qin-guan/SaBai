using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ReferralPartners.Update.Put;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Put("/referral-partners/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a referral partner");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var partner = await db.ReferralPartners.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral partner was not found.",
            ct
        );

        partner.Name = req.Name.Trim();
        partner.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        partner.IsActive = req.IsActive;

        await db.SaveChangesAsync(ct);

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
