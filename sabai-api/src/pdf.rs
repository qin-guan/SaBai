//! The consult report PDF, typeset with Typst.
//!
//! The layout lives in [`TEMPLATE`]. The consult's text reaches it as data
//! (`sys.inputs.report`), never as markup, so whatever a patient record contains prints
//! literally. Fonts are embedded in the binary; the template can read no files and
//! import no packages.

use std::sync::LazyLock;

use anyhow::anyhow;
use typst::diag::{FileError, FileResult, SourceDiagnostic};
use typst::foundations::{Array, Bytes, Datetime, Dict, IntoValue, Value};
use typst::layout::PagedDocument;
use typst::syntax::{FileId, Source, VirtualPath};
use typst::text::{Font, FontBook};
use typst::utils::LazyHash;
use typst::{Library, World};

/// Text printed on a consult PDF.
#[derive(Debug, Clone, Default)]
pub struct ConsultPdfContent {
    pub patient_display_id: String,
    pub patient_name: String,
    pub doctor_nickname: String,
    pub past_medical_history: Option<String>,
    pub consultation: Option<String>,
    pub diagnoses: Vec<ConsultPdfDiagnosis>,
    pub treatment_plan: Option<String>,
    pub referred_for: Option<String>,
    pub referral_notes: Option<String>,
    pub remarks: Option<String>,
}

/// One diagnosis line on a consult PDF.
#[derive(Debug, Clone, Default)]
pub struct ConsultPdfDiagnosis {
    pub category: Option<String>,
    pub details: String,
}

/// A4 with 2 cm margins and 11 pt text. The header and footer sit inside the margins'
/// inner edge, 12 pt away from the body; lines are set solid (one line is the font's
/// ascender to descender), as the previous report was.
const TEMPLATE: &str = r##"
#let report = sys.inputs.report
#let header-height = 18pt * 1.2 + 11pt * 1.2 + 6pt + 1pt
#let footer-height = 11pt * 1.2
#let gap = 12pt
#let blank(body) = body == none or body.trim() == ""

#set text(
  font: ("Lato", "Noto Sans Khmer"),
  size: 11pt,
  top-edge: "ascender",
  bottom-edge: "descender",
)
#set par(leading: 0pt, spacing: 10pt)
#set block(spacing: 10pt)
#set page(
  paper: "a4",
  fill: white,
  margin: (x: 2cm, top: 2cm + header-height + gap, bottom: 2cm + footer-height + gap),
  header-ascent: gap,
  footer-descent: gap,
  header: {
    set block(spacing: 0pt)
    block(text(size: 18pt, weight: "semibold", "Consultation report"))
    block(text(fill: rgb("#616161"), report.patient-display-id + "  ·  " + report.patient-name))
    block(inset: (top: 6pt), line(length: 100%, stroke: 1pt + rgb("#e0e0e0")))
  },
  footer: context align(
    right,
    report.patient-display-id
      + "  ·  "
      + str(counter(page).get().first())
      + " / "
      + str(counter(page).final().first()),
  ),
)

#let title(body) = block(text(weight: "semibold", body))
#let section(name, body) = {
  title(name)
  block(if blank(body) { "—" } else { body })
}

#block(text(weight: "semibold", "Doctor: ") + report.doctor-nickname)
#section("Past medical history", report.past-medical-history)
#section("Consultation", report.consultation)
#title("Diagnoses")
#if report.diagnoses.len() == 0 {
  block(text(fill: rgb("#757575"), "—"))
} else {
  for diagnosis in report.diagnoses {
    let category = if blank(diagnosis.category) { "Uncategorised" } else { diagnosis.category }
    block(category + ": " + diagnosis.details)
  }
}
#section("Treatment plan", report.treatment-plan)
#section("Referred for", report.referred-for)
#section("Referral notes", report.referral-notes)
#section("Remarks", report.remarks)
"##;

