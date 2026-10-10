using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Delete.Delete;

public sealed class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/auth/users/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "User deletion is disabled");
    }

    public override Task HandleAsync(Request req, CancellationToken ct) =>
        throw new BadRequestException("User deletion is disabled.");
}
