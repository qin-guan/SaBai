using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.VillageCodes.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/village-codes/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a village code");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var village = await db.VillageCodes.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Village code was not found.",
            ct
        );

        if (req.Code is not null)
        {
            var code = req.Code.Trim();
            if (await db.VillageCodes.AnyAsync(x => x.Id != village.Id && x.Code == code, ct))
            {
                throw new ConflictException("A village code with this code already exists.");
            }

            village.Code = code;
        }

        if (req.Name is not null)
        {
            village.Name = req.Name.Trim();
        }

        if (req.ColorHex is not null)
        {
            village.ColorHex = req.ColorHex.Trim();
        }

        if (req.IsVisible is bool isVisible)
        {
            village.IsVisible = isVisible;
        }

        village.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(
                village.Id,
                village.Code,
                village.Name,
                village.ColorHex,
                village.IsVisible,
                village.CreatedAt,
                village.UpdatedAt
            ),
            ct
        );
    }
}
