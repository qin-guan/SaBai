using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Dental.UpsertByVisit.Put;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, DentalConsultResponse>
{
    public override void Configure()
    {
        Put("/visits/{VisitId}/dental");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
            s.Summary = "Save the dental consultation for a visit, creating it if needed"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var visit = await db
            .Visits.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        if (req.ImageFileId is Guid imageFileId)
        {
            var file = await db
                .Files.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == imageFileId && !x.IsDeleted, ct);
            if (file is null)
            {
                throw new BadRequestException("Image file was not found.");
            }

            if (file.PatientId is not null && file.PatientId != visit.PatientId)
            {
                throw new BadRequestException("Image file belongs to a different patient.");
            }
        }

        var entity = await db.DentalConsults.FirstOrDefaultAsync(
            x => x.VisitId == req.VisitId,
            ct
        );
        if (entity is null)
        {
            entity = new DentalConsult { VisitId = req.VisitId };
            db.DentalConsults.Add(entity);
        }

        Apply.To(entity, req);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            throw new ConflictException(
                "The dental consultation for this visit was saved by someone else. Reload and try again."
            );
        }

        await Send.OkAsync(DentalConsultResponse.From(entity), ct);
    }
}
