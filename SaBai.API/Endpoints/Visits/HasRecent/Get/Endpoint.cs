using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Visits.HasRecent.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(1);

    public override void Configure()
    {
        Get("/visits/recent");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Check whether a patient had a visit opened in the last minute");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var windowStart = DateTimeOffset.UtcNow - DuplicateWindow;
        var recentVisitDate = await db
            .Visits.AsNoTracking()
            .Where(x => x.PatientId == req.PatientId && x.Date >= windowStart)
            .OrderByDescending(x => x.Date)
            .Select(x => (DateTimeOffset?)x.Date)
            .FirstOrDefaultAsync(ct);

        await Send.OkAsync(new Response(recentVisitDate is not null, recentVisitDate), ct);
    }
}
