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
            consult.Doctor.Nickname,
            consult.PastMedicalHistory,
            consult.Consultation,
            consult.Diagnoses
                .OrderBy(x => x.Id)
                .Select(x => new ConsultPdfDiagnosis(FormatCategory(x.Category), x.Details))
                .ToList(),
            consult.TreatmentPlan,
            consult.ReferredFor,
            consult.ReferralNotes,
            consult.Remarks));

        await SendPdfAsync(bytes, consult.Id, displayId, ct);
    }

    private async Task SendPdfAsync(byte[] bytes, int consultId, string displayId, CancellationToken ct)
    {
        var safeId = new string(displayId.Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '_').ToArray());
        var fileName = $"consultation{consultId}_report_{safeId}.pdf";
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = "application/pdf";
        HttpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        HttpContext.Response.ContentLength = bytes.Length;
        await HttpContext.Response.StartAsync(ct);
        await HttpContext.Response.Body.WriteAsync(bytes, ct);
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
