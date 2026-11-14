using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Sync;

namespace SaBai.API.Endpoints.Sync.Upload.Post;

public sealed record Request(IReadOnlyList<SyncCrudOp> Ops);

public sealed class Endpoint(SyncUploadProcessor processor) : Endpoint<Request, SyncUploadResult>
{
    public override void Configure()
    {
        Post("/sync/upload");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Apply a batch of offline PowerSync CRUD operations idempotently");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var result = await processor.ProcessAsync(req.Ops, User, ct);
        await Send.OkAsync(result, ct);
    }
}
