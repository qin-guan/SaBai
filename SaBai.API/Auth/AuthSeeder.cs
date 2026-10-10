using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

public static class AuthSeeder
{
    public const string Password = "Password1!";

    public static readonly (string Email, string Username, string Nickname, StaffRole Role)[] People =
    [
        ("admin@sabai.test", "admin", "Admin", StaffRole.Admin),
        ("doctor@sabai.test", "doctor", "Doctor", StaffRole.Member),
        ("pharmacist@sabai.test", "pharmacist", "Pharmacist", StaffRole.Member),
        ("registration@sabai.test", "registration", "Registration", StaffRole.Member),
    ];

    public static async Task EnsureAsync(
        AppDbContext db,
        FakeWorkOsDirectory directory,
        CancellationToken cancellationToken)
    {
        foreach (var person in People)
        {
            var existing = await db.StaffUsers.FirstOrDefaultAsync(
                x => x.Email == person.Email,
                cancellationToken);

            if (existing is null)
            {
                var id = Guid.CreateVersion7();
                var identity = await directory.CreateUserAsync(
                    person.Email,
                    Password,
                    person.Nickname,
                    null,
                    id.ToString(),
                    cancellationToken);

                db.StaffUsers.Add(new StaffUser
                {
                    Id = id,
                    WorkOsUserId = identity.Id,
                    Username = person.Username,
                    Email = person.Email,
                    Nickname = person.Nickname,
                    Role = person.Role,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                directory.Remember(existing.WorkOsUserId, existing.Email, Password);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
