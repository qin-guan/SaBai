using Microsoft.AspNetCore.Identity;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

/// <summary>Startup seeding for roles, the optional bootstrap admin and demo staff.</summary>
public static class AuthSeeder
{
    public const string DemoPassword = "Password1!";

    public static readonly (
        string Email,
        string UserName,
        string Nickname,
        string Role
    )[] DemoPeople =
    [
        ("admin@sabai.test", "admin", "Admin", StaffRoles.Admin),
        ("doctor@sabai.test", "doctor", "Doctor", StaffRoles.Member),
        ("pharmacist@sabai.test", "pharmacist", "Pharmacist", StaffRoles.Member),
        ("registration@sabai.test", "registration", "Registration", StaffRoles.Member),
    ];

    public static async Task EnsureAsync(
        IServiceProvider services,
        AuthSettings settings,
        ILogger logger
    )
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = services.GetRequiredService<UserManager<StaffUser>>();

        foreach (var role in StaffRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Ensure(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"role {role}");
            }
        }

        var bootstrap = settings.BootstrapAdmin;
        if (
            !string.IsNullOrWhiteSpace(bootstrap.UserName)
            && !string.IsNullOrWhiteSpace(bootstrap.Password)
        )
        {
            await EnsureUserAsync(
                users,
                logger,
                bootstrap.UserName.Trim(),
                bootstrap.Email,
                bootstrap.UserName.Trim(),
                StaffRoles.Admin,
                bootstrap.Password
            );
        }

        if (settings.SeedDemoUsers)
        {
            foreach (var person in DemoPeople)
            {
                await EnsureUserAsync(
                    users,
                    logger,
                    person.UserName,
                    person.Email,
                    person.Nickname,
                    person.Role,
                    DemoPassword
                );
            }
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<StaffUser> users,
        ILogger logger,
        string userName,
        string? email,
        string nickname,
        string role,
        string password
    )
    {
        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            user = new StaffUser
            {
                Id = Guid.CreateVersion7(),
                UserName = userName,
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                EmailConfirmed = !string.IsNullOrWhiteSpace(email),
                Nickname = nickname,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            Ensure(await users.CreateAsync(user, password), $"user {userName}");
            logger.LogInformation("Seeded staff user {UserName}", userName);
        }
        else if (!await users.HasPasswordAsync(user))
        {
            Ensure(await users.AddPasswordAsync(user, password), $"password for {userName}");
            logger.LogInformation("Set the configured password for {UserName}", userName);
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            Ensure(await users.AddToRoleAsync(user, role), $"role {role} for {userName}");
        }
    }

    private static void Ensure(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not seed {what}: {string.Join(" ", result.Errors.Select(x => x.Description))}"
            );
        }
    }
}
