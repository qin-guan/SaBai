using System.Reflection;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Pdf.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/consults/{Id}/pdf");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Download a consult as a PDF");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var consult = await db.Consults
            .AsNoTracking()
            .Include(x => x.Doctor)
            .Include(x => x.Diagnoses)
            .Include(x => x.Visit).ThenInclude(x => x.Patient).ThenInclude(x => x.VillageCode)
            .Include(x => x.Visit).ThenInclude(x => x.VillageCode)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Consult was not found.", ct);

        var code = consult.Visit.Patient.VillageCode?.Code ?? consult.Visit.VillageCode.Code;
        var displayId = Patient.FormatDisplayId(code, consult.Visit.Patient.Id);
        var bytes = PdfDocument.Create(new ConsultPdfContent(
            displayId,
            consult.Visit.Patient.Name,
            consult.Doctor.Nickname,
            consult.PastMedicalHistory,
            consult.Consultation,
            [
                .. consult.Diagnoses
                    .OrderBy(x => x.Id)
                    .Select(x => new ConsultPdfDiagnosis(FormatCategory(x.Category), x.Details))
            ],
            consult.TreatmentPlan,
            consult.ReferredFor,
            consult.ReferralNotes,
            consult.Remarks));

        var safeId = new string(displayId.Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '_').ToArray());
        var fileName = $"consultation{consult.Id}_report_{safeId}.pdf";
        HttpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        await Send.StreamAsync(new MemoryStream(bytes), fileName, bytes.Length, "application/pdf", cancellation: ct);
    }

    private static string FormatCategory(DiagnosisCategory? category)
    {
        if (category is null)
        {
            return "";
        }

        var field = typeof(DiagnosisCategory).GetField(category.Value.ToString());
        return field?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? category.Value.ToString();
    }
}
