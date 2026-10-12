using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SaBai.API.Endpoints.Consults.Pdf.Get;

/// <summary>Text printed on a consult PDF.</summary>
public sealed record ConsultPdfContent(
    string PatientDisplayId,
    string PatientName,
    string DoctorNickname,
    string? PastMedicalHistory,
    string? Consultation,
    IReadOnlyList<ConsultPdfDiagnosis> Diagnoses,
    string? TreatmentPlan,
    string? ReferredFor,
    string? ReferralNotes,
    string? Remarks
);

/// <summary>One diagnosis line on a consult PDF.</summary>
public sealed record ConsultPdfDiagnosis(string? Category, string Details);

/// <summary>Builds the consult report with QuestPDF.</summary>
public static class PdfDocument
{
    public static byte[] Create(ConsultPdfContent content)
    {
        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style.FontSize(11));

                    page.Header()
                        .Column(header =>
                        {
                            header.Item().Text("Consultation report").SemiBold().FontSize(18);
                            header
                                .Item()
                                .Text($"{content.PatientDisplayId}  ·  {content.PatientName}")
                                .FontColor(Colors.Grey.Darken2);
                            header
                                .Item()
                                .PaddingTop(6)
                                .LineHorizontal(1)
                                .LineColor(Colors.Grey.Lighten2);
                        });

                    page.Content()
                        .PaddingVertical(12)
                        .Column(column =>
                        {
                            column.Spacing(10);
                            column
                                .Item()
                                .Text(text =>
                                {
                                    text.Span("Doctor: ").SemiBold();
                                    text.Span(content.DoctorNickname);
                                });

                            Section(column, "Past medical history", content.PastMedicalHistory);
                            Section(column, "Consultation", content.Consultation);

                            column.Item().Text("Diagnoses").SemiBold();
                            if (content.Diagnoses.Count == 0)
                            {
                                column.Item().Text("—").FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                foreach (var diagnosis in content.Diagnoses)
                                {
                                    var category = string.IsNullOrWhiteSpace(diagnosis.Category)
                                        ? "Uncategorised"
                                        : diagnosis.Category;
                                    column.Item().Text($"{category}: {diagnosis.Details}");
                                }
                            }

                            Section(column, "Treatment plan", content.TreatmentPlan);
                            Section(column, "Referred for", content.ReferredFor);
                            Section(column, "Referral notes", content.ReferralNotes);
                            Section(column, "Remarks", content.Remarks);
                        });

                    page.Footer()
                        .AlignRight()
                        .Text(text =>
                        {
                            text.Span(content.PatientDisplayId);
                            text.Span("  ·  ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            })
            .GeneratePdf();
    }

    private static void Section(ColumnDescriptor column, string title, string? body)
    {
        column.Item().Text(title).SemiBold();
        column.Item().Text(string.IsNullOrWhiteSpace(body) ? "—" : body);
    }
}
