using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/consults/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a consult and its diagnoses");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var consult = await db.Consults.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Consult was not found.",
            ct
        );
        if (await db.Referrals.AnyAsync(x => x.ConsultId == consult.Id, ct))
        {
            throw new ConflictException("Cannot delete a consult that still has referrals.");
        }

        if (await db.Orders.AnyAsync(x => x.ConsultId == consult.Id, ct))
        {
            throw new ConflictException("Cannot delete a consult that still has orders.");
        }

        db.Consults.Remove(consult);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
