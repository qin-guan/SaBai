using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationBrands.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/medication-brands/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a medication brand");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var brand =
            await db.MedicationBrands.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new NotFoundException("Medication brand was not found.");

        if (await db.MedicationStocks.AnyAsync(x => x.MedicationBrandId == req.Id, ct))
        {
            throw new ConflictException("The brand is referenced by medication stock.");
        }

        if (await db.Orders.AnyAsync(x => x.MedicationBrandId == req.Id, ct))
        {
            throw new ConflictException("The brand is referenced by an order.");
        }

        // Removed through the change tracker (not ExecuteDelete) so the audit log records it.
        db.MedicationBrands.Remove(brand);
        await db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
