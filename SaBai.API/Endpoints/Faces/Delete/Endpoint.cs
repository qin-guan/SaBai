using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Faces.Delete;

public sealed class Request
{
    public Guid FaceId { get; set; }
}

public class Endpoint(FaceStore store) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/faces/{faceId}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a single enrolled face.");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (await store.DeleteFaceAsync(req.FaceId, ct))
            await Send.NoContentAsync(ct);
        else
            await Send.NotFoundAsync(ct);
    }
}
