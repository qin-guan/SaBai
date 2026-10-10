using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Files.Create.Post;

public sealed class Endpoint(AppDbContext db, IOptions<StorageSettings> storage) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Post("/files");
        AllowFileUploads();
        MaxRequestBodySize(ClinicalFile.MaxBytes * 8L);
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Upload clinical files to local storage");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var (patientId, files, descriptions) = await ReadFormAsync(req, ct);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId, ct))
        {
            throw new BadRequestException("Patient was not found.");
        }

        if (files.Count == 0)
        {
            throw new BadRequestException("No file was uploaded.");
        }

        if (files.Count != descriptions.Count)
        {
            throw new BadRequestException("Number of files and descriptions must match.");
        }

        var root = string.IsNullOrWhiteSpace(storage.Value.Root) ? "data/files" : storage.Value.Root;
        Directory.CreateDirectory(root);

        var written = new List<string>();
        var created = new List<ClinicalFile>();
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                var upload = files[i];
                var fileName = Path.GetFileName(upload.FileName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    throw new BadRequestException("No name provided.");
                }

                if (fileName.Length > 255)
                {
                    throw new BadRequestException("File name is too long.");
                }

                if (upload.Length > ClinicalFile.MaxBytes)
                {
                    throw new BadRequestException("File is too large.");
                }

                var extension = Path.GetExtension(fileName);
                var storedName = $"{Guid.NewGuid():N}{extension}";
                var fullPath = Path.Combine(root, storedName);
                await using (var stream = File.Create(fullPath))
                {
                    await upload.CopyToAsync(stream, ct);
                }

                written.Add(fullPath);
                var file = new ClinicalFile
                {
                    PatientId = patientId,
                    FileName = fileName,
                    StoredFileName = storedName,
                    Description = descriptions[i] ?? "",
                    SizeBytes = upload.Length,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                created.Add(file);
                db.ClinicalFiles.Add(file);
            }

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var path in written)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            throw;
        }

        var responses = created
            .Select(file => new Response(
                file.Id,
                file.PatientId,
                file.FileName,
                file.StoredFileName,
                file.Description,
                file.SizeBytes,
                file.IsDeleted,
                file.CreatedAt))
            .ToList();
        await Send.CreatedAtAsync<Get.Endpoint>(new { Id = responses[0].Id }, responses, cancellation: ct);
    }

    private async Task<(int PatientId, IReadOnlyList<IFormFile> Files, IReadOnlyList<string> Descriptions)> ReadFormAsync(
        Request req,
        CancellationToken ct)
    {
        if (!HttpContext.Request.HasFormContentType)
        {
            return (req.PatientId, req.Files ?? [], req.Descriptions ?? []);
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var patientId = req.PatientId;
        if (patientId <= 0)
        {
            var rawPatientId = FirstValue(form, "patientId", "PatientId");
            if (int.TryParse(rawPatientId, out var parsed))
            {
                patientId = parsed;
            }
        }

        var formFiles = form.Files.GetFiles("files");
        if (formFiles.Count == 0)
        {
            formFiles = form.Files.GetFiles("Files");
        }

        IReadOnlyList<IFormFile> files = formFiles;
        if (files.Count == 0 && req.Files is { Count: > 0 })
        {
            files = req.Files;
        }

        if (files.Count == 0 && Files.Count > 0)
        {
            files = Files;
        }

        var rawDescriptions = form["descriptions"];
        if (rawDescriptions.Count == 0)
        {
            rawDescriptions = form["Descriptions"];
        }

        IReadOnlyList<string> descriptions = rawDescriptions.Count > 0
            ? rawDescriptions.Select(value => value ?? "").ToList()
            : req.Descriptions ?? [];

        return (patientId, files, descriptions);
    }

    private static string? FirstValue(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (form.TryGetValue(name, out var value) && value.Count > 0)
            {
                return value[0];
            }
        }

        return null;
    }
}
