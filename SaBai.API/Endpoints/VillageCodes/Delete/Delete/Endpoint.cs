using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.VillageCodes.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/village-codes/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a village code");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var village = await db.VillageCodes.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Village code was not found.",
            ct
        );
        if (await db.Visits.AnyAsync(x => x.VillageCodeId == village.Id, ct))
        {
            throw new ConflictException("A visit still references this village code.");
        }

        db.VillageCodes.Remove(village);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
