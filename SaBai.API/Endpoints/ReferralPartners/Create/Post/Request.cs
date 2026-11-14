namespace SaBai.API.Endpoints.ReferralPartners.Create.Post;

/// <param name="Id">Id for the new partner, chosen by the client; generated when omitted. Resending a create with the same id returns the existing partner.</param>
public sealed record Request(string Name, string? Remarks = null, bool IsActive = true, Guid? Id = null);
