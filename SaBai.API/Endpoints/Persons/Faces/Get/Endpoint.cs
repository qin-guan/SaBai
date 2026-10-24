using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Persons.Faces.Get;

public sealed class Request
{
    public string PersonId { get; set; } = null!;
}

public sealed record Response(string PersonId, StoredFace[] Faces);

public class Endpoint(FaceStore store) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/persons/{personId}/faces");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List the faces enrolled for a person.");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        PersonId.Validate(this, req.PersonId);
        ThrowIfAnyErrors();

        var faces = await store.ListAsync(req.PersonId, ct);
        await Send.OkAsync(new Response(req.PersonId, faces.ToArray()), ct);
    }
}
