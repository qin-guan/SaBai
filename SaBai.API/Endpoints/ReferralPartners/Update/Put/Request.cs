namespace SaBai.API.Endpoints.ReferralPartners.Update.Put;

public sealed record Request(Guid Id, string Name, string? Remarks = null, bool IsActive = true);
