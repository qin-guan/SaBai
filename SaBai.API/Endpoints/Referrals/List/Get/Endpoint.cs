using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/referrals");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "List referrals, newest first");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await db
            .Referrals.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ReferredFor,
                x.ReferralNotes,
                x.ReferralState,
                x.ReferralOutcome,
                x.ConsultId,
                x.CreatedAt,
                ConsultDate = x.Consult.Date,
                x.Consult.DoctorId,
                x.Consult.Visit.PatientId,
                PatientName = x.Consult.Visit.Patient.Name,
                HasPhoto = x.Consult.Visit.Patient.PhotoStoredFileName != null,
                x.Consult.Visit.VillageCodeId,
                VillageCode = x.Consult.Visit.VillageCode.Code,
                VillageColorHex = x.Consult.Visit.VillageCode.ColorHex,
                x.ReferredToId,
                x.AmountPaidUsd,
                x.AmountPaidRiels,
                x.MemberWhoPaidId,
            })
            .ToListAsync(ct);

        var patientIds = rows.Select(x => x.PatientId).Distinct().ToArray();
        var tagsByPatient = await TagSync.LoadPatientTagsBulkAsync(db, patientIds, ct);

        var referrals = rows
            .Select(x => new Response(
                x.Id,
                x.ReferredFor,
                x.ReferralNotes,
                x.ReferralState,
                x.ReferralOutcome,
                x.ConsultId,
                x.CreatedAt,
                x.ConsultDate,
                x.DoctorId,
                x.PatientId,
                x.PatientName,
                x.HasPhoto,
                x.VillageCodeId,
                x.VillageCode,
                x.VillageColorHex,
                tagsByPatient[x.PatientId].ToList(),
                x.ReferredToId,
                x.AmountPaidUsd,
                x.AmountPaidRiels,
                x.MemberWhoPaidId
            ))
            .ToList();

        await Send.OkAsync(referrals, ct);
    }
}