/// Lato first, as the previous report's Latin font; Noto Sans Khmer covers Khmer.
static FONT_FILES: [&[u8]; 4] = [
    include_bytes!("../fonts/Lato-Regular.ttf"),
    include_bytes!("../fonts/Lato-SemiBold.ttf"),
    include_bytes!("../fonts/NotoSansKhmer-Regular.ttf"),
    include_bytes!("../fonts/NotoSansKhmer-SemiBold.ttf"),
];

struct Fonts {
    book: LazyHash<FontBook>,
    fonts: Vec<Font>,
}

static FONTS: LazyLock<Fonts> = LazyLock::new(|| {
    let fonts: Vec<Font> = FONT_FILES.iter().flat_map(|file| Font::iter(Bytes::new(*file))).collect();
    Fonts { book: LazyHash::new(FontBook::from_fonts(&fonts)), fonts }
});

/// Everything Typst may see while compiling a report: the template, the embedded fonts
/// and the report's text. There are no other files and no packages.
struct ReportWorld {
    library: LazyHash<Library>,
    main: Source,
}

impl ReportWorld {
    fn new(content: &ConsultPdfContent) -> Self {
        let mut inputs = Dict::new();
        inputs.insert("report".into(), report_value(content));
        ReportWorld {
            library: LazyHash::new(Library::builder().with_inputs(inputs).build()),
            main: Source::new(FileId::new(None, VirtualPath::new("consult-report.typ")), TEMPLATE.to_owned()),
        }
    }
}

impl World for ReportWorld {
    fn library(&self) -> &LazyHash<Library> {
        &self.library
    }

    fn book(&self) -> &LazyHash<FontBook> {
        &FONTS.book
    }

    fn main(&self) -> FileId {
        self.main.id()
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        if id == self.main.id() {
            Ok(self.main.clone())
        } else {
            Err(FileError::AccessDenied)
        }
    }

    fn file(&self, _id: FileId) -> FileResult<Bytes> {
        Err(FileError::AccessDenied)
    }

    fn font(&self, index: usize) -> Option<Font> {
        FONTS.fonts.get(index).cloned()
    }

    fn today(&self, _offset: Option<i64>) -> Option<Datetime> {
        None
    }
}

fn text(value: &str) -> Value {
    value.into_value()
}

fn optional(value: &Option<String>) -> Value {
    value.as_deref().map_or(Value::None, text)
}

fn report_value(content: &ConsultPdfContent) -> Value {
    let diagnoses: Array = content
        .diagnoses
        .iter()
        .map(|diagnosis| {
            let mut entry = Dict::new();
            entry.insert("category".into(), optional(&diagnosis.category));
            entry.insert("details".into(), text(&diagnosis.details));
            Value::Dict(entry)
        })
        .collect();

    let mut report = Dict::new();
    report.insert("patient-display-id".into(), text(&content.patient_display_id));
    report.insert("patient-name".into(), text(&content.patient_name));
    report.insert("doctor-nickname".into(), text(&content.doctor_nickname));
    report.insert("past-medical-history".into(), optional(&content.past_medical_history));
    report.insert("consultation".into(), optional(&content.consultation));
    report.insert("diagnoses".into(), Value::Array(diagnoses));
    report.insert("treatment-plan".into(), optional(&content.treatment_plan));
    report.insert("referred-for".into(), optional(&content.referred_for));
    report.insert("referral-notes".into(), optional(&content.referral_notes));
    report.insert("remarks".into(), optional(&content.remarks));
    Value::Dict(report)
}

fn failure(stage: &str, diagnostics: &[SourceDiagnostic]) -> anyhow::Error {
    let messages: Vec<&str> = diagnostics.iter().map(|diagnostic| diagnostic.message.as_str()).collect();
    anyhow!("the consult report could not be {stage}: {}", messages.join("; "))
}

fn typeset(content: &ConsultPdfContent) -> anyhow::Result<PagedDocument> {
    let world = ReportWorld::new(content);
    typst::compile::<PagedDocument>(&world).output.map_err(|errors| failure("typeset", &errors))
}

