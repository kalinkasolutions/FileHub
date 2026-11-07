namespace FileHub.BusinessLogic.Media.Igdb;

/// <summary>What Twitch's <c>oauth2/token</c> answers a client-credentials request with.</summary>
public sealed record TwitchToken
{
    public string? AccessToken { get; init; }

    /// <summary>Seconds the token is good for.</summary>
    public int? ExpiresIn { get; init; }
}
