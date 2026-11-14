using System.IO.Compression;
using System.Text;

namespace SaBai.API.Storage;

/// <summary>
/// Determines an upload's type from its bytes rather than the client's Content-Type header, and
/// prepares stored files for download.
/// </summary>
public static class FileSignatures
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Docx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Content type of the main part of a Word document without macros.</summary>
    private const string DocxMainPart =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private const int MaxZipEntries = 10_000;
    private const int MaxContentTypesBytes = 1024 * 1024;

    /// <summary>Content-Security-Policy for stored files: nothing they contain can run or load.</summary>
    public const string DownloadContentSecurityPolicy = "default-src 'none'; sandbox";

    /// <summary>File extensions accepted for each type; the first is the one files are stored with.</summary>
    private static readonly Dictionary<string, string[]> ExtensionsByType = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [Pdf] = [".pdf"],
        [Jpeg] = [".jpg", ".jpeg"],
        [Png] = [".png"],
        [Docx] = [".docx"],
    };

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// The type of a PDF, JPEG, PNG or DOCX file, read from its content; null for anything else.
    /// A DOCX must be a zip holding <c>[Content_Types].xml</c> that declares a (macro-free) Word
    /// document and <c>word/document.xml</c>. Only the zip directory and that one small part are
    /// read, never the rest of the archive.
    /// </summary>
    public static async Task<string?> DetectAsync(Stream content, CancellationToken ct)
    {
        var stream = content;
        if (!stream.CanSeek)
        {
            stream = new MemoryStream();
            await content.CopyToAsync(stream, ct);
        }

        stream.Position = 0;
        var head = new byte[8];
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var bytes = head.AsSpan(0, read);

        if (bytes.StartsWith(PdfMagic))
            return Pdf;
        if (bytes.StartsWith(JpegMagic))
            return Jpeg;
        if (bytes.StartsWith(PngMagic))
            return Png;
        if (bytes.StartsWith(ZipMagic) && IsDocx(stream))
            return Docx;
        return null;
    }

    private static bool IsDocx(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > MaxZipEntries || zip.GetEntry("word/document.xml") is null)
                return false;

            var contentTypes = zip.GetEntry("[Content_Types].xml");
            if (contentTypes is null || contentTypes.Length > MaxContentTypesBytes)
                return false;

            // The declared size can lie, so the read itself is bounded too.
            using var part = contentTypes.Open();
            var buffer = new byte[MaxContentTypesBytes + 1];
            var length = part.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return length <= MaxContentTypesBytes
                && Encoding.UTF8.GetString(buffer, 0, length).Contains(DocxMainPart);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsImage(string contentType) => contentType is Jpeg or Png;

    /// <summary>The extension a file of this type is stored with.</summary>
    public static string ExtensionFor(string contentType) => ExtensionsByType[contentType][0];

    /// <summary>Whether a file name's extension is one used for this type.</summary>
    public static bool HasExtensionFor(string contentType, string? fileName) =>
        ExtensionsByType[contentType]
            .Contains(Path.GetExtension(fileName ?? ""), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The type of a stored file, from the extension it was stored with (which was derived from its
    /// content); <c>application/octet-stream</c> for anything unknown.
    /// </summary>
    public static string ContentTypeOfStored(string storedName)
    {
        var extension = Path.GetExtension(storedName);
        foreach (var (type, extensions) in ExtensionsByType)
        {
            if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return type;
        }

        return "application/octet-stream";
    }

    /// <summary>
    /// A file name that is safe to offer for download: no path, control characters, quotes or
    /// backslashes, and an extension that matches the file's actual type.
    /// </summary>
    public static string DownloadName(string? fileName, string contentType, string fallback)
    {
        var cleaned = new string(
                Path.GetFileName(fileName ?? "")
                    .Select(c => char.IsControl(c) || c is '"' or '\\' or '/' ? '_' : c)
                    .ToArray()
            )
            .Trim()
            .Trim('.');
        if (cleaned.Length == 0)
            cleaned = fallback;
        if (cleaned.Length > 200)
            cleaned = cleaned[..200];

        return ExtensionsByType.ContainsKey(contentType) && !HasExtensionFor(contentType, cleaned)
            ? cleaned + ExtensionFor(contentType)
            : cleaned;
    }
}