/// Builds the consult report. CPU-bound: call it from a blocking task.
pub fn consult_report(content: &ConsultPdfContent) -> anyhow::Result<Vec<u8>> {
    let document = typeset(content)?;
    let pdf =
        typst_pdf::pdf(&document, &typst_pdf::PdfOptions::default()).map_err(|errors| failure("written", &errors));
    // Typst memoises its work between compilations; drop what recent reports did not reuse.
    typst::comemo::evict(8);
    pdf
}

#[cfg(test)]
mod tests {
    use typst::layout::{Frame, FrameItem, Point};

    use super::*;

    /// The text runs of a frame with the font each was set in, in drawing order.
    fn runs(frame: &Frame, origin: Point, out: &mut Vec<(String, String, f64)>) {
        for (position, item) in frame.items() {
            let at = origin + *position;
            match item {
                FrameItem::Group(group) => runs(&group.frame, at, out),
                FrameItem::Text(run) => {
                    assert!(run.glyphs.iter().all(|glyph| glyph.id != 0), "no glyph for part of {:?}", run.text);
                    out.push((run.text.to_string(), run.font.info().family.clone(), at.x.to_pt()))
                }
                _ => {}
            }
        }
    }

    fn page_runs(document: &PagedDocument, page: usize) -> Vec<(String, String, f64)> {
        let mut out = Vec::new();
        runs(&document.pages[page].frame, Point::zero(), &mut out);
        out
    }

    fn page_text(document: &PagedDocument, page: usize) -> String {
        page_runs(document, page).into_iter().map(|(text, _, _)| text).collect::<Vec<_>>().join("\n")
    }

    fn khmer_content() -> ConsultPdfContent {
        ConsultPdfContent {
            patient_display_id: "PC0012".into(),
            patient_name: "សុខ តារា (Sok Dara)".into(),
            doctor_nickname: "វេជ្ជបណ្ឌិត ហេង សុភា".into(),
            past_medical_history: Some("ជំងឺហឺត".into()),
            consultation: Some("ក្អករយៈពេល ៣ ថ្ងៃ និងគ្រុនក្តៅ".into()),
            diagnoses: vec![ConsultPdfDiagnosis { category: Some("Respiratory".into()), details: "ក្អកស្រួចស្រាវ".into() }],
            treatment_plan: Some("សម្រាក និងផឹកទឹកឱ្យបានច្រើន".into()),
            referred_for: Some("ផ្នែកធ្មេញ".into()),
            referral_notes: Some("ពិនិត្យបន្ត".into()),
            remarks: Some("គ្មាន".into()),
        }
    }

    #[test]
    fn report_with_khmer_text_is_a_pdf_set_in_both_fonts() {
        let content = khmer_content();
        let bytes = consult_report(&content).unwrap();
        assert!(bytes.starts_with(b"%PDF"));

        let document = typeset(&content).unwrap();
        assert_eq!(document.pages.len(), 1);
        let runs = page_runs(&document, 0);
        let khmer: String =
            runs.iter().filter(|(_, family, _)| family == "Noto Sans Khmer").map(|(text, _, _)| text.as_str()).collect();
        let latin: String = runs.iter().filter(|(_, family, _)| family == "Lato").map(|(text, _, _)| text.as_str()).collect();
        assert!(khmer.contains("ជំងឺហឺត"), "{khmer}");
        assert!(khmer.contains("ក្អកស្រួចស្រាវ"), "{khmer}");
        assert!(latin.contains("Consultation report"), "{latin}");
        assert!(latin.contains("Respiratory: "), "{latin}");
        assert!(latin.contains("PC0012  ·  1 / 1"), "{latin}");
        assert!(runs.iter().all(|(_, family, _)| family == "Lato" || family == "Noto Sans Khmer"));
    }

