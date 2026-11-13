using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ReferralPartners.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/referral-partners");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List referral partners, ordered by name");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partners = await db
            .ReferralPartners.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new Response(
                x.Id,
                x.Name,
                x.Remarks,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            ))
            .ToListAsync(ct);

        await Send.OkAsync(partners, ct);
    }
}
