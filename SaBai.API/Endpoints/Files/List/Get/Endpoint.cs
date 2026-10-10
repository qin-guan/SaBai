using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Files.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/files");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List clinical files");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var deleted = (req.Deleted ?? "false").Trim().ToLowerInvariant();
        bool? isDeleted = deleted switch
        {
            "false" => false,
            "true" => true,
            "all" => null,
            _ => throw new BadRequestException("deleted parameter must be 'all', 'true', or 'false'."),
        };

        var query = db.ClinicalFiles.AsNoTracking().AsQueryable();
        if (req.PatientId is int patientId)
        {
            query = query.Where(x => x.PatientId == patientId);
        }

        if (isDeleted is bool flag)
        {
            query = query.Where(x => x.IsDeleted == flag);
        }

        var files = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new Response(
                x.Id,
                x.PatientId,
                x.FileName,
                x.StoredFileName,
                x.Description,
                x.SizeBytes,
                x.IsDeleted,
                x.CreatedAt))
            .ToListAsync(ct);

        await Send.OkAsync(files, ct);
    }
}
