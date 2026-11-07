using System.Text.Json;

namespace FileHub.BusinessLogic.Media;

/// <summary>How both sources' JSON is read: snake_case on the wire (<c>poster_path</c>,
/// <c>first_release_date</c>), PascalCase on the records, and a missing field is a null.</summary>
public static class SourceJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
