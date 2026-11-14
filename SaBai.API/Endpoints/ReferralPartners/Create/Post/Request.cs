namespace SaBai.API.Endpoints.ReferralPartners.Create.Post;

public sealed record Request(string Name, string? Remarks = null, bool IsActive = true);
