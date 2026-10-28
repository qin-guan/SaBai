using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/referrals/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a referral");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var referral = await db
            .Referrals.AsNoTracking()
            .Include(x => x.Consult)
                .ThenInclude(x => x.Doctor)
            .Include(x => x.Consult)
                .ThenInclude(x => x.Visit)
                    .ThenInclude(x => x.Patient)
            .Include(x => x.Consult)
                .ThenInclude(x => x.Visit)
                    .ThenInclude(x => x.VillageCode)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Referral was not found.", ct);

        await Send.OkAsync(
            new Response(
                referral.Id,
                referral.ReferredFor,
                referral.ReferralNotes,
                referral.ReferralState,
                referral.ReferralOutcome,
                referral.ConsultId,
                referral.CreatedAt,
                referral.Consult.Date,
                referral.Consult.DoctorId,
                referral.Consult.Visit.PatientId,
                referral.Consult.Visit.Patient.Name,
                referral.Consult.Visit.Patient.PhotoStoredFileName != null,
                referral.Consult.Visit.VillageCodeId,
                referral.Consult.Visit.VillageCode.Code,
                referral.Consult.Visit.VillageCode.ColorHex,
                referral.Consult.Doctor.Nickname
            ),
            ct
        );
    }
}
