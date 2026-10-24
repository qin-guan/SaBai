using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.VillageCodes.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/village-codes");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a village code");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var code = req.Code.Trim();
        if (await db.VillageCodes.AnyAsync(x => x.Code == code, ct))
        {
            throw new ConflictException("A village code with this code already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var village = new VillageCode
        {
            Code = code,
            Name = req.Name.Trim(),
            ColorHex = req.ColorHex.Trim(),
            IsVisible = req.IsVisible,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.VillageCodes.Add(village);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { village.Id },
            new Response(village.Id, village.Code, village.Name, village.ColorHex, village.IsVisible, village.CreatedAt, village.UpdatedAt),
            cancellation: ct);
    }
}
