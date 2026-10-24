using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/medication-stock/{Id}/splits");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Split a stock lot into child lots");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var response = await SplitAsync(req, ct);
        await Send.OkAsync(response, ct);
    }

    private async Task<Response> SplitAsync(Request req, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<Response>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var parent = await db.MedicationStocks.FirstOrNotFoundAsync(
                x => x.Id == req.Id,
                "Medication stock was not found.",
                ct
            );

            if (parent.Quantity == 0)
            {
                throw new ConflictException("The parent stock has already split.");
            }

            var children = req
                .Splits.Select(split => new
                {
                    split.Quantity,
                    Location = split.Location.Trim(),
                    split.StockStatus,
                    Remarks = string.IsNullOrWhiteSpace(split.Remarks)
                        ? null
                        : split.Remarks.Trim(),
                })
                .ToList();

            if (children.Sum(x => x.Quantity) != parent.Quantity)
            {
                throw new ConflictException(
                    "Child stock quantity does not equal parent stock quantity!"
                );
            }

            var distinct = children
                .Select(x =>
                    $"{x.Location}\u001f{x.StockStatus}\u001f{x.Quantity}\u001f{x.Remarks ?? ""}"
                )
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (distinct != children.Count)
            {
                throw new ConflictException("Splits are not distinct!");
            }

            foreach (var child in children)
            {
                db.MedicationStocks.Add(
                    new SaBai.API.Domain.MedicationStock
                    {
                        MedicationBrandId = parent.MedicationBrandId,
                        Quantity = child.Quantity,
                        Expiry = parent.Expiry,
                        Location = child.Location,
                        StockStatus = child.StockStatus,
                        Remarks = child.Remarks,
                    }
                );
            }

            parent.Quantity = 0;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new Response(true);
        });
    }
}
