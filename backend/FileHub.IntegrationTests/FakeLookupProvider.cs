using Entities.Paths;
using FileHub.BusinessLogic.Media;

namespace FileHub.IntegrationTests;

/// <summary>
/// A look-up source with a scripted answer. The tests are about what the service does with an
/// answer — caches it, refuses to ask twice, forgets it with the disk — not about TMDb's JSON, so
/// the network never enters into it.
/// </summary>
public sealed class FakeLookupProvider : IMediaLookupProvider
{
    public FakeLookupProvider(BasePathKind kind)
    {
        Kind = kind;
        Name = kind == BasePathKind.Media ? "TMDb" : "IGDB";
        Source = kind == BasePathKind.Media ? "tmdb" : "igdb";
    }

    public BasePathKind Kind { get; }
    public string Name { get; }
    public string Source { get; }
    public bool IsConfigured { get; set; } = true;
    public string NotConfiguredHint => "set the key";

    /// <summary>What the next look-up answers; null means "nothing matches".</summary>
    public MediaLookup? Answer { get; set; }

    /// <summary>Thrown by the next look-up instead of answering, when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Every title the service asked for, in order.</summary>
    public List<ParsedTitle> Calls { get; } = [];

    public Task<MediaLookup?> LookupAsync(ParsedTitle title, CancellationToken cancellationToken)
    {
        Calls.Add(title);

        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(Answer);
    }

    public static MediaLookup Movie(string title, int year, string? posterUrl = "https://posters.example/dune.jpg") => new(
        MediaType: "movie",
        ExternalId: 693134,
        ExternalUrl: "https://www.themoviedb.org/movie/693134",
        Title: title,
        Year: year,
        Overview: "Paul Atreides unites with Chani and the Fremen.",
        Rating: 8.2,
        Genres: ["Science Fiction", "Adventure"],
        Platforms: [],
        PosterUrl: posterUrl);
}
