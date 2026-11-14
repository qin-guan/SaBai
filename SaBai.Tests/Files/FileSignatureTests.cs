using SaBai.API.Storage;
using SaBai.Tests.Support;

namespace SaBai.Tests.Files;

public sealed class FileSignatureTests
{
    [Test]
    public async Task Detect_RecognisesGenuineFiles()
    {
        await Assert.That(await DetectAsync(SampleFiles.Pdf)).IsEqualTo(FileSignatures.Pdf);
        await Assert.That(await DetectAsync(SampleFiles.Png())).IsEqualTo(FileSignatures.Png);
        await Assert.That(await DetectAsync(SampleFiles.Jpeg())).IsEqualTo(FileSignatures.Jpeg);
        await Assert.That(await DetectAsync(SampleFiles.Docx())).IsEqualTo(FileSignatures.Docx);
    }

    [Test]
    public async Task Detect_RejectsLookalikes()
    {
        await Assert.That(await DetectAsync(SampleFiles.Html)).IsNull();
        await Assert.That(await DetectAsync(SampleFiles.Zip())).IsNull();
        await Assert.That(await DetectAsync(SampleFiles.Docx(macroEnabled: true))).IsNull();
        await Assert.That(await DetectAsync([0x50, 0x4B, 0x03, 0x04, 0, 0])).IsNull();
        await Assert.That(await DetectAsync([])).IsNull();
    }

    [Test]
    [Arguments("../../etc/passwd", "application/pdf", "passwd.pdf")]
    [Arguments("lab \"result\".pdf", "application/pdf", "lab _result_.pdf")]
    [Arguments("x\r\nSet-Cookie: a.pdf", "application/pdf", "x__Set-Cookie: a.pdf")]
    [Arguments("report.html", "application/pdf", "report.html.pdf")]
    [Arguments("photo.JPEG", "image/jpeg", "photo.JPEG")]
    [Arguments("", "image/png", "file.png")]
    public async Task DownloadName_IsSanitised(string name, string type, string expected)
    {
        await Assert.That(FileSignatures.DownloadName(name, type, "file")).IsEqualTo(expected);
    }

    [Test]
    public async Task ContentTypeOfStored_FollowsStoredExtension()
    {
        await Assert.That(FileSignatures.ContentTypeOfStored("abc.pdf")).IsEqualTo(FileSignatures.Pdf);
        await Assert.That(FileSignatures.ContentTypeOfStored("patients/x/abc.jpg")).IsEqualTo(FileSignatures.Jpeg);
        await Assert.That(FileSignatures.ContentTypeOfStored("abc.html")).IsEqualTo("application/octet-stream");
    }

    private static async Task<string?> DetectAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await FileSignatures.DetectAsync(stream, CancellationToken.None);
    }
}
