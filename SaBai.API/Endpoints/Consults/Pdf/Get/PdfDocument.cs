using System.Text;

namespace SaBai.API.Endpoints.Consults.Pdf.Get;

/// <summary>Text printed on a consult PDF.</summary>
public sealed record ConsultPdfContent(
    string PatientDisplayId,
    string DoctorNickname,
    string? PastMedicalHistory,
    string? Consultation,
    IReadOnlyList<ConsultPdfDiagnosis> Diagnoses,
    string? TreatmentPlan,
    string? ReferredFor,
    string? ReferralNotes,
    string? Remarks);

/// <summary>One diagnosis line on a consult PDF.</summary>
public sealed record ConsultPdfDiagnosis(string? Category, string Details);

/// <summary>Builds a minimal valid PDF. Not a DI service.</summary>
public static class PdfDocument
{
    public static byte[] Create(ConsultPdfContent content)
    {
        var lines = new List<string>
        {
            $"Patient ID: {content.PatientDisplayId}",
            $"Doctor: {content.DoctorNickname}",
        };
        AddSection(lines, "Past Medical History", content.PastMedicalHistory);
        AddSection(lines, "Consultation", content.Consultation);
        lines.Add("Diagnoses");
        if (content.Diagnoses.Count == 0)
        {
            lines.Add("");
        }
        else
        {
            foreach (var diagnosis in content.Diagnoses)
            {
                var category = string.IsNullOrWhiteSpace(diagnosis.Category) ? "Uncategorised" : diagnosis.Category;
                lines.Add($"{category}: {diagnosis.Details}");
            }
        }

        AddSection(lines, "Treatment Plan", content.TreatmentPlan);
        AddSection(lines, "Referred For", content.ReferredFor);
        AddSection(lines, "Referral Notes", content.ReferralNotes);
        AddSection(lines, "Remarks", content.Remarks);

        var stream = new StringBuilder();
        stream.Append("BT\n/F1 11 Tf\n50 760 Td\n14 TL\n");
        foreach (var line in lines)
        {
            stream.Append('(').Append(Escape(line)).Append(") Tj\nT*\n");
        }

        stream.Append("ET\n");
        return Assemble(stream.ToString());
    }

    private static void AddSection(List<string> lines, string title, string? body)
    {
        lines.Add(title);
        if (string.IsNullOrEmpty(body))
        {
            lines.Add("");
            return;
        }

        foreach (var line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            lines.Add(line);
        }
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '(':
                case ')':
                case '\\':
                    builder.Append('\\').Append(ch);
                    break;
                default:
                    builder.Append(ch is >= ' ' and <= '~' ? ch : '?');
                    break;
            }
        }

        return builder.ToString();
    }

    private static byte[] Assemble(string stream)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new int[objects.Length + 1];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = builder.Length;
            builder.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var startxref = builder.Length;
        builder.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        builder.Append("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Length; i++)
        {
            builder.Append(offsets[i].ToString("D10")).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\n");
        builder.Append("startxref\n").Append(startxref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
