using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.ListByConsult.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/consults/{ConsultId}/referrals");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List referrals for a consult, newest first");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Consults.AsNoTracking().FirstOrNotFoundAsync(x => x.Id == req.ConsultId, "Consult was not found.", ct);
        var referrals = await db.Referrals
            .AsNoTracking()
            .Where(x => x.ConsultId == req.ConsultId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new Response(
                x.Id,
                x.ReferredFor,
                x.ReferralNotes,
                x.ReferralState,
                x.ReferralOutcome,
                x.ConsultId,
                x.CreatedAt))
            .ToListAsync(ct);

        await Send.OkAsync(referrals, ct);
    }
}
