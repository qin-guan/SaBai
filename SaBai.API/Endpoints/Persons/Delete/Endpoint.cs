using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Persons.Delete;

public sealed class Request
{
    public string PersonId { get; set; } = null!;
}

public class Endpoint(FaceStore store) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/persons/{personId}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete every face enrolled for a person.");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        PersonId.Validate(this, req.PersonId);
        ThrowIfAnyErrors();

        if (await store.DeletePersonAsync(req.PersonId, ct) > 0)
            await Send.NoContentAsync(ct);
        else
            await Send.NotFoundAsync(ct);
    }
}
