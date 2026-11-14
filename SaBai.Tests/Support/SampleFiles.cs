using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using SkiaSharp;

namespace SaBai.Tests.Support;

/// <summary>Small but genuine files of each accepted upload type, plus content that only pretends to be one.</summary>
public static class SampleFiles
{
    public const string DocxType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static readonly byte[] Html = Encoding.UTF8.GetBytes(
        "<html><body><script>alert(document.cookie)</script></body></html>"
    );

    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n"
    );

    /// <summary>A decodable 16×16 PNG.</summary>
    public static byte[] Png() => Encode(SKEncodedImageFormat.Png);

    /// <summary>A decodable 16×16 JPEG.</summary>
    public static byte[] Jpeg() => Encode(SKEncodedImageFormat.Jpeg);

    /// <summary>A minimal Word document: a zip with the content-types part and the main document part.</summary>
    public static byte[] Docx(bool macroEnabled = false)
    {
        var mainType = macroEnabled
            ? "application/vnd.ms-word.document.macroEnabled.main+xml"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(
                zip,
                "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                    + $"<Override PartName=\"/word/document.xml\" ContentType=\"{mainType}\"/></Types>"
            );
            Write(
                zip,
                "word/document.xml",
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
                    + "<w:body><w:p><w:r><w:t>clinic note</w:t></w:r></w:p></w:body></w:document>"
            );
        }

        return buffer.ToArray();
    }

    /// <summary>A zip that is not a Word document.</summary>
    public static byte[] Zip()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "readme.txt", "not a document");
        }

        return buffer.ToArray();
    }

    public static ByteArrayContent Part(byte[] bytes, string contentType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return part;
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(new SKColor(200, 180, 160));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
