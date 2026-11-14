using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.ReferralPartners.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/referral-partners");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Create a referral partner");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var partner = await db.ReferralPartners.FindExistingAsync(req.Id, ct);
        if (partner is null)
        {
            var now = DateTimeOffset.UtcNow;
            partner = new ReferralPartner
            {
                Id = req.Id ?? Guid.CreateVersion7(),
                Name = req.Name.Trim(),
                Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
                IsActive = req.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.ReferralPartners.Add(partner);
            await db.SaveChangesAsync(ct);
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { partner.Id },
            new Response(
                partner.Id,
                partner.Name,
                partner.Remarks,
                partner.IsActive,
                partner.CreatedAt,
                partner.UpdatedAt
            ),
            cancellation: ct
        );
    }
}
