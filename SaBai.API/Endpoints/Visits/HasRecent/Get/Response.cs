namespace SaBai.API.Endpoints.Visits.HasRecent.Get;

/// <summary>Whether the patient already had a visit opened within the duplicate window.</summary>
public sealed record Response(bool HasRecentVisit, DateTimeOffset? ExistingVisitDate);
