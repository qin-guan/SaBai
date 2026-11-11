package sabai.api.endpoints.consults.pdf

import org.openpdf.text.Chunk
import org.openpdf.text.Document
import org.openpdf.text.Element
import org.openpdf.text.Font
import org.openpdf.text.PageSize
import org.openpdf.text.Paragraph
import org.openpdf.text.Phrase
import org.openpdf.text.pdf.BaseFont
import org.openpdf.text.pdf.ColumnText
import org.openpdf.text.pdf.PdfPageEventHelper
import org.openpdf.text.pdf.PdfTemplate
import org.openpdf.text.pdf.PdfWriter
import org.openpdf.text.pdf.draw.LineSeparator
import java.awt.Color
import java.io.ByteArrayOutputStream

/** Text printed on a consult PDF. */
data class ConsultPdfContent(
    val patientDisplayId: String,
    val patientName: String,
    val doctorNickname: String,
    val pastMedicalHistory: String?,
    val consultation: String?,
    val diagnoses: List<ConsultPdfDiagnosis>,
    val treatmentPlan: String?,
    val referredFor: String?,
    val referralNotes: String?,
    val remarks: String?,
)

data class ConsultPdfDiagnosis(val category: String?, val details: String)

/**
 * Builds the consult report with OpenPDF. This is the one place the port is clearly
 * wordier than C#: QuestPDF's fluent layout (header/content/footer, "page X of Y")
 * has no JVM equivalent, so the footer and total page count are drawn by hand.
 */
object ConsultPdf {
    private const val CM = 28.35f
    private val regular = Font(Font.HELVETICA, 11f)
    private val bold = Font(Font.HELVETICA, 11f, Font.BOLD)
    private val title = Font(Font.HELVETICA, 18f, Font.BOLD)
    private val muted = Font(Font.HELVETICA, 11f, Font.NORMAL, Color(0x61, 0x61, 0x61))

    fun create(content: ConsultPdfContent): ByteArray {
        val out = ByteArrayOutputStream()
        val document = Document(PageSize.A4, 2 * CM, 2 * CM, 2 * CM, 2 * CM)
        PdfWriter.getInstance(document, out).pageEvent = Footer(content.patientDisplayId)
        document.open()

        document.add(Paragraph("Consultation report", title))
        document.add(Paragraph("${content.patientDisplayId}  ·  ${content.patientName}", muted))
        document.add(Chunk(LineSeparator(1f, 100f, Color(0xE0, 0xE0, 0xE0), Element.ALIGN_CENTER, -6f)))
        document.add(Paragraph(" "))

        document.add(Phrase().apply { add(Chunk("Doctor: ", bold)); add(Chunk(content.doctorNickname, regular)) })
        section(document, "Past medical history", content.pastMedicalHistory)
        section(document, "Consultation", content.consultation)

        document.add(heading("Diagnoses"))
        if (content.diagnoses.isEmpty()) document.add(Paragraph("—", muted))
        for (diagnosis in content.diagnoses) {
            val category = diagnosis.category?.ifBlank { null } ?: "Uncategorised"
            document.add(Paragraph("$category: ${diagnosis.details}", regular))
        }

        section(document, "Treatment plan", content.treatmentPlan)
        section(document, "Referred for", content.referredFor)
        section(document, "Referral notes", content.referralNotes)
        section(document, "Remarks", content.remarks)
        document.close()
        return out.toByteArray()
    }

    private fun heading(text: String) = Paragraph(text, bold).apply { spacingBefore = 10f }

    private fun section(document: Document, title: String, body: String?) {
        document.add(heading(title))
        document.add(Paragraph(body?.ifBlank { null } ?: "—", regular))
    }

    /** "SB0042  ·  3 / 7" in the bottom-right corner; the total is filled in on close. */
    private class Footer(private val displayId: String) : PdfPageEventHelper() {
        private lateinit var total: PdfTemplate
        private val font = BaseFont.createFont(BaseFont.HELVETICA, BaseFont.WINANSI, false)

        override fun onOpenDocument(writer: PdfWriter, document: Document) {
            total = writer.directContent.createTemplate(30f, 12f)
        }

        override fun onEndPage(writer: PdfWriter, document: Document) {
            val text = "$displayId  ·  ${writer.pageNumber} / "
            val width = font.getWidthPoint(text, 9f)
            val x = document.right() - width - 20f
            val y = document.bottom() - 24f
            val canvas = writer.directContent
            ColumnText.showTextAligned(canvas, Element.ALIGN_LEFT, Phrase(text, Font(font, 9f)), x, y, 0f)
            canvas.addTemplate(total, x + width, y)
        }

        override fun onCloseDocument(writer: PdfWriter, document: Document) {
            total.beginText()
            total.setFontAndSize(font, 9f)
            // The writer has already moved past the last page when the document closes.
            total.showText("${writer.pageNumber - 1}")
            total.endText()
        }
    }
}
