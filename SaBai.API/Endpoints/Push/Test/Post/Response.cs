namespace SaBai.API.Endpoints.Push.Test.Post;

/// <summary>How many of your devices the test notification was sent to.</summary>
/// <param name="Removed">Devices dropped because they no longer accept notifications.</param>
/// <param name="Failed">Devices whose push service could not be reached or refused the message.</param>
public sealed record Response(int Sent, int Removed, int Failed);