    #[test]
    fn blank_sections_print_a_dash_and_uncategorised_diagnoses_say_so() {
        let content = ConsultPdfContent {
            patient_display_id: "A10001".into(),
            patient_name: "Ann".into(),
            doctor_nickname: "Doc".into(),
            consultation: Some("  \n ".into()),
            remarks: Some("fine".into()),
            diagnoses: vec![
                ConsultPdfDiagnosis { category: None, details: "rash".into() },
                ConsultPdfDiagnosis { category: Some(" ".into()), details: "itch".into() },
                ConsultPdfDiagnosis { category: Some("Ear Nose Throat".into()), details: "cough".into() },
            ],
            ..Default::default()
        };
        let text = page_text(&typeset(&content).unwrap(), 0);
        assert_eq!(
            text,
            [
                "Consultation report",
                "A10001  ·  Ann",
                "Doctor: ",
                "Doc",
                "Past medical history",
                "—",
                "Consultation",
                "—",
                "Diagnoses",
                "Uncategorised: rash",
                "Uncategorised: itch",
                "Ear Nose Throat: cough",
                "Treatment plan",
                "—",
                "Referred for",
                "—",
                "Referral notes",
                "—",
                "Remarks",
                "fine",
                "A10001  ·  1 / 1",
            ]
            .join("\n")
        );

        let empty = ConsultPdfContent { diagnoses: Vec::new(), ..content };
        let text = page_text(&typeset(&empty).unwrap(), 0);
        assert!(text.contains("Diagnoses\n—\nTreatment plan"), "{text}");
    }

    #[test]
    fn markup_characters_and_line_breaks_print_literally() {
        let tricky = "#import \"x\" *bold* _it_ [link] $x^2$ `raw` <label> @ref \\ // not a comment\n= second line #sys.inputs";
        let content = ConsultPdfContent {
            patient_display_id: "A1#0001".into(),
            patient_name: "#strong[Ann] *".into(),
            doctor_nickname: "Dr. #1".into(),
            consultation: Some(tricky.into()),
            diagnoses: vec![ConsultPdfDiagnosis { category: Some("Others".into()), details: "#panic()".into() }],
            ..Default::default()
        };
        let text = page_text(&typeset(&content).unwrap(), 0);
        let (first, second) = tricky.split_once('\n').unwrap();
        assert!(text.contains(&format!("\n{first}\n{second}\n")), "{text}");
        assert!(text.contains("A1#0001  ·  #strong[Ann] *"), "{text}");
        assert!(text.contains("Others: #panic()"), "{text}");
    }

    #[test]
    fn long_report_continues_on_a_second_page_with_header_and_numbered_footer() {
        let content = ConsultPdfContent {
            patient_display_id: "A10001".into(),
            patient_name: "Ann".into(),
            doctor_nickname: "Doc".into(),
            consultation: Some((1..=70).map(|n| format!("Observation number {n}.")).collect::<Vec<_>>().join("\n")),
            ..Default::default()
        };
        let document = typeset(&content).unwrap();
        assert_eq!(document.pages.len(), 2);

        let first = page_text(&document, 0);
        let second = page_text(&document, 1);
        assert!(first.starts_with("Consultation report\nA10001  ·  Ann\n"), "{first}");
        assert!(second.starts_with("Consultation report\nA10001  ·  Ann\n"), "{second}");
        assert!(first.ends_with("A10001  ·  1 / 2"), "{first}");
        assert!(second.ends_with("A10001  ·  2 / 2"), "{second}");
        assert!(first.contains("Observation number 1.") && second.contains("Observation number 70."));
        assert!(second.contains("Remarks\n—"), "{second}");
    }

    #[test]
    fn page_is_a4_with_the_footer_right_aligned_inside_two_centimetre_margins() {
        let document = typeset(&khmer_content()).unwrap();
        let page = &document.pages[0].frame;
        assert!((page.width().to_mm() - 210.0).abs() < 0.01 && (page.height().to_mm() - 297.0).abs() < 0.01);

        let runs = page_runs(&document, 0);
        let left = 20.0 / 25.4 * 72.0;
        let (_, _, title_x) = &runs[0];
        assert!((title_x - left).abs() < 0.01, "title starts at {title_x}pt");
        let (footer, _, footer_x) = runs.last().unwrap();
        assert_eq!(footer, "PC0012  ·  1 / 1");
        assert!(*footer_x > page.width().to_pt() / 2.0, "footer starts at {footer_x}pt");
    }
}
